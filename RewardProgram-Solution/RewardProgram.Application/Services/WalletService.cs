using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Wallet;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Application.Services;

public class WalletService : IWalletService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        IApplicationDbContext context,
        ILogger<WalletService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<WalletBalanceResponse>> GetBalanceAsync(
        string userId, CancellationToken ct = default)
    {
        var wallet = await _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);

        var balance = wallet?.Balance ?? 0;
        return Result.Success(new WalletBalanceResponse(balance));
    }

    public async Task<Result<PaginatedResult<WalletTransactionResponse>>> GetTransactionsAsync(
        string userId, WalletTransactionListQuery query, CancellationToken ct = default)
    {
        var wallet = await _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);

        if (wallet is null)
            return Result.Success(new PaginatedResult<WalletTransactionResponse>([], 0, query.Page, query.PageSize));

        var dbQuery = _context.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == wallet.Id);

        if (query.Type.HasValue)
            dbQuery = dbQuery.Where(t => t.Type == query.Type.Value);

        var totalCount = await dbQuery.CountAsync(ct);

        var items = await dbQuery
            .OrderByDescending(t => t.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => new WalletTransactionResponse(
                t.Id,
                t.Amount,
                t.Type,
                t.Description,
                t.CreatedAt
            ))
            .ToListAsync(ct);

        return Result.Success(new PaginatedResult<WalletTransactionResponse>(items, totalCount, query.Page, query.PageSize));
    }
}
