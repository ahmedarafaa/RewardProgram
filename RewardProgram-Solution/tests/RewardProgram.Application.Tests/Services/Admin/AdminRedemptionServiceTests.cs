using FluentAssertions;
using RewardProgram.Application.Contracts.Admin.Redemptions;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Services.Admin;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Tests.Services.Admin;

public class AdminRedemptionServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly AdminRedemptionService _sut;

    public AdminRedemptionServiceTests()
    {
        _context = TestDbContext.Create();
        _sut = new AdminRedemptionService(_context);
    }

    public void Dispose() => _context.Dispose();

    private async Task<(ApplicationUser user, RedemptionRequest request)> SeedRedemptionRequest(
        RedemptionRequestStatus status = RedemptionRequestStatus.PendingSalesMan,
        RedemptionMethod method = RedemptionMethod.Cash,
        string userId = "u1")
    {
        var user = new ApplicationUser
        {
            Id = userId, Name = "Test User", MobileNumber = "+966500000001",
            UserName = "+966500000001"
        };
        _context.Set<ApplicationUser>().Add(user);

        var request = new RedemptionRequest
        {
            UserId = userId,
            Status = status,
            Method = method,
            PointsAmount = 1000,
            SarRate = 10,
            SarAmount = 100,
            CreatedAt = DateTime.UtcNow
        };
        _context.RedemptionRequests.Add(request);
        await _context.SaveChangesAsync();

        return (user, request);
    }

    // ── GetAll ──

    [Fact]
    public async Task GetAll_NoFilters_ShouldReturnAllRequests()
    {
        await SeedRedemptionRequest(userId: "u1");
        await SeedRedemptionRequest(status: RedemptionRequestStatus.Completed, userId: "u2");

        var result = await _sut.GetAllAsync(new AdminRedemptionListQuery(null, null, null, null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAll_FilterByStatus_ShouldReturnOnlyMatching()
    {
        await SeedRedemptionRequest(status: RedemptionRequestStatus.PendingSalesMan, userId: "u1");
        await SeedRedemptionRequest(status: RedemptionRequestStatus.Completed, userId: "u2");

        var result = await _sut.GetAllAsync(
            new AdminRedemptionListQuery(null, null, RedemptionRequestStatus.Completed, null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.First().Status.Should().Be(RedemptionRequestStatus.Completed);
    }

    [Fact]
    public async Task GetAll_FilterByMethod_ShouldReturnOnlyMatching()
    {
        await SeedRedemptionRequest(method: RedemptionMethod.Cash, userId: "u1");
        await SeedRedemptionRequest(method: RedemptionMethod.BankTransfer, userId: "u2");

        var result = await _sut.GetAllAsync(
            new AdminRedemptionListQuery(null, RedemptionMethod.BankTransfer, null, null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAll_FilterByUserId_ShouldReturnOnlyThatUser()
    {
        await SeedRedemptionRequest(userId: "u1");
        await SeedRedemptionRequest(userId: "u2");

        var result = await _sut.GetAllAsync(
            new AdminRedemptionListQuery("u1", null, null, null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAll_FilterByDateRange_ShouldReturnOnlyInRange()
    {
        var (_, req1) = await SeedRedemptionRequest(userId: "u1");
        req1.CreatedAt = new DateTime(2026, 1, 15);

        var (_, req2) = await SeedRedemptionRequest(userId: "u2");
        req2.CreatedAt = new DateTime(2026, 3, 15);

        await _context.SaveChangesAsync();

        var result = await _sut.GetAllAsync(
            new AdminRedemptionListQuery(null, null, null, new DateTime(2026, 3, 1), new DateTime(2026, 4, 1)));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAll_ShouldPaginate()
    {
        for (int i = 0; i < 5; i++)
            await SeedRedemptionRequest(userId: $"u{i}");

        var result = await _sut.GetAllAsync(
            new AdminRedemptionListQuery(null, null, null, null, null, Page: 1, PageSize: 2));

        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(5);
    }

    // ── GetById ──

    [Fact]
    public async Task GetById_NotFound_ShouldFail()
    {
        var result = await _sut.GetByIdAsync("bad-id");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RedemptionErrors.RequestNotFound);
    }

    [Fact]
    public async Task GetById_Valid_ShouldReturnFullDetails()
    {
        var (user, request) = await SeedRedemptionRequest();

        var result = await _sut.GetByIdAsync(request.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(request.Id);
        result.Value.UserFullName.Should().Be("Test User");
        result.Value.PointsAmount.Should().Be(1000);
        result.Value.SarAmount.Should().Be(100);
    }

    [Fact]
    public async Task GetById_WithApprovals_ShouldReturnApprovalHistory()
    {
        var (user, request) = await SeedRedemptionRequest();

        var approver = new ApplicationUser
        {
            Id = "approver1", Name = "Sales Man", MobileNumber = "+966500000002",
            UserName = "+966500000002"
        };
        _context.Set<ApplicationUser>().Add(approver);

        _context.RedemptionApprovals.Add(new RedemptionApproval
        {
            RedemptionRequestId = request.Id,
            ApproverId = "approver1",
            Action = ApprovalAction.Approved,
            FromStatus = RedemptionRequestStatus.PendingSalesMan,
            ToStatus = RedemptionRequestStatus.PendingZoneManager,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(request.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Approvals.Should().HaveCount(1);
        result.Value.Approvals.First().ApproverName.Should().Be("Sales Man");
    }
}
