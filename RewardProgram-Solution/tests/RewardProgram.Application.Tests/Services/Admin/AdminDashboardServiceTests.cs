using FluentAssertions;
using NSubstitute;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Services.Admin;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;
using static RewardProgram.Application.Tests.TestHelpers.AsyncQueryableExtensions;

namespace RewardProgram.Application.Tests.Services.Admin;

public class AdminDashboardServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly IUserRepository _userRepo;
    private readonly AdminDashboardService _sut;

    public AdminDashboardServiceTests()
    {
        _context = TestDbContext.Create();
        _userRepo = Substitute.For<IUserRepository>();
        _sut = new AdminDashboardService(_context, _userRepo);
    }

    public void Dispose() => _context.Dispose();

    private void SetupUsers(params ApplicationUser[] users)
    {
        _userRepo.Query().Returns(users.AsAsyncQueryable());
    }

    // ── GetDashboard ──

    [Fact]
    public async Task GetDashboard_EmptyData_ShouldReturnZeros()
    {
        SetupUsers();

        var result = await _sut.GetDashboardAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalShopOwners.Should().Be(0);
        result.Value.TotalSellers.Should().Be(0);
        result.Value.TotalTechnicians.Should().Be(0);
        result.Value.TotalPendingApprovals.Should().Be(0);
        result.Value.TotalPointsEarned.Should().Be(0);
        result.Value.TotalScans.Should().Be(0);
    }

    [Fact]
    public async Task GetDashboard_WithUsers_ShouldCountByType()
    {
        SetupUsers(
            new ApplicationUser { Id = "1", Name = "S1", MobileNumber = "1", UserType = UserType.Seller, RegistrationStatus = RegistrationStatus.Approved },
            new ApplicationUser { Id = "2", Name = "S2", MobileNumber = "2", UserType = UserType.Seller, RegistrationStatus = RegistrationStatus.Approved },
            new ApplicationUser { Id = "3", Name = "T1", MobileNumber = "3", UserType = UserType.Technician, RegistrationStatus = RegistrationStatus.Approved },
            new ApplicationUser { Id = "4", Name = "O1", MobileNumber = "4", UserType = UserType.ShopOwner, RegistrationStatus = RegistrationStatus.Approved },
            new ApplicationUser { Id = "5", Name = "P1", MobileNumber = "5", UserType = UserType.Seller, RegistrationStatus = RegistrationStatus.PendingSalesman }
        );

        var result = await _sut.GetDashboardAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSellers.Should().Be(3); // includes pending seller
        result.Value.TotalTechnicians.Should().Be(1);
        result.Value.TotalShopOwners.Should().Be(1);
        result.Value.TotalPendingApprovals.Should().Be(1);
    }

    [Fact]
    public async Task GetDashboard_WithTransactions_ShouldSumPointsCorrectly()
    {
        SetupUsers();

        var wallet = new Wallet { UserId = "u1", Balance = 200, SarBalance = 20 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id, Amount = 100, Type = WalletTransactionType.Earned,
            SarRate = 10, SarAmount = 10, RemainingAmount = 100
        });
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id, Amount = 50, Type = WalletTransactionType.InvitationReward,
            SarRate = 10, SarAmount = 5, RemainingAmount = 50
        });
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id, Amount = -30, Type = WalletTransactionType.Redeemed,
            SarRate = 10, SarAmount = -3, RemainingAmount = 0
        });
        await _context.SaveChangesAsync();

        var result = await _sut.GetDashboardAsync();

        result.Value.TotalPointsEarned.Should().Be(150); // 100 + 50 (earned + invitation)
        result.Value.TotalPointsRedeemed.Should().Be(30); // abs(-30)
    }

    [Fact]
    public async Task GetDashboard_WithBarcodes_ShouldCountActiveBarcodes()
    {
        SetupUsers();

        var product = new Product { Name = "P1", ProductCode = "PC1", PointValue = 100, Price = 50 };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        _context.ProductBarcodes.Add(new ProductBarcode { Code = "B1", ProductId = product.Id, Status = BarcodeStatus.Available });
        _context.ProductBarcodes.Add(new ProductBarcode { Code = "B2", ProductId = product.Id, Status = BarcodeStatus.Available });
        _context.ProductBarcodes.Add(new ProductBarcode { Code = "B3", ProductId = product.Id, Status = BarcodeStatus.Consumed });
        _context.ProductBarcodes.Add(new ProductBarcode { Code = "B4", ProductId = product.Id, Status = BarcodeStatus.Available, DeletedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await _sut.GetDashboardAsync();

        result.Value.TotalActiveBarcodes.Should().Be(2); // B1, B2 (not consumed, not deleted)
    }

    [Fact]
    public async Task GetDashboard_WithPendingRedemptions_ShouldCount()
    {
        SetupUsers();

        var user = new ApplicationUser { Id = "u1", Name = "U1", MobileNumber = "1", UserName = "1" };
        _context.Set<ApplicationUser>().Add(user);

        _context.RedemptionRequests.Add(new RedemptionRequest
        {
            UserId = "u1", Status = RedemptionRequestStatus.PendingSalesMan,
            PointsAmount = 100, SarRate = 10, SarAmount = 10, Method = RedemptionMethod.Cash
        });
        _context.RedemptionRequests.Add(new RedemptionRequest
        {
            UserId = "u1", Status = RedemptionRequestStatus.PendingZoneManager,
            PointsAmount = 200, SarRate = 10, SarAmount = 20, Method = RedemptionMethod.Cash
        });
        _context.RedemptionRequests.Add(new RedemptionRequest
        {
            UserId = "u1", Status = RedemptionRequestStatus.Completed,
            PointsAmount = 300, SarRate = 10, SarAmount = 30, Method = RedemptionMethod.Cash
        });
        await _context.SaveChangesAsync();

        var result = await _sut.GetDashboardAsync();

        result.Value.PendingRedemptions.Should().Be(2);
    }

    // Note: GetUserAnalyticsAsync, GetRegionAnalyticsAsync, GetPointsAnalyticsAsync, GetTopPerformersAsync,
    // GetInactiveUsersAsync, and GetSalesManPerformanceAsync require cross-provider joins between
    // IUserRepository.Query() and IApplicationDbContext entities, which cannot be tested with mock IQueryable.
    // These are covered by the E2E Postman tests against a real database.

    // ── GetBarcodeAnalytics ──

    [Fact]
    public async Task GetBarcodeAnalytics_ShouldCalculateScanRate()
    {
        var product = new Product { Name = "P1", ProductCode = "PC1", PointValue = 100, Price = 50 };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // 10 barcodes: 6 available, 2 seller-scanned, 2 consumed
        for (int i = 0; i < 6; i++)
            _context.ProductBarcodes.Add(new ProductBarcode { Code = $"A{i}", ProductId = product.Id, Status = BarcodeStatus.Available });
        for (int i = 0; i < 2; i++)
            _context.ProductBarcodes.Add(new ProductBarcode { Code = $"S{i}", ProductId = product.Id, Status = BarcodeStatus.SellerScanned });
        for (int i = 0; i < 2; i++)
            _context.ProductBarcodes.Add(new ProductBarcode { Code = $"C{i}", ProductId = product.Id, Status = BarcodeStatus.Consumed });
        await _context.SaveChangesAsync();

        var result = await _sut.GetBarcodeAnalyticsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalGenerated.Should().Be(10);
        result.Value.TotalAvailable.Should().Be(6);
        result.Value.TotalSellerScanned.Should().Be(2);
        result.Value.TotalConsumed.Should().Be(2);
        result.Value.ScanRate.Should().Be(40); // (10 - 6) / 10 * 100
    }

    // ── GetRedemptionAnalytics ──

    [Fact]
    public async Task GetRedemptionAnalytics_ShouldGroupByStatusAndMethod()
    {
        var user = new ApplicationUser { Id = "u1", Name = "U1", MobileNumber = "1", UserName = "1" };
        _context.Set<ApplicationUser>().Add(user);

        _context.RedemptionRequests.Add(new RedemptionRequest
        {
            UserId = "u1", Status = RedemptionRequestStatus.Completed, Method = RedemptionMethod.Cash,
            PointsAmount = 100, SarRate = 10, SarAmount = 10, CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow.AddDays(2)
        });
        _context.RedemptionRequests.Add(new RedemptionRequest
        {
            UserId = "u1", Status = RedemptionRequestStatus.Rejected, Method = RedemptionMethod.BankTransfer,
            PointsAmount = 200, SarRate = 10, SarAmount = 20, CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = await _sut.GetRedemptionAnalyticsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.CountByStatus.Should().HaveCount(2);
        result.Value.CountByMethod.Should().HaveCount(2);
        result.Value.TotalSarRedeemed.Should().Be(10); // only completed
        result.Value.PendingCount.Should().Be(0);
    }

    // ── GetRevenueAnalytics ──

    [Fact]
    public async Task GetRevenueAnalytics_ShouldCalculateSarLiabilities()
    {
        SetupUsers();

        _context.Wallets.Add(new Wallet { UserId = "u1", Balance = 500, SarBalance = 50, HeldSarBalance = 10 });
        _context.Wallets.Add(new Wallet { UserId = "u2", Balance = 300, SarBalance = 30, HeldSarBalance = 5 });
        await _context.SaveChangesAsync();

        var user = new ApplicationUser { Id = "u1", Name = "U1", MobileNumber = "1", UserName = "1" };
        _context.Set<ApplicationUser>().Add(user);
        _context.RedemptionRequests.Add(new RedemptionRequest
        {
            UserId = "u1", Status = RedemptionRequestStatus.Completed,
            PointsAmount = 200, SarRate = 10, SarAmount = 20, Method = RedemptionMethod.Cash
        });
        await _context.SaveChangesAsync();

        var result = await _sut.GetRevenueAnalyticsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalSarLiability.Should().Be(80); // 50 + 30
        result.Value.TotalSarHeld.Should().Be(15); // 10 + 5
        result.Value.TotalSarPaidOut.Should().Be(20);
        result.Value.TotalPointsOutstanding.Should().Be(800); // 500 + 300
    }
}
