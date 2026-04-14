using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Contracts.Notifications;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Services;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using static RewardProgram.Application.Tests.TestHelpers.AsyncQueryableExtensions;

namespace RewardProgram.Application.Tests.Services;

public class NotificationServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly IUserRepository _userRepo;
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        _context = TestDbContext.Create();
        _userRepo = Substitute.For<IUserRepository>();
        _sut = new NotificationService(_context, _userRepo, Substitute.For<IFirebaseMessagingService>(), Substitute.For<ILogger<NotificationService>>(), Substitute.For<IServiceScopeFactory>());
    }

    public void Dispose() => _context.Dispose();

    private async Task<Notification> SeedNotification(string userId, bool isRead = false, bool isDeleted = false)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = NotificationType.AdminMessage,
            Title = "Test",
            Body = "Test body",
            IsRead = isRead,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    // ── GetUserNotifications ──

    [Fact]
    public async Task GetNotifications_ShouldReturnOnlyUserNotifications()
    {
        await SeedNotification("u1");
        await SeedNotification("u1");
        await SeedNotification("u2"); // different user

        var result = await _sut.GetUserNotificationsAsync("u1", new NotificationListQuery(), default);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetNotifications_ShouldExcludeDeleted()
    {
        await SeedNotification("u1");
        await SeedNotification("u1", isDeleted: true);

        var result = await _sut.GetUserNotificationsAsync("u1", new NotificationListQuery(), default);

        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetNotifications_ShouldPaginate()
    {
        for (int i = 0; i < 5; i++)
            await SeedNotification("u1");

        var result = await _sut.GetUserNotificationsAsync("u1", new NotificationListQuery(Page: 1, PageSize: 2), default);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(5);
    }

    // ── GetUnreadCount ──

    [Fact]
    public async Task GetUnreadCount_ShouldCountOnlyUnreadNonDeleted()
    {
        await SeedNotification("u1", isRead: false);
        await SeedNotification("u1", isRead: false);
        await SeedNotification("u1", isRead: true);
        await SeedNotification("u1", isRead: false, isDeleted: true);

        var count = await _sut.GetUnreadCountAsync("u1", default);

        count.Should().Be(2);
    }

    // ── MarkAsRead ──

    [Fact]
    public async Task MarkAsRead_NotFound_ShouldFail()
    {
        var result = await _sut.MarkAsReadAsync("bad-id", "u1", default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(NotificationErrors.NotificationNotFound);
    }

    [Fact]
    public async Task MarkAsRead_WrongUser_ShouldFail()
    {
        var notification = await SeedNotification("u1");

        var result = await _sut.MarkAsReadAsync(notification.Id, "u2", default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(NotificationErrors.NotificationNotOwned);
    }

    [Fact]
    public async Task MarkAsRead_Valid_ShouldSetIsReadAndReadAt()
    {
        var notification = await SeedNotification("u1");

        var result = await _sut.MarkAsReadAsync(notification.Id, "u1", default);

        result.IsSuccess.Should().BeTrue();
        var updated = await _context.Notifications.FindAsync(notification.Id);
        updated!.IsRead.Should().BeTrue();
        updated.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAsRead_AlreadyRead_ShouldSucceedIdempotently()
    {
        var notification = await SeedNotification("u1", isRead: true);

        var result = await _sut.MarkAsReadAsync(notification.Id, "u1", default);

        result.IsSuccess.Should().BeTrue();
    }

    // ── MarkAllAsRead ──
    // Note: MarkAllAsReadAsync uses ExecuteUpdateAsync (bulk update) which is not
    // supported by EF InMemory provider. This method is tested via API integration tests.

    // ── RegisterDevice ──

    [Fact]
    public async Task RegisterDevice_UserNotFound_ShouldFail()
    {
        _userRepo.FindByIdAsync("bad", Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.RegisterDeviceAsync("bad", "fcm-token-123", default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(NotificationErrors.UserNotFound);
    }

    [Fact]
    public async Task RegisterDevice_Valid_ShouldUpdateFcmToken()
    {
        var user = new ApplicationUser { Id = "u1", Name = "Test", MobileNumber = "+966500000001" };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.RegisterDeviceAsync("u1", "fcm-token-abc", default);

        result.IsSuccess.Should().BeTrue();
        user.FcmToken.Should().Be("fcm-token-abc");
        await _userRepo.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task RegisterDevice_ShouldTrimToken()
    {
        var user = new ApplicationUser { Id = "u1", Name = "Test", MobileNumber = "+966500000001" };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.RegisterDeviceAsync("u1", "  fcm-token-abc  ", default);

        result.IsSuccess.Should().BeTrue();
        user.FcmToken.Should().Be("fcm-token-abc");
    }

    // ── CreateAsync ──

    [Fact]
    public async Task Create_ShouldAddNotification()
    {
        await _sut.CreateAsync("u1", NotificationType.PointsEarned, "Title", "Body");

        _context.Notifications.Should().HaveCount(1);
        var n = _context.Notifications.First();
        n.UserId.Should().Be("u1");
        n.Type.Should().Be(NotificationType.PointsEarned);
        n.Title.Should().Be("Title");
    }

    // ── SendToUser ──

    [Fact]
    public async Task SendToUser_UserNotFound_ShouldFail()
    {
        _userRepo.FindByIdAsync("bad", Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.SendToUserAsync("bad", "Title", "Body", "admin1", default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(NotificationErrors.UserNotFound);
    }

    [Fact]
    public async Task SendToUser_Valid_ShouldCreateNotification()
    {
        var user = new ApplicationUser { Id = "u1", Name = "Test", MobileNumber = "+966500000001" };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.SendToUserAsync("u1", "Hello", "World", "admin1", default);

        result.IsSuccess.Should().BeTrue();
        _context.Notifications.Should().HaveCount(1);
        _context.Notifications.First().Type.Should().Be(NotificationType.AdminMessage);
    }

    // ── SendToRole ──

    [Fact]
    public async Task SendToRole_ShouldCreateNotificationForActiveUsersOnly()
    {
        var users = new List<ApplicationUser>
        {
            new() { Id = "u1", Name = "Active", MobileNumber = "+966500000001", IsDisabled = false },
            new() { Id = "u2", Name = "Disabled", MobileNumber = "+966500000002", IsDisabled = true },
            new() { Id = "u3", Name = "Active2", MobileNumber = "+966500000003", IsDisabled = false }
        };
        _userRepo.GetUsersInRoleAsync("Seller").Returns(users);

        var result = await _sut.SendToRoleAsync("Seller", "Title", "Body", "admin1", default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2); // Only active users
        _context.Notifications.Should().HaveCount(2);
    }

    // ── Broadcast ──

    [Fact]
    public async Task Broadcast_ShouldCreateForAllActiveUsers()
    {
        var users = new List<ApplicationUser>
        {
            new() { Id = "u1", Name = "User1", MobileNumber = "+966500000001", IsDisabled = false },
            new() { Id = "u2", Name = "User2", MobileNumber = "+966500000002", IsDisabled = false }
        };
        _userRepo.Query().Returns(users.AsAsyncQueryable());

        var result = await _sut.BroadcastAsync("Alert", "Body", "admin1", default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        _context.Notifications.Should().HaveCount(2);
    }
}
