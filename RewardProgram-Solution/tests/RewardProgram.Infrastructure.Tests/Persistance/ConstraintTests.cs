using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Infrastructure.Tests.Persistance;

/// <summary>
/// Tests unique indexes, default values, required fields, composite keys,
/// and delete behaviors defined in EF configurations.
/// </summary>
public class ConstraintTests : IDisposable
{
    private readonly TestDbContextFactory _factory;

    public ConstraintTests()
    {
        _factory = new TestDbContextFactory("admin");
    }

    public void Dispose() => _factory.Dispose();

    // ════════════════════════════════════════════════
    //  MODEL-LEVEL CONSTRAINT VALIDATION
    // ════════════════════════════════════════════════

    [Fact]
    public void AllForeignKeys_ShouldNotCascadeDelete_ExceptOwnership()
    {
        using var ctx = _factory.CreateContext();
        var model = ctx.Model;

        var cascadeFKs = model.GetEntityTypes()
            .SelectMany(t => t.GetForeignKeys())
            .Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade && !fk.IsOwnership)
            .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name} -> {fk.PrincipalEntityType.ClrType.Name}")
            .ToList();

        cascadeFKs.Should().BeEmpty("all non-ownership FKs should use Restrict (or SetNull), never Cascade");
    }

    [Fact]
    public void SetNullDeleteBehavior_ShouldOnlyApplyToExpectedFKs()
    {
        using var ctx = _factory.CreateContext();
        var model = ctx.Model;

        var setNullFKs = model.GetEntityTypes()
            .SelectMany(t => t.GetForeignKeys())
            .Where(fk => fk.DeleteBehavior == DeleteBehavior.SetNull)
            .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name}.{string.Join(",", fk.Properties.Select(p => p.Name))}")
            .ToList();

        // City.ApprovalSalesManId and Region.ZoneManagerId should be SetNull
        setNullFKs.Should().Contain(s => s.Contains("ApprovalSalesManId"), "City.ApprovalSalesManId FK should be SetNull");
        setNullFKs.Should().Contain(s => s.Contains("ZoneManagerId"), "Region.ZoneManagerId FK should be SetNull");
    }

    // ════════════════════════════════════════════════
    //  UNIQUE INDEX VALIDATION (via model metadata)
    // ════════════════════════════════════════════════

    [Fact]
    public void ErpCustomer_ShouldHaveUniqueCustomerCode()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ErpCustomer))!;

        var uniqueIndexes = entityType.GetIndexes()
            .Where(i => i.IsUnique)
            .SelectMany(i => i.Properties.Select(p => p.Name))
            .ToList();

        uniqueIndexes.Should().Contain("CustomerCode");
    }

    [Fact]
    public void Product_ShouldHaveUniqueProductCode()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(Product))!;

        var uniqueIndexes = entityType.GetIndexes()
            .Where(i => i.IsUnique)
            .SelectMany(i => i.Properties.Select(p => p.Name))
            .ToList();

        uniqueIndexes.Should().Contain("ProductCode");
    }

    [Fact]
    public void ProductBarcode_ShouldHaveUniqueCode()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ProductBarcode))!;

        var uniqueIndexes = entityType.GetIndexes()
            .Where(i => i.IsUnique)
            .SelectMany(i => i.Properties.Select(p => p.Name))
            .ToList();

        uniqueIndexes.Should().Contain("Code");
    }

    [Fact]
    public void ShopData_ShouldHaveUniqueConstraintsOnBusinessFields()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ShopData))!;

        var uniqueProps = entityType.GetIndexes()
            .Where(i => i.IsUnique)
            .SelectMany(i => i.Properties.Select(p => p.Name))
            .ToList();

        uniqueProps.Should().Contain("CustomerCode");
        uniqueProps.Should().Contain("VAT");
        uniqueProps.Should().Contain("CRN");
        uniqueProps.Should().Contain("ShortAddress");
    }

    [Fact]
    public void Wallet_ShouldHaveUniqueUserId()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(Wallet))!;

        var uniqueIndexes = entityType.GetIndexes()
            .Where(i => i.IsUnique)
            .SelectMany(i => i.Properties.Select(p => p.Name))
            .ToList();

        uniqueIndexes.Should().Contain("UserId");
    }

    [Fact]
    public void ScanRecord_ShouldHaveCompositeUniqueOnBarcodeAndRole()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ScanRecord))!;

        var compositeUnique = entityType.GetIndexes()
            .Where(i => i.IsUnique && i.Properties.Count > 1)
            .ToList();

        compositeUnique.Should().ContainSingle("ScanRecord should have exactly one composite unique index");

        var props = compositeUnique[0].Properties.Select(p => p.Name).ToList();
        props.Should().Contain("BarcodeId");
        props.Should().Contain("ScannerRole");
    }

    [Fact]
    public void City_ShouldHaveCompositeUniqueOnRegionAndName()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(City))!;

        var compositeUniques = entityType.GetIndexes()
            .Where(i => i.IsUnique && i.Properties.Count > 1)
            .ToList();

        compositeUniques.Should().HaveCountGreaterThanOrEqualTo(2, "City needs unique (RegionId,NameAr) and (RegionId,NameEn)");

        var allProps = compositeUniques.SelectMany(i => i.Properties.Select(p => p.Name)).ToList();
        allProps.Should().Contain("RegionId");
        allProps.Should().Contain("NameAr");
        allProps.Should().Contain("NameEn");
    }

    [Fact]
    public void District_ShouldHaveCompositeUniqueOnCityAndName()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(District))!;

        var compositeUniques = entityType.GetIndexes()
            .Where(i => i.IsUnique && i.Properties.Count > 1)
            .ToList();

        compositeUniques.Should().HaveCountGreaterThanOrEqualTo(2, "District needs unique (CityId,NameAr) and (CityId,NameEn)");
    }

    [Fact]
    public void Region_ShouldHaveUniqueNames()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(Region))!;

        var uniqueProps = entityType.GetIndexes()
            .Where(i => i.IsUnique)
            .SelectMany(i => i.Properties.Select(p => p.Name))
            .ToList();

        uniqueProps.Should().Contain("NameAr");
        uniqueProps.Should().Contain("NameEn");
        uniqueProps.Should().Contain("ZoneManagerId");
    }

    [Fact]
    public void UserNotificationPreference_ShouldHaveCompositeKey()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(UserNotificationPreference))!;

        var pk = entityType.FindPrimaryKey()!;
        var pkProps = pk.Properties.Select(p => p.Name).ToList();

        pkProps.Should().HaveCount(2);
        pkProps.Should().Contain("UserId");
        pkProps.Should().Contain("NotificationType");
    }

    [Fact]
    public void ShopOwnerProfile_ShouldHaveUniqueUserId()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ShopOwnerProfile))!;

        var uniqueProps = entityType.GetIndexes()
            .Where(i => i.IsUnique)
            .SelectMany(i => i.Properties.Select(p => p.Name))
            .ToList();

        uniqueProps.Should().Contain("UserId");
    }

    [Fact]
    public void SellerProfile_ShouldHaveUniqueUserId()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(SellerProfile))!;

        var uniqueProps = entityType.GetIndexes()
            .Where(i => i.IsUnique)
            .SelectMany(i => i.Properties.Select(p => p.Name))
            .ToList();

        uniqueProps.Should().Contain("UserId");
    }

    [Fact]
    public void TechnicianProfile_ShouldHaveUniqueUserId()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(TechnicianProfile))!;

        var uniqueProps = entityType.GetIndexes()
            .Where(i => i.IsUnique)
            .SelectMany(i => i.Properties.Select(p => p.Name))
            .ToList();

        uniqueProps.Should().Contain("UserId");
    }

    // ════════════════════════════════════════════════
    //  INDEX VALIDATION (non-unique)
    // ════════════════════════════════════════════════

    [Fact]
    public void Notification_ShouldHaveCompositeIndexOnUserIsReadCreatedAt()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(Notification))!;

        var compositeIndexes = entityType.GetIndexes()
            .Where(i => i.Properties.Count >= 3)
            .ToList();

        compositeIndexes.Should().ContainSingle();
        var props = compositeIndexes[0].Properties.Select(p => p.Name).ToList();
        props.Should().Contain("UserId");
        props.Should().Contain("IsRead");
        props.Should().Contain("CreatedAt");
    }

    [Fact]
    public void OtpCode_ShouldHaveUniquePinId()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(Domain.Entities.OTP.OtpCode))!;

        var uniqueProps = entityType.GetIndexes()
            .Where(i => i.IsUnique)
            .SelectMany(i => i.Properties.Select(p => p.Name))
            .ToList();

        uniqueProps.Should().Contain("PinId");
    }

    // ════════════════════════════════════════════════
    //  DEFAULT VALUE VALIDATION
    // ════════════════════════════════════════════════

    [Fact]
    public async Task Region_DefaultIsActiveTrue()
    {
        await using var ctx = _factory.CreateContext();
        var region = new Region { NameAr = "تبوك", NameEn = "Tabuk" };
        ctx.Regions.Add(region);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.Regions.FirstAsync(r => r.Id == region.Id);
        loaded.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task City_DefaultIsActiveTrue()
    {
        await using var ctx = _factory.CreateContext();
        var region = new Region { NameAr = "تبوك", NameEn = "Tabuk" };
        ctx.Regions.Add(region);
        var city = new City { NameAr = "تبوك", NameEn = "Tabuk", RegionId = region.Id };
        ctx.Cities.Add(city);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.Cities.FirstAsync(c => c.Id == city.Id);
        loaded.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ProductBarcode_DefaultStatusAvailable()
    {
        await using var ctx = _factory.CreateContext();
        var product = new Product { Name = "P", ProductCode = "DEF-001", PointValue = 5, Price = 10m, Category = "C" };
        ctx.Products.Add(product);
        var barcode = new ProductBarcode { Code = "DEFAULT12345", ProductId = product.Id };
        ctx.ProductBarcodes.Add(barcode);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.ProductBarcodes.FirstAsync(b => b.Id == barcode.Id);
        loaded.Status.Should().Be(BarcodeStatus.Available);
    }

    [Fact]
    public async Task Notification_DefaultIsReadFalse()
    {
        await using var ctx = _factory.CreateContext();
        var n = new Notification
        {
            UserId = Guid.NewGuid().ToString(),
            Type = NotificationType.AdminMessage,
            Title = "Test",
            Body = "Body"
        };
        ctx.Notifications.Add(n);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.Notifications.FirstAsync(x => x.Id == n.Id);
        loaded.IsRead.Should().BeFalse();
        loaded.ReadAt.Should().BeNull();
    }

    [Fact]
    public async Task Wallet_DefaultAllBalancesZero()
    {
        await using var ctx = _factory.CreateContext();
        var wallet = new Wallet { UserId = Guid.NewGuid().ToString() };
        ctx.Wallets.Add(wallet);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.Wallets.FirstAsync(w => w.Id == wallet.Id);
        loaded.Balance.Should().Be(0m);
        loaded.SarBalance.Should().Be(0m);
        loaded.HeldBalance.Should().Be(0m);
        loaded.HeldSarBalance.Should().Be(0m);
    }

    [Fact]
    public async Task RewardSettings_DefaultValues()
    {
        await using var ctx = _factory.CreateContext();
        var settings = new RewardSettings();
        ctx.RewardSettings.Add(settings);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.RewardSettings.FirstAsync();
        loaded.PointsToSarRate.Should().Be(10m);
        loaded.InviterRewardPoints.Should().Be(100m);
        loaded.InviteeRewardPoints.Should().Be(50m);
        loaded.MinimumRedemptionPoints.Should().Be(1000m);
    }

    [Fact]
    public async Task RedemptionRequest_DefaultCashOtpAttemptsZero()
    {
        await using var ctx = _factory.CreateContext();
        var req = new RedemptionRequest
        {
            UserId = "user1",
            Method = RedemptionMethod.Cash,
            Status = RedemptionRequestStatus.PendingSalesMan,
            PointsAmount = 1000,
            SarRate = 10,
            SarAmount = 100
        };
        ctx.RedemptionRequests.Add(req);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.RedemptionRequests.FirstAsync(r => r.Id == req.Id);
        loaded.CashOtpAttempts.Should().Be(0);
        loaded.CashOtpHash.Should().BeNull();
    }

    [Fact]
    public async Task UserNotificationPreference_DefaultIsPushMutedFalse()
    {
        await using var ctx = _factory.CreateContext();
        var pref = new UserNotificationPreference
        {
            UserId = Guid.NewGuid().ToString(),
            NotificationType = NotificationType.PointsEarned
        };
        ctx.UserNotificationPreferences.Add(pref);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.UserNotificationPreferences.FirstAsync();
        loaded.IsPushMuted.Should().BeFalse();
    }

    // ════════════════════════════════════════════════
    //  ENUM PERSISTENCE
    // ════════════════════════════════════════════════

    [Theory]
    [InlineData(BarcodeStatus.Available)]
    [InlineData(BarcodeStatus.SellerScanned)]
    [InlineData(BarcodeStatus.TechnicianScanned)]
    [InlineData(BarcodeStatus.Consumed)]
    public async Task ProductBarcode_ShouldPersistAllStatusValues(BarcodeStatus status)
    {
        await using var ctx = _factory.CreateContext();
        var product = new Product { Name = "P", ProductCode = $"ENUM-{(int)status}", PointValue = 5, Price = 10m, Category = "C" };
        ctx.Products.Add(product);
        var barcode = new ProductBarcode { Code = $"ENUM{(int)status:D8}", ProductId = product.Id, Status = status };
        ctx.ProductBarcodes.Add(barcode);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.ProductBarcodes.FirstAsync(b => b.Id == barcode.Id);
        loaded.Status.Should().Be(status);
    }

    [Theory]
    [InlineData(WalletTransactionType.Earned)]
    [InlineData(WalletTransactionType.Redeemed)]
    [InlineData(WalletTransactionType.Cancelled)]
    [InlineData(WalletTransactionType.Expired)]
    [InlineData(WalletTransactionType.Refunded)]
    [InlineData(WalletTransactionType.InvitationReward)]
    public async Task WalletTransaction_ShouldPersistAllTypeValues(WalletTransactionType type)
    {
        await using var ctx = _factory.CreateContext();
        var wallet = new Wallet { UserId = Guid.NewGuid().ToString() };
        ctx.Wallets.Add(wallet);
        var tx = new WalletTransaction { WalletId = wallet.Id, Amount = 10, Type = type, SarRate = 10, SarAmount = 1 };
        ctx.WalletTransactions.Add(tx);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.WalletTransactions.FirstAsync(t => t.Id == tx.Id);
        loaded.Type.Should().Be(type);
    }

    [Theory]
    [InlineData(NotificationType.RegistrationApproved)]
    [InlineData(NotificationType.RegistrationRejected)]
    [InlineData(NotificationType.PointsEarned)]
    [InlineData(NotificationType.RedemptionCreated)]
    [InlineData(NotificationType.RedemptionApproved)]
    [InlineData(NotificationType.RedemptionRejected)]
    [InlineData(NotificationType.RedemptionCompleted)]
    [InlineData(NotificationType.InvitationReward)]
    [InlineData(NotificationType.AdminMessage)]
    public async Task Notification_ShouldPersistAllTypeValues(NotificationType type)
    {
        await using var ctx = _factory.CreateContext();
        var n = new Notification { UserId = Guid.NewGuid().ToString(), Type = type, Title = "T", Body = "B" };
        ctx.Notifications.Add(n);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.Notifications.FirstAsync(x => x.Id == n.Id);
        loaded.Type.Should().Be(type);
    }

    [Theory]
    [InlineData(ScannerRole.Seller)]
    [InlineData(ScannerRole.Technician)]
    public async Task ScanRecord_ShouldPersistAllScannerRoles(ScannerRole role)
    {
        await using var ctx = _factory.CreateContext();
        var product = new Product { Name = "P", ProductCode = $"SR-{(int)role}", PointValue = 5, Price = 10m, Category = "C" };
        ctx.Products.Add(product);
        var barcode = new ProductBarcode { Code = $"SROLE{(int)role:D6}", ProductId = product.Id };
        ctx.ProductBarcodes.Add(barcode);
        var scan = new ScanRecord { BarcodeId = barcode.Id, UserId = "user1", ScannerRole = role, PointsAwarded = 5 };
        ctx.ScanRecords.Add(scan);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.ScanRecords.FirstAsync(s => s.Id == scan.Id);
        loaded.ScannerRole.Should().Be(role);
    }

    [Theory]
    [InlineData(RedemptionMethod.BankTransfer)]
    [InlineData(RedemptionMethod.Cash)]
    public async Task RedemptionRequest_ShouldPersistAllMethods(RedemptionMethod method)
    {
        await using var ctx = _factory.CreateContext();
        var req = new RedemptionRequest
        {
            UserId = Guid.NewGuid().ToString(),
            Method = method,
            Status = RedemptionRequestStatus.PendingSalesMan,
            PointsAmount = 1000,
            SarRate = 10,
            SarAmount = 100
        };
        ctx.RedemptionRequests.Add(req);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.RedemptionRequests.FirstAsync(r => r.Id == req.Id);
        loaded.Method.Should().Be(method);
    }

    // ════════════════════════════════════════════════
    //  DECIMAL PRECISION
    // ════════════════════════════════════════════════

    [Fact]
    public async Task Wallet_ShouldPreserveDecimalPrecision()
    {
        await using var ctx = _factory.CreateContext();
        var wallet = new Wallet
        {
            UserId = Guid.NewGuid().ToString(),
            Balance = 12345.5m,
            SarBalance = 1234.56m,
            HeldBalance = 100.5m,
            HeldSarBalance = 10.05m
        };
        ctx.Wallets.Add(wallet);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.Wallets.FirstAsync(w => w.Id == wallet.Id);
        loaded.Balance.Should().Be(12345.5m);
        loaded.SarBalance.Should().Be(1234.56m);
        loaded.HeldBalance.Should().Be(100.5m);
        loaded.HeldSarBalance.Should().Be(10.05m);
    }

    [Fact]
    public async Task WalletTransaction_ShouldPreserveDecimalPrecision()
    {
        await using var ctx = _factory.CreateContext();
        var wallet = new Wallet { UserId = Guid.NewGuid().ToString() };
        ctx.Wallets.Add(wallet);

        var tx = new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = 150.5m,
            Type = WalletTransactionType.Earned,
            SarRate = 10.00m,
            SarAmount = 15.05m,
            RemainingAmount = 150.5m
        };
        ctx.WalletTransactions.Add(tx);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.WalletTransactions.FirstAsync(t => t.Id == tx.Id);
        loaded.Amount.Should().Be(150.5m);
        loaded.SarRate.Should().Be(10.00m);
        loaded.SarAmount.Should().Be(15.05m);
        loaded.RemainingAmount.Should().Be(150.5m);
    }

    [Fact]
    public async Task Product_ShouldPreservePricePrecision()
    {
        await using var ctx = _factory.CreateContext();
        var product = new Product
        {
            Name = "Expensive Panel",
            ProductCode = "PREC-001",
            PointValue = 200,
            Price = 9999.99m,
            Category = "Premium"
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.Products.FirstAsync(p => p.Id == product.Id);
        loaded.Price.Should().Be(9999.99m);
    }

    // ════════════════════════════════════════════════
    //  NULLABLE FK FIELDS
    // ════════════════════════════════════════════════

    [Fact]
    public async Task Region_ZoneManagerId_ShouldBeNullable()
    {
        await using var ctx = _factory.CreateContext();
        var region = new Region { NameAr = "حائل", NameEn = "Hail", ZoneManagerId = null };
        ctx.Regions.Add(region);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.Regions.FirstAsync(r => r.Id == region.Id);
        loaded.ZoneManagerId.Should().BeNull();
    }

    [Fact]
    public async Task City_ApprovalSalesManId_ShouldBeNullable()
    {
        await using var ctx = _factory.CreateContext();
        var region = new Region { NameAr = "حائل", NameEn = "Hail" };
        ctx.Regions.Add(region);
        var city = new City { NameAr = "حائل", NameEn = "Hail", RegionId = region.Id, ApprovalSalesManId = null };
        ctx.Cities.Add(city);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.Cities.FirstAsync(c => c.Id == city.Id);
        loaded.ApprovalSalesManId.Should().BeNull();
    }

    [Fact]
    public async Task RedemptionRequest_OptionalFields_ShouldBeNullable()
    {
        await using var ctx = _factory.CreateContext();
        var req = new RedemptionRequest
        {
            UserId = "user1",
            Method = RedemptionMethod.Cash,
            Status = RedemptionRequestStatus.PendingSalesMan,
            PointsAmount = 1000,
            SarRate = 10,
            SarAmount = 100,
            // All optional fields left null
        };
        ctx.RedemptionRequests.Add(req);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.RedemptionRequests.FirstAsync(r => r.Id == req.Id);
        loaded.Iban.Should().BeNull();
        loaded.AccountNumber.Should().BeNull();
        loaded.SwiftCode.Should().BeNull();
        loaded.AccountName.Should().BeNull();
        loaded.Address.Should().BeNull();
        loaded.CashOtpHash.Should().BeNull();
        loaded.CashOtpExpiresAt.Should().BeNull();
        loaded.CashHandoverById.Should().BeNull();
        loaded.CashHandoverAt.Should().BeNull();
        loaded.RejectionReason.Should().BeNull();
        loaded.RejectedById.Should().BeNull();
    }

    // ════════════════════════════════════════════════
    //  ALTERNATE KEY FK (CustomerCode)
    // ════════════════════════════════════════════════

    [Fact]
    public void ErpCustomer_ShouldHaveAlternateKeyOnCustomerCode()
    {
        using var ctx = _factory.CreateContext();
        var entityType = ctx.Model.FindEntityType(typeof(ErpCustomer))!;

        var alternateKeys = entityType.GetKeys()
            .Where(k => !k.IsPrimaryKey())
            .ToList();

        // CustomerCode is used as principal key for ShopData, ShopOwnerProfile, SellerProfile FKs
        alternateKeys.Should().NotBeEmpty("ErpCustomer should have an alternate key on CustomerCode");
        alternateKeys.SelectMany(k => k.Properties.Select(p => p.Name)).Should().Contain("CustomerCode");
    }
}
