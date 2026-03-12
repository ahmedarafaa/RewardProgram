using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Contracts.Scan;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Services;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Tests.Services;

public class ScanServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly IUserRepository _userRepo;
    private readonly ScanService _sut;

    public ScanServiceTests()
    {
        _context = TestDbContext.Create();
        _userRepo = Substitute.For<IUserRepository>();
        _sut = new ScanService(_context, _userRepo, Substitute.For<ILogger<ScanService>>());
    }

    public void Dispose() => _context.Dispose();

    private ApplicationUser CreateApprovedUser(string id, string role)
    {
        var user = new ApplicationUser
        {
            Id = id,
            Name = "Test User",
            MobileNumber = "+966500000001",
            UserName = "+966500000001",
            RegistrationStatus = RegistrationStatus.Approved,
            IsDisabled = false
        };

        _userRepo.FindByIdAsync(id, Arg.Any<CancellationToken>()).Returns(user);
        _userRepo.GetRolesAsync(user).Returns(new List<string> { role });

        return user;
    }

    private async Task<(Product product, ProductBarcode barcode)> SeedProductAndBarcode(
        BarcodeStatus status = BarcodeStatus.Available, int pointValue = 100)
    {
        var product = new Product
        {
            Name = "Test Product",
            ProductCode = "TP001",
            PointValue = pointValue,
            Price = 50
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var barcode = new ProductBarcode
        {
            Code = "ABC123DEF456",
            ProductId = product.Id,
            Status = status
        };
        _context.ProductBarcodes.Add(barcode);
        await _context.SaveChangesAsync();

        return (product, barcode);
    }

    // ── User validation ──

    [Fact]
    public async Task ScanBarcode_UserNotFound_ShouldReturnUserNotApproved()
    {
        _userRepo.FindByIdAsync("bad-user", Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.ScanBarcodeAsync(new ScanBarcodeRequest("CODE"), "bad-user");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ScanErrors.UserNotApproved);
    }

    [Fact]
    public async Task ScanBarcode_DisabledUser_ShouldReturnUserNotApproved()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "+966500000001",
            RegistrationStatus = RegistrationStatus.Approved, IsDisabled = true
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.ScanBarcodeAsync(new ScanBarcodeRequest("CODE"), "u1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ScanErrors.UserNotApproved);
    }

    [Fact]
    public async Task ScanBarcode_PendingUser_ShouldReturnUserNotApproved()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "+966500000001",
            RegistrationStatus = RegistrationStatus.PendingSalesman, IsDisabled = false
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.ScanBarcodeAsync(new ScanBarcodeRequest("CODE"), "u1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ScanErrors.UserNotApproved);
    }

    [Fact]
    public async Task ScanBarcode_NonSellerOrTechnicianRole_ShouldReturnUnauthorizedRole()
    {
        var user = CreateApprovedUser("u1", UserRoles.SalesMan);
        _userRepo.GetRolesAsync(user).Returns(new List<string> { UserRoles.SalesMan });

        var result = await _sut.ScanBarcodeAsync(new ScanBarcodeRequest("CODE"), "u1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ScanErrors.UnauthorizedRole);
    }

    // ── Barcode validation ──

    [Fact]
    public async Task ScanBarcode_BarcodeNotFound_ShouldFail()
    {
        CreateApprovedUser("u1", UserRoles.Seller);

        var result = await _sut.ScanBarcodeAsync(new ScanBarcodeRequest("INVALID"), "u1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BarcodeErrors.BarcodeNotFound);
    }

    [Fact]
    public async Task ScanBarcode_ConsumedBarcode_ShouldFail()
    {
        CreateApprovedUser("u1", UserRoles.Seller);
        var (_, barcode) = await SeedProductAndBarcode(BarcodeStatus.Consumed);

        var result = await _sut.ScanBarcodeAsync(new ScanBarcodeRequest(barcode.Code), "u1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BarcodeErrors.BarcodeConsumed);
    }

    // ── Point calculation: Seller first ──

    [Fact]
    public async Task ScanBarcode_SellerFirst_ShouldGet50Percent()
    {
        CreateApprovedUser("seller-1", UserRoles.Seller);
        var (product, barcode) = await SeedProductAndBarcode(pointValue: 100);

        var result = await _sut.ScanBarcodeAsync(new ScanBarcodeRequest(barcode.Code), "seller-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.PointsAwarded.Should().Be(50); // 50% of 100

        // Barcode should be SellerScanned
        var updatedBarcode = await _context.ProductBarcodes.FindAsync(barcode.Id);
        updatedBarcode!.Status.Should().Be(BarcodeStatus.SellerScanned);
    }

    // ── Point calculation: Technician first ──

    [Fact]
    public async Task ScanBarcode_TechnicianFirst_ShouldGet100Percent()
    {
        CreateApprovedUser("tech-1", UserRoles.Technician);
        var (product, barcode) = await SeedProductAndBarcode(pointValue: 100);

        var result = await _sut.ScanBarcodeAsync(new ScanBarcodeRequest(barcode.Code), "tech-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.PointsAwarded.Should().Be(100);

        var updatedBarcode = await _context.ProductBarcodes.FindAsync(barcode.Id);
        updatedBarcode!.Status.Should().Be(BarcodeStatus.TechnicianScanned);
    }

    // ── Point calculation: Technician second (after seller) ──

    [Fact]
    public async Task ScanBarcode_TechnicianAfterSeller_ShouldGet100PercentAndDeferSellerBonus()
    {
        // Seller scans first
        CreateApprovedUser("seller-1", UserRoles.Seller);
        var (product, barcode) = await SeedProductAndBarcode(pointValue: 100);
        await _sut.ScanBarcodeAsync(new ScanBarcodeRequest(barcode.Code), "seller-1");

        // Technician scans second
        CreateApprovedUser("tech-1", UserRoles.Technician);
        var result = await _sut.ScanBarcodeAsync(new ScanBarcodeRequest(barcode.Code), "tech-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.PointsAwarded.Should().Be(100); // Technician gets 100%

        // Barcode should be Consumed
        var updatedBarcode = await _context.ProductBarcodes.FindAsync(barcode.Id);
        updatedBarcode!.Status.Should().Be(BarcodeStatus.Consumed);

        // Seller should have received deferred bonus (50 extra = 100 total)
        var sellerWallet = _context.Wallets.First(w => w.UserId == "seller-1");
        sellerWallet.Balance.Should().Be(100); // 50 initial + 50 deferred

        // Technician wallet
        var techWallet = _context.Wallets.First(w => w.UserId == "tech-1");
        techWallet.Balance.Should().Be(100);
    }

    // ── Point calculation: Seller second (after technician) ──

    [Fact]
    public async Task ScanBarcode_SellerAfterTechnician_ShouldGet100Percent()
    {
        // Technician scans first
        CreateApprovedUser("tech-1", UserRoles.Technician);
        var (product, barcode) = await SeedProductAndBarcode(pointValue: 100);
        await _sut.ScanBarcodeAsync(new ScanBarcodeRequest(barcode.Code), "tech-1");

        // Seller scans second
        CreateApprovedUser("seller-1", UserRoles.Seller);
        var result = await _sut.ScanBarcodeAsync(new ScanBarcodeRequest(barcode.Code), "seller-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.PointsAwarded.Should().Be(100); // Seller gets 100% when scanning second

        var updatedBarcode = await _context.ProductBarcodes.FindAsync(barcode.Id);
        updatedBarcode!.Status.Should().Be(BarcodeStatus.Consumed);
    }

    // ── Duplicate scan ──

    [Fact]
    public async Task ScanBarcode_SameRoleTwice_ShouldFail()
    {
        CreateApprovedUser("seller-1", UserRoles.Seller);
        var (_, barcode) = await SeedProductAndBarcode();
        await _sut.ScanBarcodeAsync(new ScanBarcodeRequest(barcode.Code), "seller-1");

        // Second seller scan (different user, same role)
        CreateApprovedUser("seller-2", UserRoles.Seller);
        var result = await _sut.ScanBarcodeAsync(new ScanBarcodeRequest(barcode.Code), "seller-2");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BarcodeErrors.BarcodeAlreadyScanned);
    }

    // ── Wallet creation ──

    [Fact]
    public async Task ScanBarcode_ShouldCreateWalletIfNotExists()
    {
        CreateApprovedUser("new-user", UserRoles.Seller);
        var (_, barcode) = await SeedProductAndBarcode(pointValue: 100);

        _context.Wallets.Any(w => w.UserId == "new-user").Should().BeFalse();

        await _sut.ScanBarcodeAsync(new ScanBarcodeRequest(barcode.Code), "new-user");

        _context.Wallets.Any(w => w.UserId == "new-user").Should().BeTrue();
    }

    // ── SAR calculation ──

    [Fact]
    public async Task ScanBarcode_ShouldCalculateSarCorrectly()
    {
        // Set SAR rate to 10 (10 points = 1 SAR)
        _context.RewardSettings.Add(new RewardSettings { PointsToSarRate = 10 });
        await _context.SaveChangesAsync();

        CreateApprovedUser("seller-1", UserRoles.Seller);
        var (_, barcode) = await SeedProductAndBarcode(pointValue: 100);

        await _sut.ScanBarcodeAsync(new ScanBarcodeRequest(barcode.Code), "seller-1");

        var wallet = _context.Wallets.First(w => w.UserId == "seller-1");
        wallet.Balance.Should().Be(50); // 50% of 100
        wallet.SarBalance.Should().Be(5); // 50 / 10
    }

    // ── GPS location ──

    [Fact]
    public async Task ScanBarcode_WithLocation_ShouldStoreLat_Lng()
    {
        CreateApprovedUser("seller-1", UserRoles.Seller);
        var (_, barcode) = await SeedProductAndBarcode();

        await _sut.ScanBarcodeAsync(
            new ScanBarcodeRequest(barcode.Code, Latitude: 24.7136, Longitude: 46.6753),
            "seller-1");

        var scan = _context.ScanRecords.First();
        scan.Latitude.Should().Be(24.7136);
        scan.Longitude.Should().Be(46.6753);
    }

    [Fact]
    public async Task ScanBarcode_WithoutLocation_ShouldStoreNull()
    {
        CreateApprovedUser("seller-1", UserRoles.Seller);
        var (_, barcode) = await SeedProductAndBarcode();

        await _sut.ScanBarcodeAsync(new ScanBarcodeRequest(barcode.Code), "seller-1");

        var scan = _context.ScanRecords.First();
        scan.Latitude.Should().BeNull();
        scan.Longitude.Should().BeNull();
    }
}
