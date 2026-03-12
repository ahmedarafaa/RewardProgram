using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Scan;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services;

public class ScanService : IScanService
{
    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ScanService> _logger;

    public ScanService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        ILogger<ScanService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result<ScanBarcodeResponse>> ScanBarcodeAsync(
        ScanBarcodeRequest request, string userId, CancellationToken ct = default)
    {
        // 1. Validate user
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure<ScanBarcodeResponse>(ScanErrors.UserNotApproved);

        if (user.IsDisabled || user.RegistrationStatus != RegistrationStatus.Approved)
            return Result.Failure<ScanBarcodeResponse>(ScanErrors.UserNotApproved);

        // 2. Determine scanner role
        var roles = await _userRepository.GetRolesAsync(user);
        ScannerRole scannerRole;

        if (roles.Contains(UserRoles.Seller))
            scannerRole = ScannerRole.Seller;
        else if (roles.Contains(UserRoles.Technician))
            scannerRole = ScannerRole.Technician;
        else
            return Result.Failure<ScanBarcodeResponse>(ScanErrors.UnauthorizedRole);

        // 3. Find barcode
        var barcode = await _context.ProductBarcodes
            .Include(b => b.Product)
            .Include(b => b.ScanRecords)
            .FirstOrDefaultAsync(b => b.Code == request.BarcodeCode, ct);

        if (barcode is null)
            return Result.Failure<ScanBarcodeResponse>(BarcodeErrors.BarcodeNotFound);

        if (barcode.Status == BarcodeStatus.Consumed)
            return Result.Failure<ScanBarcodeResponse>(BarcodeErrors.BarcodeConsumed);

        // 4. Check if already scanned by this role
        if (barcode.ScanRecords.Any(s => s.ScannerRole == scannerRole))
            return Result.Failure<ScanBarcodeResponse>(BarcodeErrors.BarcodeAlreadyScanned);

        // 5. Get current SAR rate
        var settings = await _context.RewardSettings.FirstOrDefaultAsync(ct);
        var sarRate = settings?.PointsToSarRate ?? 10m;

        // 6. Begin transaction
        await using var transaction = await _context.BeginTransactionAsync(ct);

        try
        {
            var pointValue = barcode.Product.PointValue;
            decimal pointsForScanner;
            decimal? deferredPointsForFirstScanner = null;
            string? firstScannerUserId = null;
            string message;

            // 7. Calculate points based on scan order and role
            switch (barcode.Status)
            {
                case BarcodeStatus.Available when scannerRole == ScannerRole.Seller:
                    // Seller scans first → 50%
                    pointsForScanner = pointValue / 2m;
                    barcode.Status = BarcodeStatus.SellerScanned;
                    message = $"تم مسح الباركود بنجاح — حصلت على {pointsForScanner} نقطة (50%)";
                    break;

                case BarcodeStatus.Available when scannerRole == ScannerRole.Technician:
                    // Technician scans first → 100%
                    pointsForScanner = pointValue;
                    barcode.Status = BarcodeStatus.TechnicianScanned;
                    message = $"تم مسح الباركود بنجاح — حصلت على {pointsForScanner} نقطة";
                    break;

                case BarcodeStatus.SellerScanned when scannerRole == ScannerRole.Technician:
                    // Technician scans second → 100% for technician + remaining 50% for seller
                    pointsForScanner = pointValue;
                    var sellerScanRecord = barcode.ScanRecords.First(s => s.ScannerRole == ScannerRole.Seller);
                    deferredPointsForFirstScanner = pointValue - sellerScanRecord.PointsAwarded;
                    firstScannerUserId = sellerScanRecord.UserId;
                    barcode.Status = BarcodeStatus.Consumed;
                    message = $"تم مسح الباركود بنجاح — حصلت على {pointsForScanner} نقطة";
                    break;

                case BarcodeStatus.TechnicianScanned when scannerRole == ScannerRole.Seller:
                    // Seller scans second → 100% directly
                    pointsForScanner = pointValue;
                    barcode.Status = BarcodeStatus.Consumed;
                    message = $"تم مسح الباركود بنجاح — حصلت على {pointsForScanner} نقطة (100%)";
                    break;

                default:
                    return Result.Failure<ScanBarcodeResponse>(BarcodeErrors.BarcodeAlreadyScanned);
            }

            // 8. Create scan record
            var scanRecord = new ScanRecord
            {
                BarcodeId = barcode.Id,
                UserId = userId,
                ScannerRole = scannerRole,
                PointsAwarded = pointsForScanner,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            };
            await _context.ScanRecords.AddAsync(scanRecord, ct);

            // 9. Credit scanner's wallet
            var scannerSarAmount = pointsForScanner / sarRate;
            var scannerWallet = await GetOrCreateWalletAsync(userId, ct);
            scannerWallet.Balance += pointsForScanner;
            scannerWallet.SarBalance += scannerSarAmount;

            await _context.WalletTransactions.AddAsync(new WalletTransaction
            {
                WalletId = scannerWallet.Id,
                Amount = pointsForScanner,
                Type = WalletTransactionType.Earned,
                ReferenceId = scanRecord.Id,
                Description = $"مسح باركود — {barcode.Product.Name}",
                SarRate = sarRate,
                SarAmount = scannerSarAmount
            }, ct);

            // 10. Credit deferred points to first scanner (if applicable)
            if (deferredPointsForFirstScanner.HasValue && firstScannerUserId is not null)
            {
                var deferredSarAmount = deferredPointsForFirstScanner.Value / sarRate;
                var firstScannerWallet = await GetOrCreateWalletAsync(firstScannerUserId, ct);
                firstScannerWallet.Balance += deferredPointsForFirstScanner.Value;
                firstScannerWallet.SarBalance += deferredSarAmount;

                await _context.WalletTransactions.AddAsync(new WalletTransaction
                {
                    WalletId = firstScannerWallet.Id,
                    Amount = deferredPointsForFirstScanner.Value,
                    Type = WalletTransactionType.Earned,
                    ReferenceId = scanRecord.Id,
                    Description = $"نقاط مؤجلة — {barcode.Product.Name}",
                    SarRate = sarRate,
                    SarAmount = deferredSarAmount
                }, ct);
            }

            // 11. Save & commit
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Barcode '{Code}' scanned by {Role} user {UserId} — {Points} points awarded",
                barcode.Code, scannerRole, userId, pointsForScanner);

            return Result.Success(new ScanBarcodeResponse(
                barcode.Product.Name,
                pointsForScanner,
                scannerWallet.Balance,
                message
            ));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning("Concurrency conflict scanning barcode '{Code}' by user {UserId}", request.BarcodeCode, userId);
            return Result.Failure<ScanBarcodeResponse>(BarcodeErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning("Duplicate key conflict scanning barcode '{Code}' by user {UserId} (likely concurrent wallet creation)", request.BarcodeCode, userId);
            return Result.Failure<ScanBarcodeResponse>(BarcodeErrors.ConcurrencyConflict);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to scan barcode '{Code}' by user {UserId}", request.BarcodeCode, userId);
            throw;
        }
    }

