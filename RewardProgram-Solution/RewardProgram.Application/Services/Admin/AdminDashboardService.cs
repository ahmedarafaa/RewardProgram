using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Analytics;
using RewardProgram.Application.Contracts.Admin.Dashboard;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services.Admin;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;

    public AdminDashboardService(IApplicationDbContext context, IUserRepository userRepository)
    {
        _context = context;
        _userRepository = userRepository;
    }

    public async Task<Result<AdminDashboardResponse>> GetDashboardAsync(CancellationToken ct = default)
    {
        var users = _userRepository.Query();

        var userTypeCounts = await users
            .Where(u => u.UserType == UserType.ShopOwner
                     || u.UserType == UserType.Seller
                     || u.UserType == UserType.Technician)
            .GroupBy(u => u.UserType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalShopOwners = userTypeCounts.FirstOrDefault(x => x.Type == UserType.ShopOwner)?.Count ?? 0;
        var totalSellers = userTypeCounts.FirstOrDefault(x => x.Type == UserType.Seller)?.Count ?? 0;
        var totalTechnicians = userTypeCounts.FirstOrDefault(x => x.Type == UserType.Technician)?.Count ?? 0;

        var totalPendingApprovals = await users
            .CountAsync(u => u.RegistrationStatus == RegistrationStatus.PendingSalesman
                          || u.RegistrationStatus == RegistrationStatus.PendingZoneManager, ct);

        var transactionSums = await _context.WalletTransactions
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        var totalPointsEarned = transactionSums
            .Where(x => x.Type == WalletTransactionType.Earned || x.Type == WalletTransactionType.InvitationReward)
            .Sum(x => x.Total);

        var totalPointsRedeemed = transactionSums
            .FirstOrDefault(x => x.Type == WalletTransactionType.Redeemed)?.Total ?? 0;

        var totalSarRedeemed = await _context.RedemptionRequests
            .Where(r => r.Status == RedemptionRequestStatus.Completed)
            .SumAsync(r => (decimal?)r.SarAmount ?? 0, ct);

        var totalActiveBarcodes = await _context.ProductBarcodes
            .CountAsync(b => b.Status == BarcodeStatus.Available && b.DeletedAt == null, ct);

        var totalScans = await _context.ScanRecords
            .CountAsync(s => s.DeletedAt == null, ct);

        var pendingRedemptions = await _context.RedemptionRequests
            .CountAsync(r => r.Status != RedemptionRequestStatus.Completed
                          && r.Status != RedemptionRequestStatus.Rejected
                          && r.Status != RedemptionRequestStatus.Cancelled, ct);

        var totalInvitations = await users
            .CountAsync(u => u.InvitedByUserId != null, ct);

        var totalNotificationsSent = await _context.Notifications.CountAsync(ct);

        var totalDeletedAccounts = await users
            .CountAsync(u => u.IsAccountDeleted, ct);

        var response = new AdminDashboardResponse(
            totalShopOwners,
            totalSellers,
            totalTechnicians,
            totalPendingApprovals,
            totalPointsEarned,
            Math.Abs(totalPointsRedeemed),
            totalSarRedeemed,
            totalActiveBarcodes,
            totalScans,
            pendingRedemptions,
            totalInvitations,
            totalNotificationsSent,
            totalDeletedAccounts
        );

        return Result.Success(response);
    }

    public async Task<Result<AdminUserAnalyticsResponse>> GetUserAnalyticsAsync(CancellationToken ct = default)
    {
        var users = _userRepository.Query()
            .Where(u => u.UserType != UserType.SystemAdmin);

        // Count by UserType
        var countByTypeRaw = await users
            .GroupBy(u => u.UserType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var countByType = countByTypeRaw.Select(x => new UserTypeCount(x.Type, x.Count)).ToList();

        // Count by RegistrationStatus
        var countByStatusRaw = await users
            .GroupBy(u => u.RegistrationStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var countByStatus = countByStatusRaw.Select(x => new RegistrationStatusCount(x.Status, x.Count)).ToList();

        // Count by Region (join through NationalAddress.CityId → City → Region)
        var countByRegionRaw = await (
            from u in users
            where u.NationalAddress != null && u.NationalAddress.CityId != null
            join c in _context.Cities on u.NationalAddress!.CityId equals c.Id
            join r in _context.Regions on c.RegionId equals r.Id
            group u by new { r.Id, r.NameAr, r.NameEn } into g
            select new { g.Key.Id, g.Key.NameAr, g.Key.NameEn, Count = g.Count() }
        ).ToListAsync(ct);
        var countByRegion = countByRegionRaw.Select(x => new RegionUserCount(x.Id, x.NameAr, x.NameEn, x.Count)).ToList();

        // Registration trend — last 12 months
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        var trendRaw = await users
            .Where(u => u.CreatedAt >= cutoff)
            .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);
        var trend = trendRaw.Select(x => new MonthlyCount(x.Year, x.Month, x.Count)).ToList();

        return Result.Success(new AdminUserAnalyticsResponse(countByType, countByStatus, countByRegion, trend));
    }

    public async Task<Result<AdminRegionAnalyticsResponse>> GetRegionAnalyticsAsync(CancellationToken ct = default)
    {
        var regions = await _context.Regions
            .AsNoTracking()
            .Where(r => r.IsActive)
            .Include(r => r.ZoneManager)
            .Include(r => r.Cities.Where(c => c.IsActive))
                .ThenInclude(c => c.ApprovalSalesMan)
            .OrderBy(r => r.NameAr)
            .ToListAsync(ct);

        var cityIds = regions.SelectMany(r => r.Cities).Select(c => c.Id).ToList();

        // Get user counts per city grouped by type
        var userCountsByCity = await _userRepository.Query()
            .Where(u => u.NationalAddress != null
                     && cityIds.Contains(u.NationalAddress.CityId!)
                     && (u.UserType == UserType.ShopOwner
                      || u.UserType == UserType.Seller
                      || u.UserType == UserType.Technician))
            .GroupBy(u => new { CityId = u.NationalAddress!.CityId, u.UserType })
            .Select(g => new { g.Key.CityId, g.Key.UserType, Count = g.Count() })
            .ToListAsync(ct);

        var regionItems = regions.Select(r =>
        {
            var regionCityIds = r.Cities.Select(c => c.Id).ToHashSet();
            var regionUserCounts = userCountsByCity.Where(x => regionCityIds.Contains(x.CityId!)).ToList();

            return new RegionAnalyticsItem(
                r.Id,
                r.NameAr,
                r.NameEn,
                r.ZoneManager?.Name,
                r.Cities.Count,
                regionUserCounts.Where(x => x.UserType == UserType.ShopOwner).Sum(x => x.Count),
                regionUserCounts.Where(x => x.UserType == UserType.Seller).Sum(x => x.Count),
                regionUserCounts.Where(x => x.UserType == UserType.Technician).Sum(x => x.Count),
                r.Cities.Select(c => new CityAnalyticsItem(
                    c.Id,
                    c.NameAr,
                    c.NameEn,
                    c.ApprovalSalesMan?.Name,
                    userCountsByCity
                        .Where(x => x.CityId == c.Id)
                        .Sum(x => x.Count)
                )).OrderBy(c => c.CityNameAr).ToList()
            );
        }).ToList();

        return Result.Success(new AdminRegionAnalyticsResponse(regionItems));
    }

    public async Task<Result<AdminPointsAnalyticsResponse>> GetPointsAnalyticsAsync(CancellationToken ct = default)
    {
        // Totals
        var transactionSums = await _context.WalletTransactions
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct);

        var totalEarned = transactionSums
            .Where(x => x.Type == WalletTransactionType.Earned || x.Type == WalletTransactionType.InvitationReward)
            .Sum(x => x.Total);

        var totalRedeemed = Math.Abs(
            transactionSums.FirstOrDefault(x => x.Type == WalletTransactionType.Redeemed)?.Total ?? 0);

        var totalBalance = await _context.Wallets.SumAsync(w => (decimal?)w.Balance ?? 0, ct);

        // Points earned by region
        var pointsByRegionRaw = await (
            from wt in _context.WalletTransactions
            join w in _context.Wallets on wt.WalletId equals w.Id
            join u in _userRepository.Query() on w.UserId equals u.Id
            where u.NationalAddress != null && u.NationalAddress.CityId != null
            join c in _context.Cities on u.NationalAddress!.CityId equals c.Id
            join r in _context.Regions on c.RegionId equals r.Id
            where wt.Type == WalletTransactionType.Earned || wt.Type == WalletTransactionType.InvitationReward
            group wt by new { r.Id, r.NameAr, r.NameEn } into g
            select new { g.Key.Id, g.Key.NameAr, g.Key.NameEn, Total = g.Sum(x => x.Amount) }
        ).ToListAsync(ct);
        var pointsByRegion = pointsByRegionRaw
            .Select(x => new RegionPointsItem(x.Id, x.NameAr, x.NameEn, x.Total)).ToList();

        // Points earned by representative (SalesMan)
        var pointsByRepRaw = await (
            from wt in _context.WalletTransactions
            join w in _context.Wallets on wt.WalletId equals w.Id
            join u in _userRepository.Query() on w.UserId equals u.Id
            join sm in _userRepository.Query() on u.AssignedSalesManId equals sm.Id
            where wt.Type == WalletTransactionType.Earned || wt.Type == WalletTransactionType.InvitationReward
            select new { SalesManId = sm.Id, SalesManName = sm.Name, UserId = u.Id, wt.Amount }
        ).ToListAsync(ct);

        var pointsByRep = pointsByRepRaw
            .GroupBy(x => new { x.SalesManId, x.SalesManName })
            .Select(g => new RepresentativePointsItem(
                g.Key.SalesManId,
                g.Key.SalesManName,
                g.Sum(x => x.Amount),
                g.Select(x => x.UserId).Distinct().Count()
            ))
            .ToList();

        // Points trend — last 12 months
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        var pointsTrendRaw = await _context.WalletTransactions
            .Where(t => (t.Type == WalletTransactionType.Earned || t.Type == WalletTransactionType.InvitationReward)
                     && t.CreatedAt >= cutoff)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(t => t.Amount) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);
        var pointsTrend = pointsTrendRaw.Select(x => new MonthlyDecimalCount(x.Year, x.Month, x.Total)).ToList();

        return Result.Success(new AdminPointsAnalyticsResponse(
            totalEarned, totalRedeemed, totalBalance,
            pointsByRegion, pointsByRep, pointsTrend
        ));
    }

    public async Task<Result<PaginatedResult<AdminPointsDetailItemResponse>>> GetPointsDetailsAsync(
        AdminPointsDetailQuery query, CancellationToken ct = default)
    {
        var baseQuery =
            from wt in _context.WalletTransactions
            join w in _context.Wallets on wt.WalletId equals w.Id
            join u in _userRepository.Query() on w.UserId equals u.Id
            select new { wt, u };

        if (!string.IsNullOrEmpty(query.UserId))
            baseQuery = baseQuery.Where(x => x.u.Id == query.UserId);

        if (!string.IsNullOrEmpty(query.RegionId))
        {
            var cityIdsInRegion = _context.Cities
                .Where(c => c.RegionId == query.RegionId)
                .Select(c => c.Id);

            baseQuery = baseQuery.Where(x => x.u.NationalAddress != null
                && cityIdsInRegion.Contains(x.u.NationalAddress.CityId!));
        }

        if (query.DateFrom.HasValue)
            baseQuery = baseQuery.Where(x => x.wt.CreatedAt >= query.DateFrom.Value);

        if (query.DateTo.HasValue)
            baseQuery = baseQuery.Where(x => x.wt.CreatedAt <= query.DateTo.Value);

        if (query.Type.HasValue)
            baseQuery = baseQuery.Where(x => x.wt.Type == query.Type.Value);

        var (page, pageSize) = Helpers.PaginationHelper.Normalize(query.Page, query.PageSize);

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(x => x.wt.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminPointsDetailItemResponse(
                x.wt.Id,
                x.u.Id,
                x.u.Name,
                x.u.MobileNumber,
                x.wt.Amount,
                x.wt.SarAmount,
                x.wt.Type,
                x.wt.Description,
                x.wt.CreatedAt
            ))
            .ToListAsync(ct);

        return Result.Success(new PaginatedResult<AdminPointsDetailItemResponse>(
            items, totalCount, page, pageSize));
    }

    public async Task<Result<List<AdminPointsDetailItemResponse>>> ExportPointsDetailsAsync(
        AdminPointsDetailQuery query, CancellationToken ct = default)
    {
        var baseQuery =
            from wt in _context.WalletTransactions
            join w in _context.Wallets on wt.WalletId equals w.Id
            join u in _userRepository.Query() on w.UserId equals u.Id
            select new { wt, u };

        if (!string.IsNullOrEmpty(query.UserId))
            baseQuery = baseQuery.Where(x => x.u.Id == query.UserId);

        if (!string.IsNullOrEmpty(query.RegionId))
        {
            var cityIdsInRegion = _context.Cities
                .Where(c => c.RegionId == query.RegionId)
                .Select(c => c.Id);

            baseQuery = baseQuery.Where(x => x.u.NationalAddress != null
                && cityIdsInRegion.Contains(x.u.NationalAddress.CityId!));
        }

        if (query.DateFrom.HasValue)
            baseQuery = baseQuery.Where(x => x.wt.CreatedAt >= query.DateFrom.Value);

        if (query.DateTo.HasValue)
            baseQuery = baseQuery.Where(x => x.wt.CreatedAt <= query.DateTo.Value);

        if (query.Type.HasValue)
            baseQuery = baseQuery.Where(x => x.wt.Type == query.Type.Value);

        var totalCount = await baseQuery.CountAsync(ct);
        if (totalCount > ExcelExportHelper.MaxExportRows)
            return Result.Failure<List<AdminPointsDetailItemResponse>>(ExportErrors.TooManyRows);

        var items = await baseQuery
            .OrderByDescending(x => x.wt.CreatedAt)
            .Select(x => new AdminPointsDetailItemResponse(
                x.wt.Id,
                x.u.Id,
                x.u.Name,
                x.u.MobileNumber,
                x.wt.Amount,
                x.wt.SarAmount,
                x.wt.Type,
                x.wt.Description,
                x.wt.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(items);
    }

    public async Task<Result<TopPerformersResponse>> GetTopPerformersAsync(int top = 10, CancellationToken ct = default)
    {
        top = Math.Clamp(top, 1, 100);

        // Top sellers by points earned
        var topSellers = await (
            from sr in _context.ScanRecords.Where(s => s.DeletedAt == null)
            join u in _userRepository.Query() on sr.UserId equals u.Id
            where sr.ScannerRole == Domain.Enums.ScannerRole.Seller
            group sr by new { u.Id, u.Name, u.MobileNumber } into g
            orderby g.Sum(x => x.PointsAwarded) descending
            select new { g.Key.Id, g.Key.Name, g.Key.MobileNumber, Total = g.Sum(x => x.PointsAwarded), Scans = g.Count() }
        ).Take(top).ToListAsync(ct);

        // Top technicians by points earned
        var topTechs = await (
            from sr in _context.ScanRecords.Where(s => s.DeletedAt == null)
            join u in _userRepository.Query() on sr.UserId equals u.Id
            where sr.ScannerRole == Domain.Enums.ScannerRole.Technician
            group sr by new { u.Id, u.Name, u.MobileNumber } into g
            orderby g.Sum(x => x.PointsAwarded) descending
            select new { g.Key.Id, g.Key.Name, g.Key.MobileNumber, Total = g.Sum(x => x.PointsAwarded), Scans = g.Count() }
        ).Take(top).ToListAsync(ct);

        // Get region info for these users
        var allUserIds = topSellers.Select(x => x.Id).Concat(topTechs.Select(x => x.Id)).Distinct().ToList();
        var userRegionsList = await (
            from u in _userRepository.Query()
            where allUserIds.Contains(u.Id)
               && u.NationalAddress != null && u.NationalAddress.CityId != null
            join c in _context.Cities on u.NationalAddress!.CityId equals c.Id
            join r in _context.Regions on c.RegionId equals r.Id
            select new { UserId = u.Id, r.NameAr, r.NameEn }
        ).ToListAsync(ct);
        var userRegions = userRegionsList.ToDictionary(x => x.UserId, x => (x.NameAr, x.NameEn));

        var sellerItems = topSellers.Select(x =>
        {
            userRegions.TryGetValue(x.Id, out var region);
            return new TopPerformerItem(x.Id, x.Name, x.MobileNumber, region.NameAr ?? string.Empty, region.NameEn ?? string.Empty, x.Total, x.Scans);
        }).ToList();

        var techItems = topTechs.Select(x =>
        {
            userRegions.TryGetValue(x.Id, out var region);
            return new TopPerformerItem(x.Id, x.Name, x.MobileNumber, region.NameAr ?? string.Empty, region.NameEn ?? string.Empty, x.Total, x.Scans);
        }).ToList();

        return Result.Success(new TopPerformersResponse(sellerItems, techItems));
    }

    public async Task<Result<PaginatedResult<InactiveUserItem>>> GetInactiveUsersAsync(
        InactiveUsersQuery query, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-query.InactiveDays);

        // Only load user IDs who scanned AFTER cutoff (active users) — much smaller set than all scan records
        var activeUserIds = await _context.ScanRecords
            .Where(s => s.DeletedAt == null && s.CreatedAt >= cutoff)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync(ct);

        var activeUserIdSet = activeUserIds.ToHashSet();

        // Load last scan dates only for eligible inactive users (filtered set, not all records)
        var lastScans = await _context.ScanRecords
            .Where(s => s.DeletedAt == null && !activeUserIdSet.Contains(s.UserId))
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, LastScan = g.Max(s => s.CreatedAt) })
            .ToDictionaryAsync(x => x.UserId, x => x.LastScan, ct);

        var now = DateTime.UtcNow;

        var eligibleUsers = await _userRepository.Query()
            .Where(u => (u.UserType == UserType.Seller || u.UserType == UserType.Technician)
               && u.RegistrationStatus == RegistrationStatus.Approved
               && !u.IsDisabled
               && !activeUserIdSet.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.MobileNumber, u.UserType, u.CreatedAt })
            .ToListAsync(ct);

        // Enrich with last scan date and sort
        var filtered = eligibleUsers
            .Select(u => new
            {
                u.Id, u.Name, u.MobileNumber, u.UserType, u.CreatedAt,
                LastScan = lastScans.TryGetValue(u.Id, out var ls) ? (DateTime?)ls : null
            })
            .OrderBy(x => x.LastScan)
            .ToList();

        var totalCount = filtered.Count;

        var items = filtered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new InactiveUserItem(
                x.Id,
                x.Name,
                x.MobileNumber,
                x.UserType,
                x.LastScan,
                x.LastScan.HasValue
                    ? (int)(now - x.LastScan.Value).TotalDays
                    : (int)(now - x.CreatedAt).TotalDays
            )).ToList();

        return Result.Success(new PaginatedResult<InactiveUserItem>(
            items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<List<InactiveUserItem>>> ExportInactiveUsersAsync(
        InactiveUsersQuery query, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-query.InactiveDays);

        var activeUserIds = await _context.ScanRecords
            .Where(s => s.DeletedAt == null && s.CreatedAt >= cutoff)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync(ct);

        var activeUserIdSet = activeUserIds.ToHashSet();

        var lastScans = await _context.ScanRecords
            .Where(s => s.DeletedAt == null && !activeUserIdSet.Contains(s.UserId))
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, LastScan = g.Max(s => s.CreatedAt) })
            .ToDictionaryAsync(x => x.UserId, x => x.LastScan, ct);

        var now = DateTime.UtcNow;

        var eligibleUsers = await _userRepository.Query()
            .Where(u => (u.UserType == UserType.Seller || u.UserType == UserType.Technician)
               && u.RegistrationStatus == RegistrationStatus.Approved
               && !u.IsDisabled
               && !activeUserIdSet.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.MobileNumber, u.UserType, u.CreatedAt })
            .ToListAsync(ct);

        if (eligibleUsers.Count > ExcelExportHelper.MaxExportRows)
            return Result.Failure<List<InactiveUserItem>>(ExportErrors.TooManyRows);

        var items = eligibleUsers
            .Select(u => new
            {
                u.Id, u.Name, u.MobileNumber, u.UserType, u.CreatedAt,
                LastScan = lastScans.TryGetValue(u.Id, out var ls) ? (DateTime?)ls : null
            })
            .OrderBy(x => x.LastScan)
            .Select(x => new InactiveUserItem(
                x.Id,
                x.Name,
                x.MobileNumber,
                x.UserType,
                x.LastScan,
                x.LastScan.HasValue
                    ? (int)(now - x.LastScan.Value).TotalDays
                    : (int)(now - x.CreatedAt).TotalDays))
            .ToList();

        return Result.Success(items);
    }

    public async Task<Result<BarcodeAnalyticsResponse>> GetBarcodeAnalyticsAsync(CancellationToken ct = default)
    {
        var statusCounts = await _context.ProductBarcodes
            .Where(b => b.DeletedAt == null)
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalGenerated = statusCounts.Sum(x => x.Count);
        var totalAvailable = statusCounts.FirstOrDefault(x => x.Status == BarcodeStatus.Available)?.Count ?? 0;
        var totalSellerScanned = statusCounts.FirstOrDefault(x => x.Status == BarcodeStatus.SellerScanned)?.Count ?? 0;
        var totalTechScanned = statusCounts.FirstOrDefault(x => x.Status == BarcodeStatus.TechnicianScanned)?.Count ?? 0;
        var totalConsumed = statusCounts.FirstOrDefault(x => x.Status == BarcodeStatus.Consumed)?.Count ?? 0;
        var scanRate = totalGenerated > 0
            ? Math.Round((decimal)(totalGenerated - totalAvailable) / totalGenerated * 100, 1)
            : 0;

        var topProductsRaw = await (
            from b in _context.ProductBarcodes.Where(b => b.DeletedAt == null)
            join p in _context.Products.Where(p => p.DeletedAt == null) on b.ProductId equals p.Id
            group b by new { p.Id, p.Name, p.ProductCode } into g
            orderby g.Count() descending
            select new
            {
                g.Key.Id,
                g.Key.Name,
                g.Key.ProductCode,
                Total = g.Count(),
                Scanned = g.Count(x => x.Status != BarcodeStatus.Available),
                Consumed = g.Count(x => x.Status == BarcodeStatus.Consumed)
            }
        ).Take(20).ToListAsync(ct);
        var topProducts = topProductsRaw
            .Select(x => new ProductBarcodeItem(x.Id, x.Name, x.ProductCode, x.Total, x.Scanned, x.Consumed))
            .ToList();

        return Result.Success(new BarcodeAnalyticsResponse(
            totalGenerated, totalAvailable, totalSellerScanned, totalTechScanned, totalConsumed,
            scanRate, topProducts));
    }

    public async Task<Result<RedemptionAnalyticsResponse>> GetRedemptionAnalyticsAsync(CancellationToken ct = default)
    {
        var requests = _context.RedemptionRequests.AsQueryable();

        var countByStatusRaw = await requests
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), TotalSar = g.Sum(r => r.SarAmount) })
            .ToListAsync(ct);
        var countByStatus = countByStatusRaw
            .Select(x => new RedemptionStatusCount(x.Status, x.Count, x.TotalSar)).ToList();

        var countByMethodRaw = await requests
            .GroupBy(r => r.Method)
            .Select(g => new { Method = g.Key, Count = g.Count(), TotalSar = g.Sum(r => r.SarAmount) })
            .ToListAsync(ct);
        var countByMethod = countByMethodRaw
            .Select(x => new RedemptionMethodCount(x.Method, x.Count, x.TotalSar)).ToList();

        var totalSarRedeemed = countByStatus
            .Where(x => x.Status == RedemptionRequestStatus.Completed)
            .Sum(x => x.TotalSar);

        // Average processing time for completed requests
        var completedRequests = await requests
            .Where(r => r.Status == RedemptionRequestStatus.Completed && r.UpdatedAt.HasValue)
            .Select(r => new { r.CreatedAt, r.UpdatedAt })
            .ToListAsync(ct);

        var avgDays = completedRequests.Count > 0
            ? Math.Round(completedRequests.Average(r => (r.UpdatedAt!.Value - r.CreatedAt).TotalDays), 1)
            : 0;

        var pendingCount = countByStatus
            .Where(x => x.Status != RedemptionRequestStatus.Completed
                     && x.Status != RedemptionRequestStatus.Rejected
                     && x.Status != RedemptionRequestStatus.Cancelled)
            .Sum(x => x.Count);

        // Redemption trend — last 12 months (completed SAR)
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        var trendRaw2 = await requests
            .Where(r => r.Status == RedemptionRequestStatus.Completed && r.CreatedAt >= cutoff)
            .GroupBy(r => new { r.CreatedAt.Year, r.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(r => r.SarAmount) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);
        var trend = trendRaw2.Select(x => new MonthlyDecimalCount(x.Year, x.Month, x.Total)).ToList();

        return Result.Success(new RedemptionAnalyticsResponse(
            countByStatus, countByMethod, totalSarRedeemed,
            (decimal)avgDays, pendingCount, trend));
    }

    public async Task<Result<SalesManPerformanceResponse>> GetSalesManPerformanceAsync(CancellationToken ct = default)
    {
        var salesMen = await _userRepository.Query()
            .Where(u => u.UserType == UserType.SalesMan && !u.IsDisabled)
            .ToListAsync(ct);

        var salesManIds = salesMen.Select(s => s.Id).ToList();

        // Assigned users grouped by SalesMan and status
        var assignedStats = await _userRepository.Query()
            .Where(u => u.AssignedSalesManId != null && salesManIds.Contains(u.AssignedSalesManId))
            .GroupBy(u => new { u.AssignedSalesManId, u.RegistrationStatus })
            .Select(g => new { g.Key.AssignedSalesManId, g.Key.RegistrationStatus, Count = g.Count() })
            .ToListAsync(ct);

        // Points earned per SalesMan's users
        var pointsBySalesMan = await (
            from wt in _context.WalletTransactions
            join w in _context.Wallets on wt.WalletId equals w.Id
            join u in _userRepository.Query() on w.UserId equals u.Id
            where u.AssignedSalesManId != null
               && salesManIds.Contains(u.AssignedSalesManId)
               && (wt.Type == WalletTransactionType.Earned || wt.Type == WalletTransactionType.InvitationReward)
            group wt by u.AssignedSalesManId into g
            select new { SalesManId = g.Key, Total = g.Sum(x => x.Amount) }
        ).ToDictionaryAsync(x => x.SalesManId!, x => x.Total, ct);

        // City count per SalesMan
        var cityCounts = await _context.Cities
            .Where(c => c.ApprovalSalesManId != null && salesManIds.Contains(c.ApprovalSalesManId))
            .GroupBy(c => c.ApprovalSalesManId)
            .Select(g => new { SalesManId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SalesManId!, x => x.Count, ct);

        var items = salesMen.Select(sm =>
        {
            var stats = assignedStats.Where(x => x.AssignedSalesManId == sm.Id).ToList();
            var approved = stats.Where(x => x.RegistrationStatus == RegistrationStatus.Approved).Sum(x => x.Count);
            var pending = stats
                .Where(x => x.RegistrationStatus == RegistrationStatus.PendingSalesman
                          || x.RegistrationStatus == RegistrationStatus.PendingZoneManager)
                .Sum(x => x.Count);

            return new SalesManPerformanceItem(
                sm.Id,
                sm.Name,
                sm.MobileNumber,
                stats.Sum(x => x.Count),
                approved,
                pending,
                pointsBySalesMan.TryGetValue(sm.Id, out var pts) ? pts : 0,
                cityCounts.TryGetValue(sm.Id, out var cc) ? cc : 0
            );
        }).OrderByDescending(x => x.TotalPointsEarned).ToList();

        return Result.Success(new SalesManPerformanceResponse(items));
    }

    public async Task<Result<RevenueAnalyticsResponse>> GetRevenueAnalyticsAsync(CancellationToken ct = default)
    {
        // SAR balances
        var wallets = _context.Wallets.AsQueryable();
        var totalSarLiability = await wallets.SumAsync(w => (decimal?)w.SarBalance ?? 0, ct);
        var totalSarHeld = await wallets.SumAsync(w => (decimal?)w.HeldSarBalance ?? 0, ct);
        var totalPointsOutstanding = await wallets.SumAsync(w => (decimal?)w.Balance ?? 0, ct);

        var totalSarPaidOut = await _context.RedemptionRequests
            .Where(r => r.Status == RedemptionRequestStatus.Completed)
            .SumAsync(r => (decimal?)r.SarAmount ?? 0, ct);

        // Volume by transaction type
        var volumeByTypeRaw = await _context.WalletTransactions
            .GroupBy(t => t.Type)
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count(),
                TotalPoints = g.Sum(t => t.Amount),
                TotalSar = g.Sum(t => t.SarAmount)
            })
            .ToListAsync(ct);

        var volumeByType = volumeByTypeRaw
            .Select(g => new TransactionTypeVolume(g.Type.ToString(), g.Count, g.TotalPoints, g.TotalSar))
            .ToList();

        // Payout trend — last 12 months
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        var payoutTrendRaw = await _context.RedemptionRequests
            .Where(r => r.Status == RedemptionRequestStatus.Completed && r.CreatedAt >= cutoff)
            .GroupBy(r => new { r.CreatedAt.Year, r.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(r => r.SarAmount) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);
        var payoutTrend = payoutTrendRaw
            .Select(x => new MonthlyDecimalCount(x.Year, x.Month, x.Total)).ToList();

        return Result.Success(new RevenueAnalyticsResponse(
            totalSarLiability, totalSarHeld, totalSarPaidOut,
            totalPointsOutstanding, volumeByType, payoutTrend));
    }

    public async Task<Result<InvitationAnalyticsResponse>> GetInvitationAnalyticsAsync(
        int topInviters = 10, CancellationToken ct = default)
    {
        topInviters = Math.Clamp(topInviters, 1, 100);

        // All users who were invited (have InvitedByUserId set)
        var invitedUsers = await _userRepository.Query()
            .Where(u => u.InvitedByUserId != null)
            .Select(u => new { u.Id, u.InvitedByUserId, u.RegistrationStatus, u.CreatedAt })
            .ToListAsync(ct);

        var totalInvitationsSent = invitedUsers.Count;
        var totalAccepted = invitedUsers.Count(u => u.RegistrationStatus == RegistrationStatus.Approved);
        var totalPending = invitedUsers.Count(u =>
            u.RegistrationStatus == RegistrationStatus.PendingSalesman
            || u.RegistrationStatus == RegistrationStatus.PendingZoneManager);
        var conversionRate = totalInvitationsSent > 0
            ? Math.Round((decimal)totalAccepted / totalInvitationsSent * 100, 1)
            : 0;

        // Top inviters
        var inviterGroups = invitedUsers
            .GroupBy(u => u.InvitedByUserId!)
            .Select(g => new
            {
                InviterId = g.Key,
                TotalInvited = g.Count(),
                ApprovedCount = g.Count(x => x.RegistrationStatus == RegistrationStatus.Approved)
            })
            .OrderByDescending(x => x.ApprovedCount)
            .Take(topInviters)
            .ToList();

        var inviterIds = inviterGroups.Select(x => x.InviterId).ToList();

        var inviterUsers = await _userRepository.Query()
            .Where(u => inviterIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.MobileNumber })
            .ToDictionaryAsync(u => u.Id, ct);

        // Points earned from invitation rewards per inviter
        var inviterRewardPoints = await (
            from wt in _context.WalletTransactions
            join w in _context.Wallets on wt.WalletId equals w.Id
            where wt.Type == WalletTransactionType.InvitationReward
               && inviterIds.Contains(w.UserId)
            group wt by w.UserId into g
            select new { UserId = g.Key, Total = g.Sum(x => x.Amount) }
        ).ToDictionaryAsync(x => x.UserId, x => x.Total, ct);

        var topInviterItems = inviterGroups.Select(g =>
        {
            inviterUsers.TryGetValue(g.InviterId, out var user);
            inviterRewardPoints.TryGetValue(g.InviterId, out var pts);
            return new TopInviterItem(
                g.InviterId,
                user?.Name ?? string.Empty,
                user?.MobileNumber ?? string.Empty,
                g.TotalInvited,
                g.ApprovedCount,
                pts
            );
        }).ToList();

        // Total reward points & SAR spent on invitation rewards (all users)
        var rewardTotals = await _context.WalletTransactions
            .Where(t => t.Type == WalletTransactionType.InvitationReward)
            .GroupBy(t => 1)
            .Select(g => new { Points = g.Sum(t => t.Amount), Sar = g.Sum(t => t.SarAmount) })
            .FirstOrDefaultAsync(ct);

        var totalRewardPoints = rewardTotals?.Points ?? 0;
        var totalRewardSar = rewardTotals?.Sar ?? 0;

        // Invitation trend — last 12 months
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        var trendData = invitedUsers
            .Where(u => u.CreatedAt >= cutoff)
            .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
            .Select(g => new MonthlyCount(g.Key.Year, g.Key.Month, g.Count()))
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();

        return Result.Success(new InvitationAnalyticsResponse(
            totalInvitationsSent, totalAccepted, totalPending, conversionRate,
            topInviterItems, totalRewardPoints, totalRewardSar, trendData));
    }

    public async Task<Result<NotificationAnalyticsResponse>> GetNotificationAnalyticsAsync(
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfDay = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);

        var notifications = _context.Notifications.AsQueryable();

        var totalAllTime = await notifications.CountAsync(ct);

        var totalThisMonth = await notifications
            .CountAsync(n => n.CreatedAt >= startOfMonth, ct);

        var totalToday = await notifications
            .CountAsync(n => n.CreatedAt >= startOfDay, ct);

        // Count by type
        var countByTypeRaw = await notifications
            .GroupBy(n => n.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var countByType = countByTypeRaw
            .Select(x => new NotificationTypeCount(x.Type, x.Count)).ToList();

        // Read vs unread
        var totalRead = await notifications.CountAsync(n => n.IsRead, ct);
        var totalUnread = totalAllTime - totalRead;
        var readRate = totalAllTime > 0
            ? Math.Round((decimal)totalRead / totalAllTime * 100, 1)
            : 0;

        // Admin-sent vs system-triggered
        // AdminMessage type = admin-sent; everything else = system-triggered
        var adminSentCount = countByTypeRaw
            .FirstOrDefault(x => x.Type == Domain.Enums.NotificationType.AdminMessage)?.Count ?? 0;
        var systemTriggeredCount = totalAllTime - adminSentCount;

        // Notification trend — last 12 months
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        var trendRaw = await notifications
            .Where(n => n.CreatedAt >= cutoff)
            .GroupBy(n => new { n.CreatedAt.Year, n.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);
        var trend = trendRaw.Select(x => new MonthlyCount(x.Year, x.Month, x.Count)).ToList();

        return Result.Success(new NotificationAnalyticsResponse(
            totalAllTime, totalThisMonth, totalToday,
            countByType, totalRead, totalUnread, readRate,
            adminSentCount, systemTriggeredCount, trend));
    }
}
