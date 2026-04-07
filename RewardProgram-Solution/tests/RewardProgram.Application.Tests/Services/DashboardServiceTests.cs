using FluentAssertions;
using NSubstitute;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Services;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Tests.Services;

public class DashboardServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly IUserRepository _userRepo;
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _context = TestDbContext.Create();
        _userRepo = Substitute.For<IUserRepository>();
        _sut = new DashboardService(_context, _userRepo);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetDashboard_UserNotFound_ShouldFail()
    {
        _userRepo.FindByIdAsync("bad", Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.GetDashboardAsync("bad");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
    }

    [Fact]
    public async Task GetDashboard_NoWallet_ShouldReturnZeroBalances()
    {
        var user = new ApplicationUser { Id = "u1", Name = "Test", MobileNumber = "+966500000001" };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.GetDashboardAsync("u1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Points.Should().Be(0);
        result.Value.SarBalance.Should().Be(0);
        result.Value.RecentTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboard_WithWallet_ShouldReturnCorrectBalances()
    {
        var user = new ApplicationUser { Id = "u1", Name = "Seller", MobileNumber = "+966500000001" };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var wallet = new Wallet { UserId = "u1", Balance = 500, SarBalance = 50 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        var result = await _sut.GetDashboardAsync("u1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Points.Should().Be(500);
        result.Value.SarBalance.Should().Be(50);
        result.Value.UserName.Should().Be("Seller");
    }

    [Fact]
    public async Task GetDashboard_ShouldCalculateRedemptionEligibility()
    {
        var user = new ApplicationUser { Id = "u1", Name = "Test", MobileNumber = "+966500000001" };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        // Set minimum redemption to 1000
        _context.RewardSettings.Add(new RewardSettings { MinimumRedemptionPoints = 1000 });
        var wallet = new Wallet { UserId = "u1", Balance = 600, SarBalance = 60 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        var result = await _sut.GetDashboardAsync("u1");

        result.IsSuccess.Should().BeTrue();
        result.Value.CanRedeem.Should().BeFalse();
        result.Value.PointsToRedeem.Should().Be(400); // 1000 - 600
        result.Value.MinimumRedemptionPoints.Should().Be(1000);
    }

    [Fact]
    public async Task GetDashboard_AboveMinimum_ShouldBeEligible()
    {
        var user = new ApplicationUser { Id = "u1", Name = "Test", MobileNumber = "+966500000001" };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        _context.RewardSettings.Add(new RewardSettings { MinimumRedemptionPoints = 500 });
        var wallet = new Wallet { UserId = "u1", Balance = 1000, SarBalance = 100 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        var result = await _sut.GetDashboardAsync("u1");

        result.Value.CanRedeem.Should().BeTrue();
        result.Value.PointsToRedeem.Should().Be(0);
    }

    [Fact]
    public async Task GetDashboard_ShouldReturnRecentTransactionsLimitedTo10()
    {
        var user = new ApplicationUser { Id = "u1", Name = "Test", MobileNumber = "+966500000001" };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var wallet = new Wallet { UserId = "u1", Balance = 1500, SarBalance = 150 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        // Add 15 transactions
        for (int i = 0; i < 15; i++)
        {
            _context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                Amount = 100,
                Type = WalletTransactionType.Earned,
                SarRate = 10, SarAmount = 10,
                RemainingAmount = 100,
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            });
        }
        await _context.SaveChangesAsync();

        var result = await _sut.GetDashboardAsync("u1");

        result.Value.RecentTransactions.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetDashboard_NoRewardSettings_ShouldUseDefault1000()
    {
        var user = new ApplicationUser { Id = "u1", Name = "Test", MobileNumber = "+966500000001" };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.GetDashboardAsync("u1");

        result.Value.MinimumRedemptionPoints.Should().Be(1000);
    }
}