    public async Task<Result<PaginatedResult<ScanHistoryItemResponse>>> GetScanHistoryAsync(
        string userId, ScanHistoryQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = PaginationHelper.Normalize(query.Page, query.PageSize);

        var dbQuery = _context.ScanRecords
            .AsNoTracking()
            .Include(s => s.Barcode)
                .ThenInclude(b => b.Product)
            .Where(s => s.UserId == userId);

        var totalCount = await dbQuery.CountAsync(ct);

        var items = await dbQuery
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ScanHistoryItemResponse(
                s.Id,
                s.Barcode.Code,
                s.Barcode.Product.Name,
                s.Barcode.Product.ProductCode,
                s.Barcode.Product.PointValue,
                s.PointsAwarded,
                s.ScannerRole,
                s.Barcode.Status,
                s.CreatedAt,
                s.Latitude,
                s.Longitude
            ))
            .ToListAsync(ct);

        return Result.Success(new PaginatedResult<ScanHistoryItemResponse>(items, totalCount, page, pageSize));
    }

    private async Task<Wallet> GetOrCreateWalletAsync(string userId, CancellationToken ct)
    {
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);

        if (wallet is not null)
            return wallet;

        wallet = new Wallet
        {
            UserId = userId,
            Balance = 0
        };

        await _context.Wallets.AddAsync(wallet, ct);
        return wallet;
    }
}
