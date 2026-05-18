using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Contracts.Redemption;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Application.Services;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Tests.Services;

public class RedemptionServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly IUserRepository _userRepo;
    private readonly RedemptionService _sut;

    public RedemptionServiceTests()
    {
        _context = TestDbContext.Create();
        _userRepo = Substitute.For<IUserRepository>();
        _sut = new RedemptionService(
            _context, _userRepo,
            Substitute.For<ITwilioService>(),
            Substitute.For<INotificationService>(),
            Substitute.For<ILogger<RedemptionService>>());
    }

    public void Dispose() => _context.Dispose();

    #region Helpers

    private ApplicationUser CreateApprovedUser(string id = "user-1")
    {
        var user = new ApplicationUser
        {
            Id = id,
            Name = "Test User",
            MobileNumber = "+966500000001",
            UserName = "+966500000001",
            RegistrationStatus = RegistrationStatus.Approved,
            IsDisabled = false,
            NationalAddress = new NationalAddress { CityId = "city-1" }
        };
        _userRepo.FindByIdAsync(id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private async Task<Wallet> SeedWalletWithEarnedPoints(
        string userId, decimal balance, decimal sarRate = 10m, int transactionCount = 1)
    {
        var wallet = new Wallet
        {
            UserId = userId,
            Balance = balance,
            SarBalance = balance / sarRate
        };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        var pointsPerTx = balance / transactionCount;
        for (var i = 0; i < transactionCount; i++)
        {
            _context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                Amount = pointsPerTx,
                Type = WalletTransactionType.Earned,
                SarRate = sarRate,
                SarAmount = pointsPerTx / sarRate,
                RemainingAmount = pointsPerTx,
                Description = $"Earned tx {i + 1}",
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            });
        }

        await _context.SaveChangesAsync();
        return wallet;
    }

    private async Task SeedCity(string cityId = "city-1", string? salesManId = "sm-1")
    {
        var region = new Region
        {
            Id = "region-1",
            NameAr = "منطقة",
            NameEn = "Region",
            IsActive = true
        };
        _context.Regions.Add(region);

        _context.Cities.Add(new City
        {
            Id = cityId,
            NameAr = "مدينة",
            NameEn = "City",
            RegionId = "region-1",
            IsActive = true,
            ApprovalSalesManId = salesManId
        });
        await _context.SaveChangesAsync();
    }

    private async Task SeedRewardSettings(decimal sarRate = 10m)
    {
        _context.RewardSettings.Add(new RewardSettings { PointsToSarRate = sarRate });
        await _context.SaveChangesAsync();
    }

    #endregion

    // ── CreateRequest: Validation ──

    [Fact]
    public async Task CreateRequest_UserNotFound_ShouldFail()
    {
        _userRepo.FindByIdAsync("bad", Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.CreateRequestAsync(
            new CreateRedemptionRequest(RedemptionMethod.BankTransfer, 1000, null, null, null, null, null), "bad");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RedemptionErrors.UserNotApproved);
    }

    [Fact]
    public async Task CreateRequest_DisabledUser_ShouldFail()
    {
        var user = CreateApprovedUser();
        user.IsDisabled = true;

        var result = await _sut.CreateRequestAsync(
            new CreateRedemptionRequest(RedemptionMethod.BankTransfer, 1000, null, null, null, null, null), user.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RedemptionErrors.UserNotApproved);
    }

    [Fact]
    public async Task CreateRequest_BelowMinimum_ShouldFail()
    {
        CreateApprovedUser();

        var result = await _sut.CreateRequestAsync(
            new CreateRedemptionRequest(RedemptionMethod.BankTransfer, 999, null, null, null, null, null), "user-1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RedemptionErrors.BelowMinimum);
    }

    [Fact]
    public async Task CreateRequest_AlreadyHasPending_ShouldFail()
    {
        CreateApprovedUser();
        await SeedWalletWithEarnedPoints("user-1", 2000);
        await SeedRewardSettings();
        await SeedCity();

        // Create a pending request
        _context.RedemptionRequests.Add(new RedemptionRequest
        {
            UserId = "user-1",
            Status = RedemptionRequestStatus.PendingSalesMan,
            PointsAmount = 1000,
            SarRate = 10,
            SarAmount = 100,
            Method = RedemptionMethod.BankTransfer
        });
        await _context.SaveChangesAsync();

        var result = await _sut.CreateRequestAsync(
            new CreateRedemptionRequest(RedemptionMethod.BankTransfer, 1000, null, null, null, null, null), "user-1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RedemptionErrors.AlreadyHasPendingRequest);
    }

    [Fact]
    public async Task CreateRequest_NotIntegerSar_ShouldFail()
    {
        CreateApprovedUser();
        await SeedWalletWithEarnedPoints("user-1", 2000);
        await SeedRewardSettings(sarRate: 3m); // 1005 / 3 = 335.0 OK, but 1001/3 = 333.67 not integer

        var result = await _sut.CreateRequestAsync(
            new CreateRedemptionRequest(RedemptionMethod.BankTransfer, 1001, null, null, null, null, null), "user-1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RedemptionErrors.NotIntegerSar);
    }

    [Fact]
    public async Task CreateRequest_InsufficientBalance_ShouldFail()
    {
        CreateApprovedUser();
        await SeedWalletWithEarnedPoints("user-1", 500);
        await SeedRewardSettings();

        var result = await _sut.CreateRequestAsync(
            new CreateRedemptionRequest(RedemptionMethod.BankTransfer, 1000, null, null, null, null, null), "user-1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RedemptionErrors.InsufficientBalance);
    }

    // ── CreateRequest: Success ──

    [Fact]
    public async Task CreateRequest_Valid_ShouldCreateAndHoldPoints()
    {
        CreateApprovedUser();
        var wallet = await SeedWalletWithEarnedPoints("user-1", 2000);
        await SeedRewardSettings(10m);
        await SeedCity();

        var result = await _sut.CreateRequestAsync(
            new CreateRedemptionRequest(RedemptionMethod.BankTransfer, 1000, "SA1234", "1234567891234", "الرياض", "RJHISARI", "Holder"), "user-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.PointsAmount.Should().Be(1000);
        result.Value.SarAmount.Should().Be(100);

        // Wallet should have held balance
        var updatedWallet = await _context.Wallets.FirstAsync(w => w.UserId == "user-1");
        updatedWallet.HeldBalance.Should().Be(1000);
        updatedWallet.HeldSarBalance.Should().Be(100);
        // Total balance unchanged
        updatedWallet.Balance.Should().Be(2000);
    }

    [Fact]
    public async Task CreateRequest_WithSalesMan_ShouldBePendingSalesMan()
    {
        CreateApprovedUser();
        await SeedWalletWithEarnedPoints("user-1", 2000);
        await SeedRewardSettings();
        await SeedCity(salesManId: "sm-1");

        var result = await _sut.CreateRequestAsync(
            new CreateRedemptionRequest(RedemptionMethod.BankTransfer, 1000, null, null, null, null, null), "user-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(RedemptionRequestStatus.PendingSalesMan);
    }

    [Fact]
    public async Task CreateRequest_WithoutSalesMan_ShouldBePendingZoneManager()
    {
        CreateApprovedUser();
        await SeedWalletWithEarnedPoints("user-1", 2000);
        await SeedRewardSettings();
        await SeedCity(salesManId: null);

        var result = await _sut.CreateRequestAsync(
            new CreateRedemptionRequest(RedemptionMethod.BankTransfer, 1000, null, null, null, null, null), "user-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(RedemptionRequestStatus.PendingZoneManager);
    }

    // ── ExpireOldPoints ──

    [Fact]
    public async Task ExpireOldPoints_ShouldExpireTransactionsOlderThan15Months()
    {
        var wallet = new Wallet { UserId = "user-1", Balance = 300, SarBalance = 30 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        // Old transaction (16 months ago) — should expire
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = 200,
            Type = WalletTransactionType.Earned,
            SarRate = 10m,
            SarAmount = 20,
            RemainingAmount = 200,
            CreatedAt = DateTime.UtcNow.AddMonths(-16)
        });

        // Recent transaction (1 month ago) — should NOT expire
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = 100,
            Type = WalletTransactionType.Earned,
            SarRate = 10m,
            SarAmount = 10,
            RemainingAmount = 100,
            CreatedAt = DateTime.UtcNow.AddMonths(-1)
        });
        await _context.SaveChangesAsync();

        await _sut.ExpireOldPointsAsync(wallet);
        await _context.SaveChangesAsync(); // ExpireOldPointsAsync doesn't save — caller owns the save

        wallet.Balance.Should().Be(100); // 300 - 200 expired
        wallet.SarBalance.Should().Be(10); // 30 - 20 expired

        var expiredTxs = _context.WalletTransactions
            .Where(t => t.Type == WalletTransactionType.Expired).ToList();
        expiredTxs.Should().HaveCount(1);
        expiredTxs[0].Amount.Should().Be(-200);
    }

    [Fact]
    public async Task ExpireOldPoints_ShouldNotExpireAlreadyConsumedPoints()
    {
        var wallet = new Wallet { UserId = "user-1", Balance = 100, SarBalance = 10 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        // Old transaction but RemainingAmount = 0 (already redeemed)
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = 200,
            Type = WalletTransactionType.Earned,
            SarRate = 10m,
            SarAmount = 20,
            RemainingAmount = 0, // fully consumed
            CreatedAt = DateTime.UtcNow.AddMonths(-16)
        });
        await _context.SaveChangesAsync();

        await _sut.ExpireOldPointsAsync(wallet);

        wallet.Balance.Should().Be(100); // unchanged
        _context.WalletTransactions.Count(t => t.Type == WalletTransactionType.Expired).Should().Be(0);
    }

    [Fact]
    public async Task ExpireOldPoints_PartiallyConsumed_ShouldExpireOnlyRemaining()
    {
        var wallet = new Wallet { UserId = "user-1", Balance = 150, SarBalance = 15 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        // Old transaction, partially consumed (50 of 200 remaining)
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = 200,
            Type = WalletTransactionType.Earned,
            SarRate = 10m,
            SarAmount = 20,
            RemainingAmount = 50, // partially consumed
            CreatedAt = DateTime.UtcNow.AddMonths(-16)
        });
        await _context.SaveChangesAsync();

        await _sut.ExpireOldPointsAsync(wallet);

        wallet.Balance.Should().Be(100); // 150 - 50 expired
        wallet.SarBalance.Should().Be(10); // 15 - 5 expired
    }

    [Fact]
    public async Task ExpireOldPoints_MultipleOldTransactions_ShouldExpireAll()
    {
        var wallet = new Wallet { UserId = "user-1", Balance = 500, SarBalance = 50 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = 200,
            Type = WalletTransactionType.Earned,
            SarRate = 10m,
            SarAmount = 20,
            RemainingAmount = 200,
            CreatedAt = DateTime.UtcNow.AddMonths(-18)
        });

        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = 100,
            Type = WalletTransactionType.Earned,
            SarRate = 10m,
            SarAmount = 10,
            RemainingAmount = 100,
            CreatedAt = DateTime.UtcNow.AddMonths(-16)
        });
        await _context.SaveChangesAsync();

        await _sut.ExpireOldPointsAsync(wallet);
        await _context.SaveChangesAsync(); // ExpireOldPointsAsync doesn't save — caller owns the save

        wallet.Balance.Should().Be(200); // 500 - 300 expired
        _context.WalletTransactions.Count(t => t.Type == WalletTransactionType.Expired).Should().Be(2);
    }

    [Fact]
    public async Task ExpireOldPoints_MixedSarRates_ShouldUsePerTransactionRate()
    {
        // This test documents the current behavior where SarBalance deduction
        // uses the FIRST transaction's rate for the total — which is a known issue
        // when rates differ across transactions.
        var wallet = new Wallet { UserId = "user-1", Balance = 300, SarBalance = 35 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        // Tx1: rate=10 → 200pts = 20 SAR
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = 200,
            Type = WalletTransactionType.Earned,
            SarRate = 10m,
            SarAmount = 20,
            RemainingAmount = 200,
            CreatedAt = DateTime.UtcNow.AddMonths(-18)
        });

        // Tx2: rate=20 → 100pts = 5 SAR
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = 100,
            Type = WalletTransactionType.Earned,
            SarRate = 20m,
            SarAmount = 5,
            RemainingAmount = 100,
            CreatedAt = DateTime.UtcNow.AddMonths(-16)
        });
        await _context.SaveChangesAsync();

        await _sut.ExpireOldPointsAsync(wallet);

        wallet.Balance.Should().Be(0); // 300 - 300 expired

        // Each tx uses its own SAR rate: 200/10 + 100/20 = 20 + 5 = 25
        wallet.SarBalance.Should().Be(10); // 35 - 25 = 10
    }

    // ── GetAvailableBalance ──

    [Fact]
    public async Task GetAvailableBalance_NoWallet_ShouldReturnZeros()
    {
        var result = await _sut.GetAvailableBalanceAsync("no-wallet-user");

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalBalance.Should().Be(0);
        result.Value.AvailableBalance.Should().Be(0);
    }

    [Fact]
    public async Task GetAvailableBalance_WithHeldPoints_ShouldSubtractHeld()
    {
        await SeedRewardSettings(10m);
        var wallet = new Wallet
        {
            UserId = "user-1",
            Balance = 2000,
            SarBalance = 200,
            HeldBalance = 500,
            HeldSarBalance = 50
        };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        var result = await _sut.GetAvailableBalanceAsync("user-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalBalance.Should().Be(2000);
        result.Value.HeldBalance.Should().Be(500);
        result.Value.AvailableBalance.Should().Be(1500);
    }
}
