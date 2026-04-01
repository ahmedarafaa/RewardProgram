using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Analytics;
using RewardProgram.Application.Contracts.Admin.Dashboard;
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
            pendingRedemptions
        );

        return Result.Success(response);
    }

    public async Task<Result<AdminUserAnalyticsResponse>> GetUserAnalyticsAsync(CancellationToken ct = default)
    {
        var users = _userRepository.Query()
            .Where(u => u.UserType != UserType.SystemAdmin);

        // Count by UserType
        var countByType = await users
            .GroupBy(u => u.UserType)
            .Select(g => new UserTypeCount(g.Key, g.Count()))
            .ToListAsync(ct);

        // Count by RegistrationStatus
        var countByStatus = await users
            .GroupBy(u => u.RegistrationStatus)
            .Select(g => new RegistrationStatusCount(g.Key, g.Count()))
            .ToListAsync(ct);

        // Count by Region (join through NationalAddress.CityId → City → Region)
        var countByRegion = await (
            from u in users
            join c in _context.Cities on u.NationalAddress!.CityId equals c.Id
            join r in _context.Regions on c.RegionId equals r.Id
            group u by new { r.Id, r.NameAr, r.NameEn } into g
            select new RegionUserCount(g.Key.Id, g.Key.NameAr, g.Key.NameEn, g.Count())
        ).ToListAsync(ct);

        // Registration trend — last 12 months
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        var trend = await users
            .Where(u => u.CreatedAt >= cutoff)
            .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
            .Select(g => new MonthlyCount(g.Key.Year, g.Key.Month, g.Count()))
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

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
        var pointsByRegion = await (
            from wt in _context.WalletTransactions
            join w in _context.Wallets on wt.WalletId equals w.Id
            join c in _context.Cities on
                (from u in _userRepository.Query()
                 where u.Id == w.UserId
                 select u.NationalAddress!.CityId).FirstOrDefault() equals c.Id
            join r in _context.Regions on c.RegionId equals r.Id
            where wt.Type == WalletTransactionType.Earned || wt.Type == WalletTransactionType.InvitationReward
            group wt by new { r.Id, r.NameAr, r.NameEn } into g
            select new RegionPointsItem(g.Key.Id, g.Key.NameAr, g.Key.NameEn, g.Sum(x => x.Amount))
        ).ToListAsync(ct);

        // Points earned by representative (SalesMan)
        var pointsByRep = await (
            from wt in _context.WalletTransactions
            join w in _context.Wallets on wt.WalletId equals w.Id
            join u in _userRepository.Query() on w.UserId equals u.Id
            join sm in _userRepository.Query() on u.AssignedSalesManId equals sm.Id
            where wt.Type == WalletTransactionType.Earned || wt.Type == WalletTransactionType.InvitationReward
            group new { wt, u } by new { sm.Id, sm.Name } into g
            select new RepresentativePointsItem(
                g.Key.Id,
                g.Key.Name,
                g.Sum(x => x.wt.Amount),
                g.Select(x => x.u.Id).Distinct().Count()
            )
        ).ToListAsync(ct);

        // Points trend — last 12 months
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        var pointsTrend = await _context.WalletTransactions
            .Where(t => (t.Type == WalletTransactionType.Earned || t.Type == WalletTransactionType.InvitationReward)
                     && t.CreatedAt >= cutoff)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new MonthlyDecimalCount(g.Key.Year, g.Key.Month, g.Sum(t => t.Amount)))
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(ct);

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

            baseQuery = baseQuery.Where(x => cityIdsInRegion.Contains(x.u.NationalAddress!.CityId!));
        }

        if (query.DateFrom.HasValue)
            baseQuery = baseQuery.Where(x => x.wt.CreatedAt >= query.DateFrom.Value);

        if (query.DateTo.HasValue)
            baseQuery = baseQuery.Where(x => x.wt.CreatedAt <= query.DateTo.Value);

        if (query.Type.HasValue)
            baseQuery = baseQuery.Where(x => x.wt.Type == query.Type.Value);

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(x => x.wt.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
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
            items, totalCount, query.Page, query.PageSize));
    }
}
