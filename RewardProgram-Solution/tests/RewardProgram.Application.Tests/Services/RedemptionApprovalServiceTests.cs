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

public class RedemptionApprovalServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly IUserRepository _userRepo;
    private readonly RedemptionApprovalService _sut;

    public RedemptionApprovalServiceTests()
    {
        _context = TestDbContext.Create();
        _userRepo = Substitute.For<IUserRepository>();
        _sut = new RedemptionApprovalService(
            _context, _userRepo,
            Substitute.For<INotificationService>(),
            Substitute.For<ILogger<RedemptionApprovalService>>());
    }

    public void Dispose() => _context.Dispose();

    #region Helpers

    private void SetupApprover(string id, string role)
    {
        var user = new ApplicationUser
        {
            Id = id, Name = "Approver", MobileNumber = "+966500000099", UserName = "+966500000099",
            RegistrationStatus = RegistrationStatus.Approved
        };
        _userRepo.FindByIdAsync(id, Arg.Any<CancellationToken>()).Returns(user);
        _userRepo.GetRolesAsync(user).Returns(new List<string> { role });
    }

    private async Task SeedGeographyAsync(string salesManId = "sm-1", string zoneManagerId = "zm-1")
    {
        if (!await _context.Regions.AnyAsync(r => r.Id == "region-1"))
        {
            _context.Regions.Add(new Region
            {
                Id = "region-1", NameAr = "منطقة", NameEn = "Region",
                IsActive = true, ZoneManagerId = zoneManagerId
            });
            _context.Cities.Add(new City
            {
                Id = "city-1", NameAr = "مدينة", NameEn = "City",
                RegionId = "region-1", IsActive = true, ApprovalSalesManId = salesManId
            });
            await _context.SaveChangesAsync();
        }
    }

    private async Task<(Wallet wallet, RedemptionRequest request)> SeedPendingRedemption(
        string userId, RedemptionRequestStatus status, RedemptionMethod method = RedemptionMethod.BankTransfer,
        decimal points = 1000, decimal sarRate = 10m, int earnedTxCount = 3)
    {
        // Seed geography for approver authorization
        await SeedGeographyAsync();

        // Create user entity for navigation
        var userEntity = new ApplicationUser
        {
            Id = userId, Name = "Requester", MobileNumber = "+966500000001", UserName = "+966500000001",
            RegistrationStatus = RegistrationStatus.Approved,
            NationalAddress = new NationalAddress { CityId = "city-1" }
        };
        _context.Add(userEntity);

        var sarAmount = points / sarRate;

        var wallet = new Wallet
        {
            UserId = userId,
            Balance = points + 500, // extra balance beyond held
            SarBalance = (points + 500) / sarRate,
            HeldBalance = points,
            HeldSarBalance = sarAmount
        };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        // Create earned transactions (FIFO targets)
        var pointsPerTx = points / earnedTxCount;
        for (var i = 0; i < earnedTxCount; i++)
        {
            _context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                Amount = pointsPerTx,
                Type = WalletTransactionType.Earned,
                SarRate = sarRate,
                SarAmount = pointsPerTx / sarRate,
                RemainingAmount = pointsPerTx,
                Description = $"Earned batch {i + 1}",
                // Oldest first for FIFO
                CreatedAt = DateTime.UtcNow.AddDays(-(earnedTxCount - i))
            });
        }
        await _context.SaveChangesAsync();

        var request = new RedemptionRequest
        {
            UserId = userId,
            Method = method,
            Status = status,
            PointsAmount = points,
            SarRate = sarRate,
            SarAmount = sarAmount
        };
        _context.RedemptionRequests.Add(request);
        await _context.SaveChangesAsync();

        return (wallet, request);
    }

    #endregion

    // ── Approve: Validation ──

    [Fact]
    public async Task Approve_RequestNotFound_ShouldFail()
    {
        var result = await _sut.ApproveAsync(new ApproveRedemptionRequest("non-existent"), "admin");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RedemptionErrors.RequestNotFound);
    }

    [Fact]
    public async Task Approve_AlreadyCompleted_ShouldFail()
    {
        var (_, request) = await SeedPendingRedemption("user-1", RedemptionRequestStatus.Completed);
        SetupApprover("admin", UserRoles.SystemAdmin);

        var result = await _sut.ApproveAsync(new ApproveRedemptionRequest(request.Id), "admin");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RedemptionErrors.NotPendingApproval);
    }

    [Fact]
    public async Task Approve_WrongRole_ShouldFail()
    {
        var (_, request) = await SeedPendingRedemption("user-1", RedemptionRequestStatus.PendingSalesMan);
        SetupApprover("zm-1", UserRoles.ZoneManager); // ZM can't approve PendingSalesMan

        var result = await _sut.ApproveAsync(new ApproveRedemptionRequest(request.Id), "zm-1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RedemptionErrors.NotAuthorizedToApprove);
    }

    // ── Approve: Status Transitions ──

    [Fact]
    public async Task Approve_PendingSalesMan_ShouldAdvanceToPendingZoneManager()
    {
        var (_, request) = await SeedPendingRedemption("user-1", RedemptionRequestStatus.PendingSalesMan);
        SetupApprover("sm-1", UserRoles.SalesMan);

        var result = await _sut.ApproveAsync(new ApproveRedemptionRequest(request.Id), "sm-1");

        result.IsSuccess.Should().BeTrue();
        var updated = await _context.RedemptionRequests.FindAsync(request.Id);
        updated!.Status.Should().Be(RedemptionRequestStatus.PendingZoneManager);

        // Approval record created
        var approval = await _context.RedemptionApprovals.FirstAsync();
        approval.FromStatus.Should().Be(RedemptionRequestStatus.PendingSalesMan);
        approval.ToStatus.Should().Be(RedemptionRequestStatus.PendingZoneManager);
    }

    [Fact]
    public async Task Approve_PendingZoneManager_ShouldAdvanceToPendingAdmin()
    {
        var (_, request) = await SeedPendingRedemption("user-1", RedemptionRequestStatus.PendingZoneManager);
        SetupApprover("zm-1", UserRoles.ZoneManager);

        var result = await _sut.ApproveAsync(new ApproveRedemptionRequest(request.Id), "zm-1");

        result.IsSuccess.Should().BeTrue();
        var updated = await _context.RedemptionRequests.FindAsync(request.Id);
        updated!.Status.Should().Be(RedemptionRequestStatus.PendingAdmin);
    }

    // ── Approve: Bank Transfer Completion ──

    [Fact]
    public async Task Approve_PendingAdmin_BankTransfer_ShouldCompleteImmediately()
    {
        var (wallet, request) = await SeedPendingRedemption(
            "user-1", RedemptionRequestStatus.PendingAdmin, RedemptionMethod.BankTransfer,
            points: 1000, sarRate: 10m, earnedTxCount: 3);
        SetupApprover("admin", UserRoles.SystemAdmin);

        var result = await _sut.ApproveAsync(new ApproveRedemptionRequest(request.Id), "admin");

        result.IsSuccess.Should().BeTrue();

        var updated = await _context.RedemptionRequests.FindAsync(request.Id);
        updated!.Status.Should().Be(RedemptionRequestStatus.Completed);

        // Wallet balance deducted
        var updatedWallet = await _context.Wallets.FirstAsync(w => w.UserId == "user-1");
        updatedWallet.Balance.Should().Be(500); // 1500 - 1000
        updatedWallet.HeldBalance.Should().Be(0); // released
        updatedWallet.HeldSarBalance.Should().Be(0);

        // Redeemed transaction created
        var redeemTx = await _context.WalletTransactions
            .FirstAsync(t => t.Type == WalletTransactionType.Redeemed);
        redeemTx.Amount.Should().Be(-1000);
        redeemTx.SarAmount.Should().Be(-100);
    }

    // ── Approve: Cash → OTP Generation ──

    [Fact]
    public async Task Approve_PendingAdmin_Cash_ShouldGenerateOtpNotComplete()
    {
        var (_, request) = await SeedPendingRedemption(
            "user-1", RedemptionRequestStatus.PendingAdmin, RedemptionMethod.Cash);
        SetupApprover("admin", UserRoles.SystemAdmin);

        var result = await _sut.ApproveAsync(new ApproveRedemptionRequest(request.Id), "admin");

        result.IsSuccess.Should().BeTrue();

        var updated = await _context.RedemptionRequests.FindAsync(request.Id);
        updated!.Status.Should().Be(RedemptionRequestStatus.AdminApproved); // NOT Completed
        updated.CashOtpHash.Should().NotBeNullOrEmpty();
        updated.CashOtpHash!.Length.Should().Be(64); // SHA256 hex
        updated.CashOtpExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(13));
    }

    // ── FIFO Point Consumption ──

    [Fact]
    public async Task CompleteRedemption_ShouldConsumeFIFO_OldestFirst()
    {
        // 3 earned transactions: 333.33 each, oldest first
        var (wallet, request) = await SeedPendingRedemption(
            "user-1", RedemptionRequestStatus.PendingAdmin, RedemptionMethod.BankTransfer,
            points: 999, sarRate: 10m, earnedTxCount: 3);
        SetupApprover("admin", UserRoles.SystemAdmin);

        var result = await _sut.ApproveAsync(new ApproveRedemptionRequest(request.Id), "admin");
        result.IsSuccess.Should().BeTrue();

        // Verify FIFO: oldest transactions consumed first
        var earnedTxs = await _context.WalletTransactions
            .Where(t => t.WalletId == wallet.Id && t.Type == WalletTransactionType.Earned)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        // All 3 transactions should be fully consumed (333 each, total = 999)
        earnedTxs.Should().HaveCount(3);
        earnedTxs[0].RemainingAmount.Should().Be(0); // oldest fully consumed
        earnedTxs[1].RemainingAmount.Should().Be(0); // second fully consumed
        earnedTxs[2].RemainingAmount.Should().Be(0); // third fully consumed
    }

    [Fact]
    public async Task CompleteRedemption_PartialConsumption_ShouldLeaveRemainderInNewest()
    {
        var userId = "user-1";
        var userEntity = new ApplicationUser
        {
            Id = userId, Name = "Requester", MobileNumber = "+966500000001", UserName = "+966500000001",
            RegistrationStatus = RegistrationStatus.Approved,
            NationalAddress = new NationalAddress { CityId = "city-1" }
        };
        _context.Add(userEntity);

        var wallet = new Wallet
        {
            UserId = userId, Balance = 1500, SarBalance = 150,
            HeldBalance = 500, HeldSarBalance = 50
        };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        // 3 transactions: 200, 300, 1000 (oldest to newest)
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id, Amount = 200, Type = WalletTransactionType.Earned,
            SarRate = 10m, SarAmount = 20, RemainingAmount = 200,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        });
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id, Amount = 300, Type = WalletTransactionType.Earned,
            SarRate = 10m, SarAmount = 30, RemainingAmount = 300,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id, Amount = 1000, Type = WalletTransactionType.Earned,
            SarRate = 10m, SarAmount = 100, RemainingAmount = 1000,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        // Redeem 500 points — should consume 200 + 300 (oldest two), leave 1000 untouched
        var request = new RedemptionRequest
        {
            UserId = userId, Method = RedemptionMethod.BankTransfer,
            Status = RedemptionRequestStatus.PendingAdmin,
            PointsAmount = 500, SarRate = 10m, SarAmount = 50
        };
        _context.RedemptionRequests.Add(request);
        await _context.SaveChangesAsync();

        SetupApprover("admin", UserRoles.SystemAdmin);
        var result = await _sut.ApproveAsync(new ApproveRedemptionRequest(request.Id), "admin");
        result.IsSuccess.Should().BeTrue();

        var txs = await _context.WalletTransactions
            .Where(t => t.WalletId == wallet.Id && t.Type == WalletTransactionType.Earned)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        txs[0].RemainingAmount.Should().Be(0);    // 200 fully consumed
        txs[1].RemainingAmount.Should().Be(0);    // 300 fully consumed
        txs[2].RemainingAmount.Should().Be(1000); // untouched
    }

    [Fact]
    public async Task CompleteRedemption_PartialSingleTx_ShouldLeaveRemainder()
    {
        var userId = "user-1";
        var userEntity = new ApplicationUser
        {
            Id = userId, Name = "Requester", MobileNumber = "+966500000001", UserName = "+966500000001",
            RegistrationStatus = RegistrationStatus.Approved,
            NationalAddress = new NationalAddress { CityId = "city-1" }
        };
        _context.Add(userEntity);

        var wallet = new Wallet
        {
            UserId = userId, Balance = 2000, SarBalance = 200,
            HeldBalance = 1000, HeldSarBalance = 100
        };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        // Single large earned transaction
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id, Amount = 2000, Type = WalletTransactionType.Earned,
            SarRate = 10m, SarAmount = 200, RemainingAmount = 2000,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        var request = new RedemptionRequest
        {
            UserId = userId, Method = RedemptionMethod.BankTransfer,
            Status = RedemptionRequestStatus.PendingAdmin,
            PointsAmount = 1000, SarRate = 10m, SarAmount = 100
        };
        _context.RedemptionRequests.Add(request);
        await _context.SaveChangesAsync();

        SetupApprover("admin", UserRoles.SystemAdmin);
        var result = await _sut.ApproveAsync(new ApproveRedemptionRequest(request.Id), "admin");
        result.IsSuccess.Should().BeTrue();

        var earnedTx = await _context.WalletTransactions
            .FirstAsync(t => t.Type == WalletTransactionType.Earned);
        earnedTx.RemainingAmount.Should().Be(1000); // 2000 - 1000 consumed
    }

    // ── Reject ──

    [Fact]
    public async Task Reject_ShouldRefundHeldPoints()
    {
        var (wallet, request) = await SeedPendingRedemption(
            "user-1", RedemptionRequestStatus.PendingSalesMan, points: 1000, sarRate: 10m);
        SetupApprover("sm-1", UserRoles.SalesMan);

        var initialBalance = wallet.Balance;

        var result = await _sut.RejectAsync(
            new RejectRedemptionRequest(request.Id, "Not eligible"), "sm-1");

        result.IsSuccess.Should().BeTrue();

        var updated = await _context.RedemptionRequests.FindAsync(request.Id);
        updated!.Status.Should().Be(RedemptionRequestStatus.Rejected);
        updated.RejectionReason.Should().Be("Not eligible");

        // Held balance released
        var updatedWallet = await _context.Wallets.FirstAsync(w => w.UserId == "user-1");
        updatedWallet.HeldBalance.Should().Be(0);
        updatedWallet.HeldSarBalance.Should().Be(0);
        // Total balance unchanged (points were held, not deducted)
        updatedWallet.Balance.Should().Be(initialBalance);

        // Refund transaction created
        var refundTx = await _context.WalletTransactions
            .FirstAsync(t => t.Type == WalletTransactionType.Refunded);
        refundTx.Amount.Should().Be(1000);
        refundTx.SarAmount.Should().Be(100);
    }

    // ── Cash Handover ──

    [Fact]
    public async Task ConfirmCashHandover_ValidOtp_ShouldComplete()
    {
        var userId = "user-1";
        var userEntity = new ApplicationUser
        {
            Id = userId, Name = "Requester", MobileNumber = "+966500000001", UserName = "+966500000001",
            RegistrationStatus = RegistrationStatus.Approved,
            NationalAddress = new NationalAddress { CityId = "city-1" }
        };
        _context.Add(userEntity);

        var wallet = new Wallet
        {
            UserId = userId, Balance = 1500, SarBalance = 150,
            HeldBalance = 1000, HeldSarBalance = 100
        };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id, Amount = 1500, Type = WalletTransactionType.Earned,
            SarRate = 10m, SarAmount = 150, RemainingAmount = 1500,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        var request = new RedemptionRequest
        {
            UserId = userId, Method = RedemptionMethod.Cash,
            Status = RedemptionRequestStatus.AdminApproved,
            PointsAmount = 1000, SarRate = 10m, SarAmount = 100,
            CashOtpHash = "8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92",
            CashOtpExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _context.RedemptionRequests.Add(request);
        await _context.SaveChangesAsync();

        var result = await _sut.ConfirmCashHandoverAsync(
            new ConfirmCashHandoverRequest(request.Id, "123456"), "handover-user");

        result.IsSuccess.Should().BeTrue();

        var updated = await _context.RedemptionRequests.FindAsync(request.Id);
        updated!.Status.Should().Be(RedemptionRequestStatus.Completed);
        updated.CashHandoverById.Should().Be("handover-user");
        updated.CashHandoverAt.Should().NotBeNull();

        // Wallet deducted
        var updatedWallet = await _context.Wallets.FirstAsync(w => w.UserId == userId);
        updatedWallet.Balance.Should().Be(500);
        updatedWallet.HeldBalance.Should().Be(0);
    }

    [Fact]
    public async Task ConfirmCashHandover_WrongOtp_ShouldFail()
    {
        var request = new RedemptionRequest
        {
            UserId = "user-1", Method = RedemptionMethod.Cash,
            Status = RedemptionRequestStatus.AdminApproved,
            PointsAmount = 1000, SarRate = 10m, SarAmount = 100,
            CashOtpHash = "8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92",
            CashOtpExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _context.RedemptionRequests.Add(request);
        await _context.SaveChangesAsync();

        var result = await _sut.ConfirmCashHandoverAsync(
            new ConfirmCashHandoverRequest(request.Id, "999999"), "handover-user");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RedemptionErrors.InvalidOtp);
    }

    [Fact]
    public async Task ConfirmCashHandover_ExpiredOtp_ShouldCancelAndRefund()
    {
        var wallet = new Wallet
        {
            UserId = "user-1", Balance = 1500, SarBalance = 150,
            HeldBalance = 1000, HeldSarBalance = 100
        };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        var request = new RedemptionRequest
        {
            UserId = "user-1", Method = RedemptionMethod.Cash,
            Status = RedemptionRequestStatus.AdminApproved,
            PointsAmount = 1000, SarRate = 10m, SarAmount = 100,
            CashOtpHash = "8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92",
            CashOtpExpiresAt = DateTime.UtcNow.AddDays(-1) // expired
        };
        _context.RedemptionRequests.Add(request);
        await _context.SaveChangesAsync();

        var result = await _sut.ConfirmCashHandoverAsync(
            new ConfirmCashHandoverRequest(request.Id, "123456"), "handover-user");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RedemptionErrors.OtpExpired);

        // Should be cancelled and refunded
        var updated = await _context.RedemptionRequests.FindAsync(request.Id);
        updated!.Status.Should().Be(RedemptionRequestStatus.Cancelled);

        var updatedWallet = await _context.Wallets.FirstAsync(w => w.UserId == "user-1");
        updatedWallet.HeldBalance.Should().Be(0); // refunded
    }

    [Fact]
    public async Task ConfirmCashHandover_NotInCashHandoverState_ShouldFail()
    {
        var request = new RedemptionRequest
        {
            UserId = "user-1", Method = RedemptionMethod.BankTransfer, // wrong method
            Status = RedemptionRequestStatus.AdminApproved,
            PointsAmount = 1000, SarRate = 10m, SarAmount = 100
        };
        _context.RedemptionRequests.Add(request);
        await _context.SaveChangesAsync();

        var result = await _sut.ConfirmCashHandoverAsync(
            new ConfirmCashHandoverRequest(request.Id, "123456"), "handover-user");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RedemptionErrors.NotInCashHandoverState);
    }
}
