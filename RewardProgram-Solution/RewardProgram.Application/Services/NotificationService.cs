using Microsoft.EntityFrameworkCore;
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

    public NotificationService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        IFirebaseMessagingService fcm,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _fcm = fcm;
        _logger = logger;
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

        // Send FCM push (fire-and-forget — don't block the caller)
        _ = SendPushToUserAsync(userId, title, body, type, referenceId);
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

        // Send FCM push to all users in role (fire-and-forget)
        _ = SendPushToUsersAsync(activeUserIds, title, body);

        _logger.LogInformation("Admin {AdminId} sent notification to role {Role}, {Count} users",
            sentByAdminId, roleName, activeUserIds.Count);

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

        // Send FCM push to all active users (fire-and-forget)
        _ = SendPushToUsersAsync(userIds, title, body);

        _logger.LogInformation("Admin {AdminId} broadcast notification to {Count} users",
            sentByAdminId, userIds.Count);

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

    // ── Private FCM Helpers ──

    private async Task SendPushToUserAsync(string userId, string title, string body,
        NotificationType type, string? referenceId)
    {
        try
        {
            // Check if user has muted this notification type
            var isMuted = await _context.UserNotificationPreferences
                .AnyAsync(p => p.UserId == userId && p.NotificationType == type && p.IsPushMuted);

            if (isMuted) return;

            var user = await _userRepository.FindByIdAsync(userId);
            if (user?.FcmToken is not null)
            {
                var data = new Dictionary<string, string>
                {
                    ["type"] = type.ToString(),
                    ["referenceId"] = referenceId ?? ""
                };
                var tokenValid = await _fcm.SendToUserAsync(user.FcmToken, title, body, data);

                // Clean up expired/unregistered token
                if (!tokenValid)
                {
                    user.FcmToken = null;
                    await _userRepository.UpdateAsync(user);
                    _logger.LogInformation("Cleared expired FCM token for user {UserId}", userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send FCM push to user {UserId}", userId);
        }
    }

    private async Task SendPushToUsersAsync(IReadOnlyList<string> userIds, string title, string body,
        NotificationType type = NotificationType.AdminMessage, string? referenceId = null)
    {
        try
        {
            var tokens = await _userRepository.Query()
                .Where(u => userIds.Contains(u.Id) && u.FcmToken != null)
                .Select(u => u.FcmToken!)
                .ToListAsync();

            if (tokens.Count > 0)
            {
                var data = new Dictionary<string, string>
                {
                    ["type"] = type.ToString(),
                    ["referenceId"] = referenceId ?? ""
                };
                var expiredTokens = await _fcm.SendToMultipleAsync(tokens, title, body, data);

                // Clean up expired/unregistered tokens
                if (expiredTokens.Count > 0)
                {
                    await _userRepository.Query()
                        .Where(u => u.FcmToken != null && expiredTokens.Contains(u.FcmToken))
                        .ExecuteUpdateAsync(s => s.SetProperty(u => u.FcmToken, (string?)null));

                    _logger.LogInformation("Cleared {Count} expired FCM tokens", expiredTokens.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send FCM push to {Count} users", userIds.Count);
        }
    }
}
