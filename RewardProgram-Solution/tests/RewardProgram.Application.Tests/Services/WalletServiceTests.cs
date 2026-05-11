using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Contracts.Wallet;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Services;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Tests.Services;

public class WalletServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly WalletService _sut;

    public WalletServiceTests()
    {
        _context = TestDbContext.Create();
        _sut = new WalletService(_context, Substitute.For<ILogger<WalletService>>(), new StubLocalizer<ErrorMessages>());
    }

    public void Dispose() => _context.Dispose();

    // ── GetBalanceAsync ──

    [Fact]
    public async Task GetBalance_NoWallet_ShouldReturnZeros()
    {
        var result = await _sut.GetBalanceAsync("non-existent-user");

        result.IsSuccess.Should().BeTrue();
        result.Value.Balance.Should().Be(0);
        result.Value.SarBalance.Should().Be(0);
    }

    [Fact]
    public async Task GetBalance_WithWallet_ShouldReturnActualBalance()
    {
        var wallet = new Wallet { UserId = "user-1", Balance = 50, SarBalance = 5 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        var result = await _sut.GetBalanceAsync("user-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Balance.Should().Be(50);
        result.Value.SarBalance.Should().Be(5);
    }

    // ── GetTransactionsAsync ──

    [Fact]
    public async Task GetTransactions_NoTransactions_ShouldReturnEmptyList()
    {
        var wallet = new Wallet { UserId = "user-1", Balance = 0, SarBalance = 0 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        var query = new WalletTransactionListQuery(null, null, null);
        var result = await _sut.GetTransactionsAsync("user-1", query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetTransactions_WithTransactions_ShouldReturnPaginated()
    {
        var wallet = new Wallet { UserId = "user-1", Balance = 100, SarBalance = 10 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        for (int i = 0; i < 5; i++)
        {
            _context.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                Amount = 20,
                Type = WalletTransactionType.Earned,
                SarRate = 10,
                SarAmount = 2,
                Description = $"Test {i}"
            });
        }
        await _context.SaveChangesAsync();

        var query = new WalletTransactionListQuery(null, null, null, Page: 1, PageSize: 3);
        var result = await _sut.GetTransactionsAsync("user-1", query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(5);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(3);
    }

    [Fact]
    public async Task GetTransactions_FilterByType_ShouldReturnOnlyMatchingType()
    {
        var wallet = new Wallet { UserId = "user-1", Balance = 100, SarBalance = 10 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id, Amount = 20, Type = WalletTransactionType.Earned,
            SarRate = 10, SarAmount = 2
        });
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id, Amount = -10, Type = WalletTransactionType.Cancelled,
            SarRate = 10, SarAmount = -1
        });
        await _context.SaveChangesAsync();

        var query = new WalletTransactionListQuery(WalletTransactionType.Earned, null, null);
        var result = await _sut.GetTransactionsAsync("user-1", query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].Type.Should().Be(WalletTransactionType.Earned);
    }
}
