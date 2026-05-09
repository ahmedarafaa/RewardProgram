using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Services;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;
using static RewardProgram.Application.Tests.TestHelpers.AsyncQueryableExtensions;

namespace RewardProgram.Application.Tests.Services;

public class InvitationServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly IUserRepository _userRepo;
    private readonly INotificationService _notificationService;
    private readonly InvitationService _sut;

    public InvitationServiceTests()
    {
        _context = TestDbContext.Create();
        _userRepo = Substitute.For<IUserRepository>();
        _notificationService = Substitute.For<INotificationService>();

        var options = Options.Create(new InvitationOptions());
        _sut = new InvitationService(
            _context, _userRepo, _notificationService, options,
            Substitute.For<ILogger<InvitationService>>());
    }

    public void Dispose() => _context.Dispose();

    // ── GetInvitationInfo ──

    [Fact]
    public async Task GetInvitationInfo_UserNotFound_ShouldFail()
    {
        _userRepo.FindByIdAsync("bad", Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.GetInvitationInfoAsync("bad");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Errors.InvitationErrors.InviterNotApproved);
    }

    [Fact]
    public async Task GetInvitationInfo_ExistingCode_ShouldReturnWithoutRegenerating()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "+966500000001",
            InvitationCode = "ABCD1234"
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);
        _userRepo.Query().Returns(new List<ApplicationUser>().AsAsyncQueryable());

        var result = await _sut.GetInvitationInfoAsync("u1");

        result.IsSuccess.Should().BeTrue();
        result.Value.InvitationCode.Should().Be("ABCD1234");
        result.Value.ShareLink.Should().Contain("ABCD1234");
    }

    [Fact]
    public async Task GetInvitationInfo_NoCode_ShouldGenerateNewCode()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "+966500000001",
            InvitationCode = null
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);
        _userRepo.Query().Returns(new List<ApplicationUser>().AsAsyncQueryable());

        var result = await _sut.GetInvitationInfoAsync("u1");

        result.IsSuccess.Should().BeTrue();
        result.Value.InvitationCode.Should().NotBeNullOrEmpty();
        result.Value.InvitationCode.Should().HaveLength(8);
        await _userRepo.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task GetInvitationInfo_ShouldReturnCorrectStats()
    {
        var inviter = new ApplicationUser
        {
            Id = "inviter", Name = "Inviter", MobileNumber = "+966500000001",
            InvitationCode = "CODE1234"
        };
        var invitedApproved = new ApplicationUser
        {
            Id = "inv1", Name = "Inv1", MobileNumber = "+966500000002",
            InvitedByUserId = "inviter",
            RegistrationStatus = RegistrationStatus.Approved
        };
        var invitedPending = new ApplicationUser
        {
            Id = "inv2", Name = "Inv2", MobileNumber = "+966500000003",
            InvitedByUserId = "inviter",
            RegistrationStatus = RegistrationStatus.PendingSalesman
        };

        _userRepo.FindByIdAsync("inviter", Arg.Any<CancellationToken>()).Returns(inviter);
        _userRepo.Query().Returns(new List<ApplicationUser> { inviter, invitedApproved, invitedPending }.AsAsyncQueryable());

        // Add invitation reward transaction
        var wallet = new Wallet { UserId = "inviter", Balance = 100 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id, Amount = 100, Type = WalletTransactionType.InvitationReward,
            ReferenceId = "inv1", SarRate = 10, SarAmount = 10, RemainingAmount = 100
        });
        await _context.SaveChangesAsync();

        var result = await _sut.GetInvitationInfoAsync("inviter");

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalInvitations.Should().Be(2);
        result.Value.ApprovedInvitations.Should().Be(1);
        result.Value.TotalPointsEarned.Should().Be(100);
    }

    // ── CreditInvitationRewards ──

    [Fact]
    public async Task CreditRewards_InviteeHasNoInviter_ShouldDoNothing()
    {
        var invitee = new ApplicationUser
        {
            Id = "invitee", Name = "Invitee", MobileNumber = "+966500000001",
            InvitedByUserId = null
        };
        _userRepo.FindByIdAsync("invitee", Arg.Any<CancellationToken>()).Returns(invitee);

        await _sut.CreditInvitationRewardsAsync("invitee");

        _context.WalletTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task CreditRewards_Valid_ShouldCreditBothInviterAndInvitee()
    {
        var inviter = new ApplicationUser { Id = "inviter", Name = "Inviter", MobileNumber = "+966500000001" };
        var invitee = new ApplicationUser
        {
            Id = "invitee", Name = "Invitee", MobileNumber = "+966500000002",
            InvitedByUserId = "inviter"
        };

        _userRepo.FindByIdAsync("invitee", Arg.Any<CancellationToken>()).Returns(invitee);
        _userRepo.FindByIdAsync("inviter", Arg.Any<CancellationToken>()).Returns(inviter);

        // Default settings: 100 inviter, 50 invitee, 10 SAR rate
        _context.RewardSettings.Add(new RewardSettings
        {
            InviterRewardPoints = 100, InviteeRewardPoints = 50, PointsToSarRate = 10
        });
        await _context.SaveChangesAsync();

        await _sut.CreditInvitationRewardsAsync("invitee");

        // Invitee wallet: 50 points, 5 SAR
        var inviteeWallet = _context.Wallets.First(w => w.UserId == "invitee");
        inviteeWallet.Balance.Should().Be(50);
        inviteeWallet.SarBalance.Should().Be(5);

        // Inviter wallet: 100 points, 10 SAR
        var inviterWallet = _context.Wallets.First(w => w.UserId == "inviter");
        inviterWallet.Balance.Should().Be(100);
        inviterWallet.SarBalance.Should().Be(10);

        // 2 transactions total
        _context.WalletTransactions.Count().Should().Be(2);
    }

    [Fact]
    public async Task CreditRewards_InviterAt20Cap_ShouldOnlyCreditInvitee()
    {
        var inviter = new ApplicationUser { Id = "inviter", Name = "Inviter", MobileNumber = "+966500000001" };
        var invitee = new ApplicationUser
        {
            Id = "invitee-21", Name = "Invitee21", MobileNumber = "+966500000002",
            InvitedByUserId = "inviter"
        };

        _userRepo.FindByIdAsync("invitee-21", Arg.Any<CancellationToken>()).Returns(invitee);
        _userRepo.FindByIdAsync("inviter", Arg.Any<CancellationToken>()).Returns(inviter);

        _context.RewardSettings.Add(new RewardSettings
        {
            InviterRewardPoints = 100, InviteeRewardPoints = 50, PointsToSarRate = 10
        });

        // Create inviter wallet with 20 existing invitation rewards
        var inviterWallet = new Wallet { UserId = "inviter", Balance = 2000, SarBalance = 200 };
        _context.Wallets.Add(inviterWallet);
        await _context.SaveChangesAsync();

        for (int i = 0; i < 20; i++)
        {
            _context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = inviterWallet.Id,
                Amount = 100,
                Type = WalletTransactionType.InvitationReward,
                ReferenceId = $"past-invitee-{i}", // ReferenceId != inviter's own ID → counts as inviter reward
                SarRate = 10, SarAmount = 10, RemainingAmount = 100
            });
        }
        await _context.SaveChangesAsync();

        await _sut.CreditInvitationRewardsAsync("invitee-21");

        // Inviter wallet should NOT have increased
        var updatedInviterWallet = _context.Wallets.First(w => w.UserId == "inviter");
        updatedInviterWallet.Balance.Should().Be(2000);

        // Invitee should still get their reward
        var inviteeWallet = _context.Wallets.First(w => w.UserId == "invitee-21");
        inviteeWallet.Balance.Should().Be(50);
    }

    [Fact]
    public async Task CreditRewards_Idempotent_ShouldNotCreditTwice()
    {
        var inviter = new ApplicationUser { Id = "inviter", Name = "Inviter", MobileNumber = "+966500000001" };
        var invitee = new ApplicationUser
        {
            Id = "invitee", Name = "Invitee", MobileNumber = "+966500000002",
            InvitedByUserId = "inviter"
        };

        _userRepo.FindByIdAsync("invitee", Arg.Any<CancellationToken>()).Returns(invitee);
        _userRepo.FindByIdAsync("inviter", Arg.Any<CancellationToken>()).Returns(inviter);

        _context.RewardSettings.Add(new RewardSettings
        {
            InviterRewardPoints = 100, InviteeRewardPoints = 50, PointsToSarRate = 10
        });
        await _context.SaveChangesAsync();

        // First call
        await _sut.CreditInvitationRewardsAsync("invitee");

        // Second call — should be idempotent
        await _sut.CreditInvitationRewardsAsync("invitee");

        // Should still only have 2 transactions (1 invitee + 1 inviter from first call)
        _context.WalletTransactions.Count().Should().Be(2);
    }
}
