using FluentAssertions;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Domain.Tests;

public class EntityDefaultsTests
{
    // ── ProductBarcode ──

    [Fact]
    public void NewBarcode_ShouldDefault_ToAvailable()
    {
        var barcode = new ProductBarcode();

        barcode.Status.Should().Be(BarcodeStatus.Available);
        barcode.Code.Should().BeEmpty();
        barcode.RowVersion.Should().BeEmpty();
        barcode.ScanRecords.Should().BeEmpty();
    }

    // ── Wallet ──

    [Fact]
    public void NewWallet_ShouldDefault_ToZeroBalances()
    {
        var wallet = new Wallet();

        wallet.Balance.Should().Be(0);
        wallet.SarBalance.Should().Be(0);
        wallet.HeldBalance.Should().Be(0);
        wallet.HeldSarBalance.Should().Be(0);
        wallet.UserId.Should().BeEmpty();
        wallet.RowVersion.Should().BeEmpty();
        wallet.Transactions.Should().BeEmpty();
    }

    // ── WalletTransaction ──

    [Fact]
    public void NewWalletTransaction_ShouldDefault_ToZeros()
    {
        var tx = new WalletTransaction();

        tx.Amount.Should().Be(0);
        tx.SarRate.Should().Be(0);
        tx.SarAmount.Should().Be(0);
        tx.RemainingAmount.Should().Be(0);
        tx.ReferenceId.Should().BeNull();
        tx.Description.Should().BeNull();
    }

    // ── RedemptionRequest ──

    [Fact]
    public void NewRedemptionRequest_ShouldDefault_WithEmptyCollections()
    {
        var request = new RedemptionRequest();

        request.Approvals.Should().BeEmpty();
        request.CashOtpHash.Should().BeNull();
        request.CashOtpExpiresAt.Should().BeNull();
        request.CashOtpAttempts.Should().Be(0);
        request.CashHandoverById.Should().BeNull();
        request.RejectionReason.Should().BeNull();
        request.RejectedById.Should().BeNull();
    }

    // ── Notification ──

    [Fact]
    public void NewNotification_ShouldDefault_ToUnread()
    {
        var notification = new Notification();

        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
        notification.ReferenceId.Should().BeNull();
    }

    // ── Product ──

    [Fact]
    public void NewProduct_ShouldHave_EmptyBarcodesList()
    {
        var product = new Product();

        product.Barcodes.Should().BeEmpty();
    }

    // ── RewardSettings ──

    [Fact]
    public void NewRewardSettings_ShouldHave_Defaults()
    {
        var settings = new RewardSettings();

        settings.Id.Should().NotBeNullOrEmpty();
        settings.PointsToSarRate.Should().Be(10m);
        settings.InviterRewardPoints.Should().Be(100m);
        settings.InviteeRewardPoints.Should().Be(50m);
        settings.MinimumRedemptionPoints.Should().Be(1000m);
    }

    // ── NationalAddress (Owned) ──

    [Fact]
    public void NewNationalAddress_ShouldDefault_ToEmptyStrings()
    {
        var address = new NationalAddress();

        address.CityId.Should().BeEmpty();
        address.Street.Should().BeEmpty();
        address.PostalCode.Should().BeEmpty();
        address.District.Should().BeEmpty();
        address.BuildingNumber.Should().Be(0);
        address.SubNumber.Should().Be(0);
    }
}
