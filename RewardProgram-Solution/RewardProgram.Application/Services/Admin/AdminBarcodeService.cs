using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NanoidDotNet;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Barcodes;
using RewardProgram.Application.Contracts.Admin.Scans;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services.Admin;

public class AdminBarcodeService : IAdminBarcodeService
{
    private const string BarcodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz";
    private const int BarcodeLength = 12;
    private const int MaxCollisionRetries = 3;

    private readonly IApplicationDbContext _context;
    private readonly ILogger<AdminBarcodeService> _logger;
    private readonly IStringLocalizer<ErrorMessages> _localizer;

    public AdminBarcodeService(
        IApplicationDbContext context,
        ILogger<AdminBarcodeService> logger,
        IStringLocalizer<ErrorMessages> localizer)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<Result<AdminGenerateBarcodesResponse>> GenerateBarcodesAsync(
        AdminGenerateBarcodesRequest request, string adminUserId, CancellationToken ct = default)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);
        if (product is null)
            return Result.Failure<AdminGenerateBarcodesResponse>(ProductErrors.ProductNotFound);

        var codes = new HashSet<string>(request.Quantity);

        // Generate unique codes with collision detection
        for (var i = 0; i < request.Quantity; i++)
            codes.Add(GenerateUniqueCode(codes));

        // Check for collisions against existing DB codes (retry on conflict)
        for (var attempt = 0; attempt <= MaxCollisionRetries; attempt++)
        {
            var existingCodes = await _context.ProductBarcodes
                .Where(b => codes.Contains(b.Code))
                .Select(b => b.Code)
                .ToListAsync(ct);

            if (existingCodes.Count == 0)
                break;

            if (attempt == MaxCollisionRetries)
            {
                _logger.LogError("Failed to generate unique barcode codes after {MaxRetries} retries", MaxCollisionRetries);
                return Result.Failure<AdminGenerateBarcodesResponse>(BarcodeErrors.CollisionRetryExhausted);
            }

            foreach (var collision in existingCodes)
            {
                codes.Remove(collision);
                codes.Add(GenerateUniqueCode(codes));
            }
        }

        var barcodes = codes.Select(code => new ProductBarcode
        {
            Code = code,
            ProductId = product.Id,
            Status = BarcodeStatus.Available
        }).ToList();

        await _context.ProductBarcodes.AddRangeAsync(barcodes, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Generated {Count} barcodes for product '{ProductCode}' by admin {AdminId}",
            request.Quantity, product.ProductCode, adminUserId);

        return Result.Success(new AdminGenerateBarcodesResponse(
            request.Quantity, product.Name, product.ProductCode, codes.ToList()));
    }

    private static string GenerateUniqueCode(HashSet<string> existingCodes)
    {
        string code;
        do
        {
            code = Nanoid.Generate(BarcodeAlphabet, BarcodeLength);
        } while (existingCodes.Contains(code));

        return code;
    }

    public async Task<Result<PaginatedResult<AdminBarcodeListItemResponse>>> ListBarcodesAsync(
        AdminBarcodeListQuery query, CancellationToken ct = default)
    {
        var dbQuery = _context.ProductBarcodes
            .AsNoTracking()
            .Include(b => b.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.ProductId))
            dbQuery = dbQuery.Where(b => b.ProductId == query.ProductId);

        if (query.Status.HasValue)
            dbQuery = dbQuery.Where(b => b.Status == query.Status.Value);

        var (page, pageSize) = PaginationHelper.Normalize(query.Page, query.PageSize);

        var totalCount = await dbQuery.CountAsync(ct);

        var items = await dbQuery
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new AdminBarcodeListItemResponse(
                b.Id,
                b.Code,
                b.Product.Name,
                b.Product.PointValue,
                b.Status,
                b.CreatedAt
            ))
            .ToListAsync(ct);

        return Result.Success(new PaginatedResult<AdminBarcodeListItemResponse>(items, totalCount, page, pageSize));
    }

    public async Task<Result<PaginatedResult<AdminScanListItemResponse>>> GetAdminScanListAsync(
        AdminScanListQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = PaginationHelper.Normalize(query.Page, query.PageSize);

        var dbQuery = _context.ScanRecords
            .AsNoTracking()
            .Include(s => s.Barcode)
                .ThenInclude(b => b.Product)
            .Include(s => s.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.UserId))
            dbQuery = dbQuery.Where(s => s.UserId == query.UserId);

        if (!string.IsNullOrWhiteSpace(query.ProductId))
            dbQuery = dbQuery.Where(s => s.Barcode.ProductId == query.ProductId);

        if (query.ScannerRole.HasValue)
            dbQuery = dbQuery.Where(s => s.ScannerRole == query.ScannerRole.Value);

        if (query.BarcodeStatus.HasValue)
            dbQuery = dbQuery.Where(s => s.Barcode.Status == query.BarcodeStatus.Value);

        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(s => s.CreatedAt >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(s => s.CreatedAt <= query.ToDate.Value);

        var totalCount = await dbQuery.CountAsync(ct);

        var items = await dbQuery
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new AdminScanListItemResponse(
                s.Id,
                s.Barcode.Code,
                s.Barcode.Product.Name,
                s.Barcode.Product.ProductCode,
                s.Barcode.Product.PointValue,
                s.PointsAwarded,
                s.ScannerRole,
                s.Barcode.Status,
                s.User.Name,
                s.User.MobileNumber,
                s.CreatedAt,
                s.Latitude,
                s.Longitude
            ))
            .ToListAsync(ct);

        return Result.Success(new PaginatedResult<AdminScanListItemResponse>(items, totalCount, page, pageSize));
    }

    public async Task<Result<AdminCancelScanResponse>> CancelScanAsync(
        string scanId, string adminUserId, CancellationToken ct = default)
    {
        // 1. Find scan record with barcode and all sibling scans
        var scan = await _context.ScanRecords
            .Include(s => s.Barcode)
                .ThenInclude(b => b.Product)
            .Include(s => s.Barcode)
                .ThenInclude(b => b.ScanRecords)
            .FirstOrDefaultAsync(s => s.Id == scanId, ct);

        if (scan is null)
            return Result.Failure<AdminCancelScanResponse>(BarcodeErrors.ScanNotFound);

        var barcode = scan.Barcode;
        var siblingScans = barcode.ScanRecords.Where(s => s.Id != scanId).ToList();

        // 2. If barcode is Consumed (both scanned), only allow cancelling the LAST scan
        if (barcode.Status == BarcodeStatus.Consumed && siblingScans.Count > 0)
        {
            var otherScan = siblingScans.First();
            // The first scan is the one that was created earlier
            if (scan.CreatedAt < otherScan.CreatedAt)
                return Result.Failure<AdminCancelScanResponse>(BarcodeErrors.CannotCancelFirstScan);
        }

        // 3. Find all wallet transactions referencing this scan
        var walletTransactions = await _context.WalletTransactions
            .Where(t => t.ReferenceId == scanId)
            .ToListAsync(ct);

        // Batch-fetch affected wallets to avoid N+1 in the reversal loop
        var walletIds = walletTransactions.Select(wt => wt.WalletId).Distinct().ToList();
        var walletsById = await _context.Wallets
            .Where(w => walletIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, ct);

        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            decimal totalPointsReversed = 0;
            decimal totalSarReversed = 0;

            // 4. Reverse each wallet transaction — check balance is sufficient
            foreach (var wt in walletTransactions)
            {
                var wallet = walletsById[wt.WalletId];

                // Available = Balance - HeldBalance. Reversal needs available >= earned amount.
                var availableBalance = wallet.Balance - wallet.HeldBalance;
                if (availableBalance < wt.Amount)
                {
                    await transaction.RollbackAsync(ct);
                    return Result.Failure<AdminCancelScanResponse>(
                        RedemptionErrors.CannotCancelScanWithInsufficientBalance);
                }

                wallet.Balance -= wt.Amount;
                wallet.SarBalance -= wt.SarAmount;
                totalPointsReversed += wt.Amount;
                totalSarReversed += wt.SarAmount;

                // Create reversal transaction
                await _context.WalletTransactions.AddAsync(new WalletTransaction
                {
                    WalletId = wt.WalletId,
                    Amount = -wt.Amount,
                    Type = WalletTransactionType.Cancelled,
                    ReferenceId = scanId,
                    Description = $"إلغاء مسح — {barcode.Product.Name}",
                    SarRate = wt.SarRate,
                    SarAmount = -wt.SarAmount
                }, ct);
            }

            // 5. Revert barcode status
            if (barcode.Status == BarcodeStatus.Consumed)
            {
                // Was consumed (both scanned), revert to the remaining scan's status
                var remainingScan = siblingScans.FirstOrDefault();
                if (remainingScan is not null)
                    barcode.Status = remainingScan.ScannerRole == ScannerRole.Seller
                        ? BarcodeStatus.SellerScanned
                        : BarcodeStatus.TechnicianScanned;
                else
                    barcode.Status = BarcodeStatus.Available;
            }
            else
            {
                // Only one scan existed, revert to Available
                barcode.Status = BarcodeStatus.Available;
            }

            // 6. Soft-delete scan record
            scan.IsDeleted = true;
            scan.DeletedAt = DateTime.UtcNow;
            scan.DeletedBy = adminUserId;

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Admin {AdminId} cancelled scan {ScanId} for barcode '{Code}' — reversed {Points} points, {Sar} SAR",
                adminUserId, scanId, barcode.Code, totalPointsReversed, totalSarReversed);

            return Result.Success(new AdminCancelScanResponse(
                scanId,
                barcode.Code,
                totalPointsReversed,
                totalSarReversed,
                _localizer["AdminScan.CancelSuccess"].Value
            ));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Admin: Failed to cancel scan {ScanId}", scanId);
            throw;
        }
    }
}
