using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Services.Admin;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Tests.Services.Admin;

public class AdminBarcodeServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly AdminBarcodeService _sut;
    private const string AdminId = "admin-1";

    public AdminBarcodeServiceTests()
    {
        _context = TestDbContext.Create();
        _sut = new AdminBarcodeService(_context, Substitute.For<ILogger<AdminBarcodeService>>(), new StubLocalizer<ErrorMessages>());
    }

    public void Dispose() => _context.Dispose();

    private async Task<(Product product, ProductBarcode barcode)> SeedProductWithBarcode()
    {
        var product = new Product
        {
            Name = "Test Product", ProductCode = "TP001", PointValue = 100, Price = 50
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var barcode = new ProductBarcode
        {
            Code = "ABC123DEF456", ProductId = product.Id, Status = BarcodeStatus.Available
        };
        _context.ProductBarcodes.Add(barcode);
        await _context.SaveChangesAsync();

        return (product, barcode);
    }

    private async Task<ScanRecord> AddScanRecord(
        string barcodeId, string userId, ScannerRole role, decimal points,
        BarcodeStatus newBarcodeStatus, DateTime? createdAt = null)
    {
        var scan = new ScanRecord
        {
            BarcodeId = barcodeId,
            UserId = userId,
            ScannerRole = role,
            PointsAwarded = points
        };
        if (createdAt.HasValue)
            scan.CreatedAt = createdAt.Value;

        _context.ScanRecords.Add(scan);

        var barcode = await _context.ProductBarcodes.FindAsync(barcodeId);
        barcode!.Status = newBarcodeStatus;

        // Create wallet and transaction
        var wallet = _context.Wallets.FirstOrDefault(w => w.UserId == userId);
        if (wallet is null)
        {
            wallet = new Wallet { UserId = userId, Balance = 0, SarBalance = 0 };
            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();
        }

        wallet.Balance += points;
        wallet.SarBalance += points / 10m; // assume rate = 10

        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = points,
            Type = WalletTransactionType.Earned,
            ReferenceId = scan.Id,
            SarRate = 10,
            SarAmount = points / 10m,
            Description = "Test scan"
        });

        await _context.SaveChangesAsync();
        return scan;
    }

    // ── CancelScan ──

    [Fact]
    public async Task CancelScan_NotFound_ShouldFail()
    {
        var result = await _sut.CancelScanAsync("non-existent", AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BarcodeErrors.ScanNotFound);
    }

    [Fact]
    public async Task CancelScan_OnlyScan_ShouldRevertToAvailableAndReverseWallet()
    {
        var (product, barcode) = await SeedProductWithBarcode();
        var scan = await AddScanRecord(barcode.Id, "seller-1", ScannerRole.Seller, 50, BarcodeStatus.SellerScanned);

        var result = await _sut.CancelScanAsync(scan.Id, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.PointsReversed.Should().Be(50);
        result.Value.SarReversed.Should().Be(5);

        // Barcode reverted to Available
        var updatedBarcode = await _context.ProductBarcodes.FindAsync(barcode.Id);
        updatedBarcode!.Status.Should().Be(BarcodeStatus.Available);

        // Scan soft-deleted
        var updatedScan = await _context.ScanRecords.FindAsync(scan.Id);
        updatedScan!.IsDeleted.Should().BeTrue();

        // Wallet balance reversed
        var wallet = _context.Wallets.First(w => w.UserId == "seller-1");
        wallet.Balance.Should().Be(0);
        wallet.SarBalance.Should().Be(0);

        // Cancellation transaction created
        var cancelTx = _context.WalletTransactions
            .Where(t => t.Type == WalletTransactionType.Cancelled)
            .ToList();
        cancelTx.Should().HaveCount(1);
        cancelTx[0].Amount.Should().Be(-50);
    }

    [Fact]
    public async Task CancelScan_SecondScan_Consumed_ShouldRevertToFirstScanStatus()
    {
        var (product, barcode) = await SeedProductWithBarcode();

        // Seller scans first
        var sellerScan = await AddScanRecord(
            barcode.Id, "seller-1", ScannerRole.Seller, 50, BarcodeStatus.SellerScanned,
            DateTime.UtcNow.AddMinutes(-10));

        // Technician scans second → Consumed
        var techScan = await AddScanRecord(
            barcode.Id, "tech-1", ScannerRole.Technician, 100, BarcodeStatus.Consumed,
            DateTime.UtcNow.AddMinutes(-5));

        // Also add the deferred bonus transaction for the seller (referenced by tech scan Id)
        var sellerWallet = _context.Wallets.First(w => w.UserId == "seller-1");
        sellerWallet.Balance += 50; // deferred bonus
        sellerWallet.SarBalance += 5;
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = sellerWallet.Id,
            Amount = 50, Type = WalletTransactionType.Earned,
            ReferenceId = techScan.Id, SarRate = 10, SarAmount = 5,
            Description = "Deferred bonus"
        });
        await _context.SaveChangesAsync();

        // Cancel the second (technician) scan
        var result = await _sut.CancelScanAsync(techScan.Id, AdminId);

        result.IsSuccess.Should().BeTrue();
        // Should reverse tech's 100pts + seller's deferred 50pts = 150 total
        result.Value.PointsReversed.Should().Be(150);

        // Barcode reverted to SellerScanned (first scan remains)
        var updatedBarcode = await _context.ProductBarcodes.FindAsync(barcode.Id);
        updatedBarcode!.Status.Should().Be(BarcodeStatus.SellerScanned);
    }

    [Fact]
    public async Task CancelScan_FirstScan_WhenConsumed_ShouldFail()
    {
        var (product, barcode) = await SeedProductWithBarcode();

        var sellerScan = await AddScanRecord(
            barcode.Id, "seller-1", ScannerRole.Seller, 50, BarcodeStatus.SellerScanned,
            DateTime.UtcNow.AddMinutes(-10));

        var techScan = await AddScanRecord(
            barcode.Id, "tech-1", ScannerRole.Technician, 100, BarcodeStatus.Consumed,
            DateTime.UtcNow.AddMinutes(-5));

        // Try to cancel the FIRST scan (should fail because second still exists)
        var result = await _sut.CancelScanAsync(sellerScan.Id, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(BarcodeErrors.CannotCancelFirstScan);
    }

    [Fact]
    public async Task CancelScan_TechnicianOnlyScan_ShouldRevertToAvailable()
    {
        var (product, barcode) = await SeedProductWithBarcode();
        var scan = await AddScanRecord(barcode.Id, "tech-1", ScannerRole.Technician, 100, BarcodeStatus.TechnicianScanned);

        var result = await _sut.CancelScanAsync(scan.Id, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.PointsReversed.Should().Be(100);

        var updatedBarcode = await _context.ProductBarcodes.FindAsync(barcode.Id);
        updatedBarcode!.Status.Should().Be(BarcodeStatus.Available);
    }
}
