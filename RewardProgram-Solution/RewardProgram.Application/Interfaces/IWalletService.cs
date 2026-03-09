using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Wallet;

namespace RewardProgram.Application.Interfaces;

public interface IWalletService
{
    Task<Result<WalletBalanceResponse>> GetBalanceAsync(string userId, CancellationToken ct = default);
    Task<Result<PaginatedResult<WalletTransactionResponse>>> GetTransactionsAsync(string userId, WalletTransactionListQuery query, CancellationToken ct = default);
}
