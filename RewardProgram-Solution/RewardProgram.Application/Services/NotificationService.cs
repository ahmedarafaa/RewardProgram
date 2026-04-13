using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Notifications;
using RewardProgram.Application.Contracts.Notifications;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IFirebaseMessagingService _fcm;
    private readonly ILogger<NotificationService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public NotificationService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        IFirebaseMessagingService fcm,
        ILogger<NotificationService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _userRepository = userRepository;
        _fcm = fcm;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    // ── Device Registration ──

    public async Task<Result> RegisterDeviceAsync(string userId, string fcmToken, CancellationToken ct)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure(NotificationErrors.UserNotFound);

        user.FcmToken = fcmToken.Trim();
        await _userRepository.UpdateAsync(user);

        _logger.LogInformation("FCM token registered for user {UserId}", userId);
        return Result.Success();
    }

    public async Task<Result> UnregisterDeviceAsync(string userId, CancellationToken ct)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure(NotificationErrors.UserNotFound);

        user.FcmToken = null;
        await _userRepository.UpdateAsync(user);

        _logger.LogInformation("FCM token cleared for user {UserId}", userId);
        return Result.Success();
    }

    // ── User-Facing ──

    public async Task<PaginatedResult<NotificationResponse>> GetUserNotificationsAsync(
        string userId, NotificationListQuery query, CancellationToken ct)
    {
        var (page, pageSize) = Application.Helpers.PaginationHelper.Normalize(query.Page, query.PageSize);

        var baseQuery = _context.Notifications
            .Where(n => n.UserId == userId && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt);

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationResponse(
                n.Id,
                n.Type,
                n.Title,
                n.Body,
                n.ReferenceId,
                n.IsRead,
                n.CreatedAt))
            .ToListAsync(ct);

        return new PaginatedResult<NotificationResponse>(items, totalCount, page, pageSize);
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken ct)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead && !n.IsDeleted, ct);
    }

    public async Task<Result> MarkAsReadAsync(string notificationId, string userId, CancellationToken ct)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && !n.IsDeleted, ct);

        if (notification is null)
            return Result.Failure(NotificationErrors.NotificationNotFound);

        if (notification.UserId != userId)
            return Result.Failure(NotificationErrors.NotificationNotOwned);

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    public async Task<Result> MarkAllAsReadAsync(string userId, CancellationToken ct)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead && !n.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);

        return Result.Success();
    }

    public async Task<Result> DeleteNotificationAsync(string notificationId, string userId, CancellationToken ct)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && !n.IsDeleted, ct);

        if (notification is null)
            return Result.Failure(NotificationErrors.NotificationNotFound);

        if (notification.UserId != userId)
            return Result.Failure(NotificationErrors.NotificationNotOwned);

        notification.IsDeleted = true;
        notification.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return Result.Success();
    }

    // ── Notification Preferences ──

    public async Task<List<NotificationPreferenceItem>> GetPreferencesAsync(string userId, CancellationToken ct)
    {
        var saved = await _context.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);

        // Return all 9 types with their current mute status
        return Enum.GetValues<NotificationType>()
            .Select(type => new NotificationPreferenceItem(
                type,
                saved.FirstOrDefault(p => p.NotificationType == type)?.IsPushMuted ?? false))
            .ToList();
    }

    public async Task<Result> UpdatePreferencesAsync(string userId, UpdateNotificationPreferencesRequest request, CancellationToken ct)
    {
        var existing = await _context.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);

        foreach (var pref in request.Preferences)
        {
            var entry = existing.FirstOrDefault(e => e.NotificationType == pref.Type);
            if (entry is not null)
            {
                entry.IsPushMuted = pref.IsPushMuted;
            }
            else if (pref.IsPushMuted)
            {
                _context.UserNotificationPreferences.Add(new UserNotificationPreference
                {
                    UserId = userId,
                    NotificationType = pref.Type,
                    IsPushMuted = true
                });
            }
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ── Internal — CreateAsync ──

    public async Task CreateAsync(string userId, NotificationType type, string title, string body,
        string? referenceId = null, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            ReferenceId = referenceId
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Notification created: {Type} for user {UserId}", type, userId);

        // Pre-fetch mute state + FCM token in the current scope so the
        // fire-and-forget task doesn't touch a disposed DbContext.
        var isMuted = await _context.UserNotificationPreferences
            .AnyAsync(p => p.UserId == userId && p.NotificationType == type && p.IsPushMuted, ct);

        if (isMuted) return;

        var user = await _userRepository.FindByIdAsync(userId);
        if (user?.FcmToken is null) return;

        _ = FirePushToUserAsync(userId, user.FcmToken, title, body, type, referenceId);
    }

    // ── Admin ──

    public async Task<Result> SendToUserAsync(string targetUserId, string title, string body,
        string sentByAdminId, CancellationToken ct)
    {
        var user = await _userRepository.FindByIdAsync(targetUserId, ct);
        if (user is null)
            return Result.Failure(NotificationErrors.UserNotFound);

        await CreateAsync(targetUserId, NotificationType.AdminMessage, title, body, ct: ct);

        _logger.LogInformation("Admin {AdminId} sent notification to user {UserId}", sentByAdminId, targetUserId);
        return Result.Success();
    }

    public async Task<Result<int>> SendToRoleAsync(string roleName, string title, string body,
        string sentByAdminId, CancellationToken ct)
    {
        var usersInRole = await _userRepository.GetUsersInRoleAsync(roleName);
        var activeUserIds = usersInRole
            .Where(u => !u.IsDisabled)
            .Select(u => u.Id)
            .ToList();

        foreach (var userId in activeUserIds)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = NotificationType.AdminMessage,
                Title = title,
                Body = body
            });
        }

        await _context.SaveChangesAsync(ct);

        // Pre-fetch tokens in the current scope — background task must not touch _context.
        var tokens = await _userRepository.Query()
            .Where(u => activeUserIds.Contains(u.Id) && u.FcmToken != null)
            .Select(u => u.FcmToken!)
            .ToListAsync(ct);

        _ = FirePushToTokensAsync(tokens, title, body, NotificationType.AdminMessage, null);

        _logger.LogInformation("Admin {AdminId} sent notification to role {Role}, {Count} users ({TokenCount} push targets)",
            sentByAdminId, roleName, activeUserIds.Count, tokens.Count);

        return Result.Success(activeUserIds.Count);
    }

    public async Task<Result<int>> BroadcastAsync(string title, string body,
        string sentByAdminId, CancellationToken ct)
    {
        var userIds = await _userRepository.Query()
            .Where(u => !u.IsDisabled)
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var userId in userIds)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = NotificationType.AdminMessage,
                Title = title,
                Body = body
            });
        }

        await _context.SaveChangesAsync(ct);

        // Pre-fetch tokens in the current scope — background task must not touch _context.
        var tokens = await _userRepository.Query()
            .Where(u => userIds.Contains(u.Id) && u.FcmToken != null)
            .Select(u => u.FcmToken!)
            .ToListAsync(ct);

        _ = FirePushToTokensAsync(tokens, title, body, NotificationType.AdminMessage, null);

        _logger.LogInformation("Admin {AdminId} broadcast notification to {Count} users ({TokenCount} push targets)",
            sentByAdminId, userIds.Count, tokens.Count);

        return Result.Success(userIds.Count);
    }

    public async Task<PaginatedResult<AdminNotificationHistoryItem>> GetNotificationHistoryAsync(
        AdminNotificationListQuery query, CancellationToken ct)
    {
        var (page, pageSize) = Application.Helpers.PaginationHelper.Normalize(query.Page, query.PageSize);

        var baseQuery = _context.Notifications
            .Include(n => n.User)
            .Where(n => !n.IsDeleted);

        if (!string.IsNullOrEmpty(query.TargetUserId))
            baseQuery = baseQuery.Where(n => n.UserId == query.TargetUserId);

        if (query.Type.HasValue)
            baseQuery = baseQuery.Where(n => n.Type == query.Type.Value);

        if (query.FromDate.HasValue)
            baseQuery = baseQuery.Where(n => n.CreatedAt >= query.FromDate.Value.Date);

        if (query.ToDate.HasValue)
            baseQuery = baseQuery.Where(n => n.CreatedAt < query.ToDate.Value.Date.AddDays(1));

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new AdminNotificationHistoryItem(
                n.Id,
                n.UserId,
                n.User != null ? n.User.Name : "Unknown",
                n.Type,
                n.Title,
                n.Body,
                n.ReferenceId,
                n.IsRead,
                n.CreatedAt))
            .ToListAsync(ct);

        return new PaginatedResult<AdminNotificationHistoryItem>(items, totalCount, page, pageSize);
    }

    // ── Private FCM Helpers (scope-safe: callers pre-fetch tokens, DB cleanup uses a fresh scope) ──

    private async Task FirePushToUserAsync(string userId, string fcmToken, string title, string body,
        NotificationType type, string? referenceId)
    {
        try
        {
            var data = new Dictionary<string, string>
            {
                ["type"] = type.ToString(),
                ["referenceId"] = referenceId ?? ""
            };
            var tokenValid = await _fcm.SendToUserAsync(fcmToken, title, body, data);

            if (!tokenValid)
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                var user = await repo.FindByIdAsync(userId);
                if (user is not null)
                {
                    user.FcmToken = null;
                    await repo.UpdateAsync(user);
                    _logger.LogInformation("Cleared expired FCM token for user {UserId}", userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send FCM push to user {UserId}", userId);
        }
    }

    private async Task FirePushToTokensAsync(List<string> tokens, string title, string body,
        NotificationType type, string? referenceId)
    {
        try
        {
            if (tokens.Count == 0) return;

            var data = new Dictionary<string, string>
            {
                ["type"] = type.ToString(),
                ["referenceId"] = referenceId ?? ""
            };
            var expiredTokens = await _fcm.SendToMultipleAsync(tokens, title, body, data);

            if (expiredTokens.Count > 0)
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                await repo.Query()
                    .Where(u => u.FcmToken != null && expiredTokens.Contains(u.FcmToken))
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.FcmToken, (string?)null));
                _logger.LogInformation("Cleared {Count} expired FCM tokens", expiredTokens.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send FCM multicast to {Count} tokens", tokens.Count);
        }
    }
}
