using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Wallet;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Services;

public class WalletService : IWalletService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<WalletService> _logger;
    private readonly IStringLocalizer<ErrorMessages> _localizer;

    public WalletService(
        IApplicationDbContext context,
        ILogger<WalletService> logger,
        IStringLocalizer<ErrorMessages> localizer)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<Result<WalletBalanceResponse>> GetBalanceAsync(
        string userId, CancellationToken ct = default)
    {
        var wallet = await _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);

        return Result.Success(new WalletBalanceResponse(
            wallet?.Balance ?? 0,
            wallet?.SarBalance ?? 0
        ));
    }

    public async Task<Result<PaginatedResult<WalletTransactionResponse>>> GetTransactionsAsync(
        string userId, WalletTransactionListQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = PaginationHelper.Normalize(query.Page, query.PageSize);

        var dbQuery = _context.WalletTransactions
            .AsNoTracking()
            .Where(t => t.Wallet.UserId == userId);

        if (query.Type.HasValue)
        {
            // "Earned" is the user-facing label for any incoming points. Expand the
            // filter to include InvitationReward so referral bonuses surface on the
            // mobile app's Earned tab — keeping a single Earned filter from the
            // user's perspective while preserving the underlying type distinction
            // for analytics queries that ask for InvitationReward explicitly.
            var t = query.Type.Value;
            dbQuery = t == WalletTransactionType.Earned
                ? dbQuery.Where(tx => tx.Type == WalletTransactionType.Earned
                    || tx.Type == WalletTransactionType.InvitationReward)
                : dbQuery.Where(tx => tx.Type == t);
        }

        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(t => t.CreatedAt >= query.FromDate.Value.Date);

        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(t => t.CreatedAt < query.ToDate.Value.Date.AddDays(1));

        var totalCount = await dbQuery.CountAsync(ct);

        // Pull the raw rows (incl. DescriptionKey/Args) and render the localized
        // Description in-memory per current culture. Old rows without a key fall
        // back to the legacy Arabic Description (see LocalizedTextRenderer).
        var rows = await dbQuery
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Amount,
                t.SarRate,
                t.SarAmount,
                t.Type,
                t.Description,
                t.DescriptionKey,
                t.DescriptionArgs,
                t.CreatedAt
            })
            .ToListAsync(ct);

        // Amount keeps its sign so the UI can render +N for credits and -N for
        // debits without having to interpret Type. SarAmount stays absolute —
        // it's an informational "equivalent value", direction is conveyed by Amount.
        var items = rows.Select(t => new WalletTransactionResponse(
            t.Id,
            t.Amount,
            t.SarRate,
            Math.Abs(t.SarAmount),
            t.Type,
            LocalizedTextRenderer.Render(_localizer, t.DescriptionKey, t.DescriptionArgs, t.Description),
            t.CreatedAt
        )).ToList();

        return Result.Success(new PaginatedResult<WalletTransactionResponse>(items, totalCount, page, pageSize));
    }
}
