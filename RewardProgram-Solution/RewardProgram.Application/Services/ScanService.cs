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
    private readonly INotificationService _notificationService;
    private readonly ILogger<ScanService> _logger;

    public ScanService(
        IApplicationDbContext context,
        IUserRepository userRepository,
        INotificationService notificationService,
        ILogger<ScanService> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    private const int MaxRetries = 2;

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

        // Pre-create the scanner's wallet in its own save so the unique-constraint
        // race on first-scan-ever is resolved outside the scan transaction. Otherwise
        // a losing concurrent insert leaves an Added entity in the change tracker
        // that poisons the retry loop below.
        await EnsureWalletAsync(userId, ct);

        // Retry loop: RowVersion concurrency on ProductBarcode may cause
        // DbUpdateConcurrencyException on legitimate concurrent scans. Retry once.
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            var result = await TryScanBarcodeAsync(request, userId, scannerRole, ct);
            if (result.IsSuccess || result.Error != BarcodeErrors.ConcurrencyConflict)
                return result;

            _logger.LogInformation("Retrying scan for barcode '{Code}' by user {UserId} (attempt {Attempt})",
                request.BarcodeCode, userId, attempt + 2);
        }

        return Result.Failure<ScanBarcodeResponse>(BarcodeErrors.ConcurrencyConflict);
    }

    private async Task<Result<ScanBarcodeResponse>> TryScanBarcodeAsync(
        ScanBarcodeRequest request, string userId, ScannerRole scannerRole, CancellationToken ct)
    {
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

            // 7. Calculate points based on scan order and role
            switch (barcode.Status)
            {
                case BarcodeStatus.Available when scannerRole == ScannerRole.Seller:
                    // Seller scans first → 50%
                    pointsForScanner = pointValue / 2m;
                    barcode.Status = BarcodeStatus.SellerScanned;
                    break;

                case BarcodeStatus.Available when scannerRole == ScannerRole.Technician:
                    // Technician scans first → 100%
                    pointsForScanner = pointValue;
                    barcode.Status = BarcodeStatus.TechnicianScanned;
                    break;

                case BarcodeStatus.SellerScanned when scannerRole == ScannerRole.Technician:
                    // Technician scans second → 100% for technician + remaining 50% for seller
                    pointsForScanner = pointValue;
                    var sellerScanRecord = barcode.ScanRecords.First(s => s.ScannerRole == ScannerRole.Seller);
                    deferredPointsForFirstScanner = pointValue - sellerScanRecord.PointsAwarded;
                    firstScannerUserId = sellerScanRecord.UserId;
                    barcode.Status = BarcodeStatus.Consumed;
                    break;

                case BarcodeStatus.TechnicianScanned when scannerRole == ScannerRole.Seller:
                    // Seller scans second → 100% directly
                    pointsForScanner = pointValue;
                    barcode.Status = BarcodeStatus.Consumed;
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
            var scannerWallet = await _context.Wallets
                .FirstAsync(w => w.UserId == userId, ct);
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
                SarAmount = scannerSarAmount,
                RemainingAmount = pointsForScanner
            }, ct);

            // 10. Credit deferred points to first scanner (if applicable)
            if (deferredPointsForFirstScanner.HasValue && firstScannerUserId is not null)
            {
                var deferredSarAmount = deferredPointsForFirstScanner.Value / sarRate;
                // First scanner already earned points on their prior scan, so their wallet exists.
                var firstScannerWallet = await _context.Wallets
                    .FirstAsync(w => w.UserId == firstScannerUserId, ct);
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
                    SarAmount = deferredSarAmount,
                    RemainingAmount = deferredPointsForFirstScanner.Value
                }, ct);
            }

            // 11. Save & commit
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Barcode '{Code}' scanned by {Role} user {UserId} — {Points} points awarded",
                barcode.Code, scannerRole, userId, pointsForScanner);

            // Scanner sees the points immediately in the scan response — no self-notification.
            // Only notify the first scanner when this scan unlocks their deferred points.
            if (deferredPointsForFirstScanner.HasValue && firstScannerUserId is not null)
            {
                try
                {
                    await _notificationService.CreateAsync(firstScannerUserId, NotificationType.PointsEarned,
                        "نقاط جديدة", $"حصلت على {deferredPointsForFirstScanner.Value} نقطة مؤجلة — {barcode.Product.Name}",
                        scanRecord.Id, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify first scanner {UserId} of deferred points", firstScannerUserId);
                }
            }

            return Result.Success(new ScanBarcodeResponse(
                barcode.Product.Name,
                pointsForScanner,
                scannerWallet.Balance,
                barcode.Id,
                barcode.Code
            ));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning("Concurrency conflict scanning barcode '{Code}' by user {UserId}", request.BarcodeCode, userId);
            return Result.Failure<ScanBarcodeResponse>(BarcodeErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning(ex, "Duplicate key conflict scanning barcode '{Code}' by user {UserId}", request.BarcodeCode, userId);
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

        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(s => s.CreatedAt >= query.FromDate.Value.Date);

        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(s => s.CreatedAt < query.ToDate.Value.Date.AddDays(1));

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

    private async Task EnsureWalletAsync(string userId, CancellationToken ct)
    {
        if (await _context.Wallets.AnyAsync(w => w.UserId == userId, ct))
            return;

        var wallet = new Wallet
        {
            UserId = userId,
            Balance = 0
        };

        await _context.Wallets.AddAsync(wallet, ct);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Concurrent creator won the unique-index race — detach the failed
            // insert so it doesn't get replayed by the next SaveChanges in this scope.
            // Remove() on an Added entity detaches rather than marks Deleted.
            _context.Wallets.Remove(wallet);
        }
    }

    // SQL Server: 2627 = primary-key violation, 2601 = unique-index violation.
    // Other DbUpdateException causes (FK, timeout, deadlock) should bubble up rather
    // than be silently treated as "concurrent creator won".
    // Duck-typed by name to avoid pulling Microsoft.Data.SqlClient into the
    // Application layer.
    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        while (inner is not null)
        {
            if (inner.GetType().Name == "SqlException")
            {
                var numberProp = inner.GetType().GetProperty("Number");
                if (numberProp?.GetValue(inner) is int number
                    && (number == 2627 || number == 2601))
                    return true;
            }
            inner = inner.InnerException;
        }
        return false;
    }
}
