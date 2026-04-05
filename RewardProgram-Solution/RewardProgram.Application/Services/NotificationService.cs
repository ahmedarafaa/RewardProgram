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
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _logger = logger;
    }

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
    }

    public async Task<Result> SendToUserAsync(string targetUserId, string title, string body,
        string sentByAdminId, CancellationToken ct)
    {
        var user = await _userRepository.FindByIdAsync(targetUserId, ct);
        if (user is null)
            return Result.Failure(NotificationErrors.NotificationNotFound);

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

        _logger.LogInformation("Admin {AdminId} broadcast notification to {Count} users",
            sentByAdminId, userIds.Count);

        return Result.Success(userIds.Count);
    }
}
