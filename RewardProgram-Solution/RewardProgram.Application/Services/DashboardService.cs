using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Dashboard;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _context;
    private readonly IUserRepository _userRepository;

    public DashboardService(
        IApplicationDbContext context,
        IUserRepository userRepository)
    {
        _context = context;
        _userRepository = userRepository;
    }

    public async Task<Result<DashboardResponse>> GetDashboardAsync(
        string userId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure<DashboardResponse>(AuthErrors.UserNotFound);

        // Wallet balance
        var wallet = await _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);

        var points = wallet?.Balance ?? 0;
        var sarBalance = wallet?.SarBalance ?? 0;

        // Reward settings
        var settings = await _context.RewardSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var minimumRedemptionPoints = settings?.MinimumRedemptionPoints ?? 1000m;

        var pointsToRedeem = Math.Max(0, minimumRedemptionPoints - points);
        var canRedeem = points >= minimumRedemptionPoints;

        // Recent 10 transactions
        var recentTransactions = wallet is null
            ? []
            : await _context.WalletTransactions
                .AsNoTracking()
                .Where(t => t.WalletId == wallet.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .Select(t => new DashboardTransactionItem(
                    t.Id,
                    t.Amount,
                    t.SarAmount,
                    t.Type,
                    t.Description,
                    t.CreatedAt
                ))
                .ToListAsync(ct);

        return Result.Success(new DashboardResponse(
            user.Name,
            points,
            sarBalance,
            minimumRedemptionPoints,
            pointsToRedeem,
            canRedeem,
            recentTransactions
        ));
    }

    public async Task<Result<ShopOwnerDashboardResponse>> GetShopOwnerDashboardAsync(
        string userId, CancellationToken ct = default)
    {
        var user = await _userRepository.FindByIdAsync(userId, ct);
        if (user is null)
            return Result.Failure<ShopOwnerDashboardResponse>(AuthErrors.UserNotFound);

        // Get ShopOwner's CustomerCode
        var profile = await _context.ShopOwnerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (profile is null)
            return Result.Failure<ShopOwnerDashboardResponse>(AuthErrors.UserNotFound);

        // Get ShopData
        var shopData = await _context.ShopData
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CustomerCode == profile.CustomerCode, ct);

        // City name
        var cityName = shopData is not null
            ? await _context.Cities.AsNoTracking()
                .Where(c => c.Id == shopData.CityId)
                .Select(c => c.NameAr)
                .FirstOrDefaultAsync(ct) ?? string.Empty
            : string.Empty;

        var shop = shopData is not null
            ? new ShopOwnerShopInfo(
                shopData.CustomerCode,
                shopData.StoreName,
                shopData.VAT,
                shopData.CRN,
                shopData.ShopImageUrl,
                shopData.ShortAddress,
                shopData.District,
                cityName,
                shopData.Street,
                shopData.PostalCode,
                shopData.BuildingNumber,
                shopData.SubNumber)
            : null!;

        // Get sellers linked to the same CustomerCode
        var sellerUserIds = await _context.SellerProfiles
            .AsNoTracking()
            .Where(s => s.CustomerCode == profile.CustomerCode)
            .Select(s => s.UserId)
            .ToListAsync(ct);

        var sellers = new List<ShopOwnerSellerItem>();
        foreach (var sellerId in sellerUserIds)
        {
            var seller = await _userRepository.FindByIdAsync(sellerId, ct);
            if (seller is not null)
            {
                sellers.Add(new ShopOwnerSellerItem(
                    seller.Id,
                    seller.Name,
                    seller.MobileNumber,
                    seller.CreatedAt));
            }
        }

        return Result.Success(new ShopOwnerDashboardResponse(
            user.Name,
            user.ProfileImageUrl,
            shop,
            sellers,
            sellers.Count
        ));
    }
}
