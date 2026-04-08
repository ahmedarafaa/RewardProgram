using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Infrastructure.Tests.Persistance;

/// <summary>
/// Tests business-level relationships and referential integrity using SQLite
/// (which enforces FK and unique constraints, unlike InMemory provider).
/// </summary>
public class RelationshipTests : IDisposable
{
    private readonly SqliteTestDbContextFactory _factory;

    public RelationshipTests()
    {
        _factory = new SqliteTestDbContextFactory("admin");
    }

    public void Dispose() => _factory.Dispose();

    // ═══════════════════════════════════════════════════
    //  GEOGRAPHIC HIERARCHY: Region → City → District
    //  Business rule: Region is top-level, City belongs to Region
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task Region_ShouldHaveManyCities_AndCities_ShouldNavigateBack()
    {
        await using var ctx = _factory.CreateContext();

        var region = new Region { NameAr = "الرياض", NameEn = "Riyadh" };
        ctx.Regions.Add(region);
        ctx.Cities.AddRange(
            new City { NameAr = "الرياض", NameEn = "Riyadh", RegionId = region.Id },
            new City { NameAr = "الخرج", NameEn = "Al Kharj", RegionId = region.Id });
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.Regions.Include(r => r.Cities).FirstAsync();
        loaded.Cities.Should().HaveCount(2);

        var city = await verify.Cities.Include(c => c.Region).FirstAsync(c => c.NameEn == "Riyadh");
        city.Region.NameEn.Should().Be("Riyadh");
    }

    [Fact]
    public async Task City_CannotExistWithoutRegion()
    {
        await using var ctx = _factory.CreateContext();
        var orphanCity = new City { NameAr = "يتيمة", NameEn = "Orphan", RegionId = Guid.NewGuid().ToString() };
        ctx.Cities.Add(orphanCity);

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("FK to Region should be enforced");
    }

    [Fact]
    public async Task District_CannotExistWithoutCity()
    {
        await using var ctx = _factory.CreateContext();
        var orphan = new District { NameAr = "حي", NameEn = "District", CityId = Guid.NewGuid().ToString() };
        ctx.Districts.Add(orphan);

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("FK to City should be enforced");
    }

    // ═══════════════════════════════════════════════════
    //  REGION ↔ ZONE MANAGER (one-to-one)
    //  Business rule: Each Region has at most ONE ZoneManager
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task Region_ZoneManagerId_IsUniqueAcrossRegions()
    {
        await using var ctx = _factory.CreateContext();
        var zmId = Guid.NewGuid().ToString();

        var region1 = new Region { NameAr = "منطقة أ", NameEn = "Region A", ZoneManagerId = zmId };
        var region2 = new Region { NameAr = "منطقة ب", NameEn = "Region B", ZoneManagerId = zmId };
        ctx.Regions.AddRange(region1, region2);

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>(
            "one ZM cannot manage two regions — unique index on ZoneManagerId");
    }

    [Fact]
    public async Task Region_MultipleRegions_CanHaveNullZoneManager()
    {
        await using var ctx = _factory.CreateContext();
        ctx.Regions.AddRange(
            new Region { NameAr = "أ", NameEn = "A", ZoneManagerId = null },
            new Region { NameAr = "ب", NameEn = "B", ZoneManagerId = null });
        await ctx.SaveChangesAsync();

        var count = await ctx.Regions.CountAsync();
        count.Should().Be(2, "multiple null ZoneManagerId should be allowed");
    }

    // ═══════════════════════════════════════════════════
    //  CITY ↔ APPROVAL SALESMAN (many-to-one nullable)
    //  Business rule: Each City has at most ONE ApprovalSalesMan
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task City_ApprovalSalesManId_CanBeNull()
    {
        await using var ctx = _factory.CreateContext();
        var region = new Region { NameAr = "الرياض", NameEn = "Riyadh" };
        ctx.Regions.Add(region);
        ctx.Cities.Add(new City { NameAr = "الرياض", NameEn = "Riyadh", RegionId = region.Id, ApprovalSalesManId = null });
        await ctx.SaveChangesAsync();

        var city = await ctx.Cities.FirstAsync();
        city.ApprovalSalesManId.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════
    //  ERP CUSTOMER → SHOP DATA (one-to-one via CustomerCode)
    //  Business rule: One ShopData per ErpCustomer
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task ErpCustomer_HasOneShopData_ViaCustomerCode()
    {
        await using var ctx = _factory.CreateContext();
        await _factory.SeedUsersAsync(ctx, "user1");
        var (region, city) = await SeedRegionAndCity(ctx);

        var erp = new ErpCustomer { CustomerCode = "ERP-100", CustomerName = "Customer" };
        ctx.ErpCustomers.Add(erp);
        ctx.ShopData.Add(MakeShopData("ERP-100", city.Id, vat: "300000000000003", crn: "1000000001", shortAddr: "AAAA1111"));
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.ErpCustomers.Include(e => e.ShopData).FirstAsync(e => e.CustomerCode == "ERP-100");
        loaded.ShopData.Should().NotBeNull();
    }

    [Fact]
    public async Task ErpCustomer_DuplicateCustomerCode_ShouldFail()
    {
        await using var ctx = _factory.CreateContext();
        ctx.ErpCustomers.Add(new ErpCustomer { CustomerCode = "DUP-001", CustomerName = "First" });
        await ctx.SaveChangesAsync();

        // Use a new context to avoid change tracker alternate-key conflict
        await using var ctx2 = _factory.CreateContext();
        ctx2.ErpCustomers.Add(new ErpCustomer { CustomerCode = "DUP-001", CustomerName = "Second" });

        var act = () => ctx2.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("CustomerCode must be unique");
    }

    [Fact]
    public async Task ShopData_DuplicateCustomerCode_ShouldFail()
    {
        await using var ctx = _factory.CreateContext();
        await _factory.SeedUsersAsync(ctx, "user1");
        var (region, city) = await SeedRegionAndCity(ctx);

        ctx.ErpCustomers.Add(new ErpCustomer { CustomerCode = "SD-DUP", CustomerName = "C" });
        ctx.ShopData.Add(MakeShopData("SD-DUP", city.Id, vat: "300000000000011", crn: "1000000011", shortAddr: "BBBB1111"));
        await ctx.SaveChangesAsync();

        await using var ctx2 = _factory.CreateContext();
        ctx2.ShopData.Add(MakeShopData("SD-DUP", city.Id, vat: "300000000000012", crn: "1000000012", shortAddr: "CCCC1111"));

        var act = () => ctx2.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("one ShopData per CustomerCode");
    }

    [Fact]
    public async Task ShopData_DuplicateVAT_ShouldFail()
    {
        await using var ctx = _factory.CreateContext();
        var (region, city) = await SeedRegionAndCity(ctx);

        ctx.ErpCustomers.AddRange(
            new ErpCustomer { CustomerCode = "VAT-A", CustomerName = "A" },
            new ErpCustomer { CustomerCode = "VAT-B", CustomerName = "B" });
        ctx.ShopData.Add(MakeShopData("VAT-A", city.Id, vat: "300000000000099", crn: "1000000021", shortAddr: "DDDD1111"));
        ctx.ShopData.Add(MakeShopData("VAT-B", city.Id, vat: "300000000000099", crn: "1000000022", shortAddr: "EEEE1111"));

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("VAT must be unique");
    }

    [Fact]
    public async Task ShopData_DuplicateCRN_ShouldFail()
    {
        await using var ctx = _factory.CreateContext();
        var (region, city) = await SeedRegionAndCity(ctx);

        ctx.ErpCustomers.AddRange(
            new ErpCustomer { CustomerCode = "CRN-A", CustomerName = "A" },
            new ErpCustomer { CustomerCode = "CRN-B", CustomerName = "B" });
        ctx.ShopData.Add(MakeShopData("CRN-A", city.Id, vat: "300000000000031", crn: "9999999999", shortAddr: "FFFF1111"));
        ctx.ShopData.Add(MakeShopData("CRN-B", city.Id, vat: "300000000000032", crn: "9999999999", shortAddr: "GGGG1111"));

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("CRN must be unique");
    }

    [Fact]
    public async Task ShopData_DuplicateShortAddress_ShouldFail()
    {
        await using var ctx = _factory.CreateContext();
        var (region, city) = await SeedRegionAndCity(ctx);

        ctx.ErpCustomers.AddRange(
            new ErpCustomer { CustomerCode = "SA-A", CustomerName = "A" },
            new ErpCustomer { CustomerCode = "SA-B", CustomerName = "B" });
        ctx.ShopData.Add(MakeShopData("SA-A", city.Id, vat: "300000000000041", crn: "1000000041", shortAddr: "HHHH1111"));
        ctx.ShopData.Add(MakeShopData("SA-B", city.Id, vat: "300000000000042", crn: "1000000042", shortAddr: "HHHH1111"));

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("ShortAddress must be unique");
    }

    // ═══════════════════════════════════════════════════
    //  PRODUCT → BARCODE (one-to-many)
    //  Business rule: Barcode Code must be globally unique
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task Product_HasManyBarcodes()
    {
        await using var ctx = _factory.CreateContext();
        var product = MakeProduct("PRD-001");
        ctx.Products.Add(product);
        ctx.ProductBarcodes.AddRange(
            new ProductBarcode { Code = "BARCODE00001", ProductId = product.Id, RowVersion = [0, 0, 0, 1] },
            new ProductBarcode { Code = "BARCODE00002", ProductId = product.Id, RowVersion = [0, 0, 0, 2] });
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.Products.Include(p => p.Barcodes).FirstAsync();
        loaded.Barcodes.Should().HaveCount(2);
    }

    [Fact]
    public async Task ProductBarcode_DuplicateCode_ShouldFail()
    {
        await using var ctx = _factory.CreateContext();
        var product = MakeProduct("PRD-DUP");
        ctx.Products.Add(product);
        ctx.ProductBarcodes.AddRange(
            new ProductBarcode { Code = "SAME_CODE_12", ProductId = product.Id },
            new ProductBarcode { Code = "SAME_CODE_12", ProductId = product.Id });

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("barcode Code must be unique");
    }

    [Fact]
    public async Task ProductBarcode_CannotExistWithoutProduct()
    {
        await using var ctx = _factory.CreateContext();
        ctx.ProductBarcodes.Add(new ProductBarcode { Code = "ORPHAN123456", ProductId = Guid.NewGuid().ToString() });

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("FK to Product should be enforced");
    }

    // ═══════════════════════════════════════════════════
    //  BARCODE → SCAN RECORD
    //  Business rule: One scan per role per barcode (composite unique)
    //  Business rule: Max 2 scans = Consumed (Seller + Technician)
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task ScanRecord_OneScanPerRolePerBarcode()
    {
        await using var ctx = _factory.CreateContext();
        await _factory.SeedUsersAsync(ctx, "seller1", "seller2");
        var product = MakeProduct("SCAN-001");
        ctx.Products.Add(product);
        var barcode = new ProductBarcode { Code = "SCANUNIQ0001", ProductId = product.Id, Status = BarcodeStatus.SellerScanned, RowVersion = [0, 0, 0, 1] };
        ctx.ProductBarcodes.Add(barcode);
        ctx.ScanRecords.Add(new ScanRecord { BarcodeId = barcode.Id, UserId = "seller1", ScannerRole = ScannerRole.Seller, PointsAwarded = 10 });
        await ctx.SaveChangesAsync();

        // Second seller scan on same barcode should fail
        await using var ctx2 = _factory.CreateContext();
        ctx2.ScanRecords.Add(new ScanRecord { BarcodeId = barcode.Id, UserId = "seller2", ScannerRole = ScannerRole.Seller, PointsAwarded = 10 });

        var act = () => ctx2.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>(
            "composite unique (BarcodeId, ScannerRole) prevents duplicate role scans");
    }

    [Fact]
    public async Task ScanRecord_DifferentRolesOnSameBarcode_ShouldSucceed()
    {
        await using var ctx = _factory.CreateContext();
        await _factory.SeedUsersAsync(ctx, "seller1", "tech1");
        var product = MakeProduct("SCAN-002");
        ctx.Products.Add(product);
        var barcode = new ProductBarcode { Code = "SCANMULTI001", ProductId = product.Id, Status = BarcodeStatus.Consumed, RowVersion = [0, 0, 0, 1] };
        ctx.ProductBarcodes.Add(barcode);
        ctx.ScanRecords.AddRange(
            new ScanRecord { BarcodeId = barcode.Id, UserId = "seller1", ScannerRole = ScannerRole.Seller, PointsAwarded = 10 },
            new ScanRecord { BarcodeId = barcode.Id, UserId = "tech1", ScannerRole = ScannerRole.Technician, PointsAwarded = 10 });
        await ctx.SaveChangesAsync();

        var scans = await ctx.ScanRecords.Where(s => s.BarcodeId == barcode.Id).ToListAsync();
        scans.Should().HaveCount(2, "one Seller scan + one Technician scan = valid");
    }

    [Fact]
    public async Task ScanRecord_CannotReferenceNonExistentBarcode()
    {
        await using var ctx = _factory.CreateContext();
        ctx.ScanRecords.Add(new ScanRecord
        {
            BarcodeId = Guid.NewGuid().ToString(),
            UserId = "user1",
            ScannerRole = ScannerRole.Seller,
            PointsAwarded = 10
        });

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("FK to ProductBarcode should be enforced");
    }

    // ═══════════════════════════════════════════════════
    //  WALLET (one-per-user, unique UserId)
    //  Business rule: Each user has exactly one wallet
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task Wallet_DuplicateUserId_ShouldFail()
    {
        await using var ctx = _factory.CreateContext();
        var userId = Guid.NewGuid().ToString();
        ctx.Wallets.AddRange(
            new Wallet { UserId = userId },
            new Wallet { UserId = userId });

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("one wallet per user");
    }

    [Fact]
    public async Task Wallet_HasManyTransactions_WithImmutableSarRate()
    {
        await using var ctx = _factory.CreateContext();
        var walletUserId = Guid.NewGuid().ToString();
        await _factory.SeedUsersAsync(ctx, walletUserId);
        var wallet = new Wallet { UserId = walletUserId, Balance = 200, RowVersion = [0, 0, 0, 1] };
        ctx.Wallets.Add(wallet);

        // SAR rate was 10:1 at earn time
        var tx1 = new WalletTransaction { WalletId = wallet.Id, Amount = 100, Type = WalletTransactionType.Earned, SarRate = 10m, SarAmount = 10m };
        // SAR rate changed to 5:1 later — but this transaction stores its own rate
        var tx2 = new WalletTransaction { WalletId = wallet.Id, Amount = 100, Type = WalletTransactionType.Earned, SarRate = 5m, SarAmount = 20m };
        ctx.WalletTransactions.AddRange(tx1, tx2);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var txns = await verify.WalletTransactions.Where(t => t.WalletId == wallet.Id).OrderBy(t => t.SarRate).ToListAsync();
        txns[0].SarRate.Should().Be(5m, "each transaction stores its own rate");
        txns[1].SarRate.Should().Be(10m, "SAR rate is immutable per transaction");
    }

    [Fact]
    public async Task WalletTransaction_CannotReferenceNonExistentWallet()
    {
        await using var ctx = _factory.CreateContext();
        ctx.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = Guid.NewGuid().ToString(),
            Amount = 50,
            Type = WalletTransactionType.Earned,
            SarRate = 10,
            SarAmount = 5
        });

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("FK to Wallet should be enforced");
    }

    // ═══════════════════════════════════════════════════
    //  WALLET TRANSACTION → SCAN RECORD (via ReferenceId)
    //  Business rule: Earned transactions trace back to the scan
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task WalletTransaction_ReferenceId_LinksTOScanRecord()
    {
        await using var ctx = _factory.CreateContext();
        await _factory.SeedUsersAsync(ctx, "seller1");
        var product = MakeProduct("REF-001");
        ctx.Products.Add(product);
        var barcode = new ProductBarcode { Code = "REFLINK000001", ProductId = product.Id, Status = BarcodeStatus.SellerScanned, RowVersion = [0, 0, 0, 1] };
        ctx.ProductBarcodes.Add(barcode);
        var scan = new ScanRecord { BarcodeId = barcode.Id, UserId = "seller1", ScannerRole = ScannerRole.Seller, PointsAwarded = 100 };
        ctx.ScanRecords.Add(scan);
        var wallet = new Wallet { UserId = "seller1", Balance = 100, RowVersion = [0, 0, 0, 1] };
        ctx.Wallets.Add(wallet);
        var tx = new WalletTransaction
        {
            WalletId = wallet.Id,
            Amount = 100,
            Type = WalletTransactionType.Earned,
            ReferenceId = scan.Id,
            SarRate = 10,
            SarAmount = 10
        };
        ctx.WalletTransactions.Add(tx);
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loadedTx = await verify.WalletTransactions.FirstAsync(t => t.Id == tx.Id);
        loadedTx.ReferenceId.Should().Be(scan.Id, "ReferenceId should point back to the ScanRecord");
        loadedTx.Type.Should().Be(WalletTransactionType.Earned);
    }

    // ═══════════════════════════════════════════════════
    //  REDEMPTION (3-level approval chain: SM → ZM → Admin)
    //  Business rule: Redemption progresses through approval stages
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task RedemptionRequest_ThreeLevelApprovalChain()
    {
        await using var ctx = _factory.CreateContext();
        await _factory.SeedUsersAsync(ctx, "seller1", "sm1", "zm1", "admin1");
        var request = new RedemptionRequest
        {
            UserId = "seller1",
            Method = RedemptionMethod.Cash,
            Status = RedemptionRequestStatus.PendingSalesMan,
            PointsAmount = 1000,
            SarRate = 10,
            SarAmount = 100
        };
        ctx.RedemptionRequests.Add(request);

        // Level 1: SalesMan approves
        ctx.RedemptionApprovals.Add(new RedemptionApproval
        {
            RedemptionRequestId = request.Id,
            ApproverId = "sm1",
            Action = ApprovalAction.Approved,
            FromStatus = RedemptionRequestStatus.PendingSalesMan,
            ToStatus = RedemptionRequestStatus.PendingZoneManager
        });

        // Level 2: ZoneManager approves
        ctx.RedemptionApprovals.Add(new RedemptionApproval
        {
            RedemptionRequestId = request.Id,
            ApproverId = "zm1",
            Action = ApprovalAction.Approved,
            FromStatus = RedemptionRequestStatus.PendingZoneManager,
            ToStatus = RedemptionRequestStatus.PendingAdmin
        });

        // Level 3: Admin approves
        ctx.RedemptionApprovals.Add(new RedemptionApproval
        {
            RedemptionRequestId = request.Id,
            ApproverId = "admin1",
            Action = ApprovalAction.Approved,
            FromStatus = RedemptionRequestStatus.PendingAdmin,
            ToStatus = RedemptionRequestStatus.AdminApproved
        });

        request.Status = RedemptionRequestStatus.AdminApproved;
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.RedemptionRequests.Include(r => r.Approvals).FirstAsync();
        loaded.Approvals.Should().HaveCount(3, "full chain: SM → ZM → Admin");
        loaded.Status.Should().Be(RedemptionRequestStatus.AdminApproved);

        var chain = loaded.Approvals.OrderBy(a => a.FromStatus).ToList();
        chain[0].FromStatus.Should().Be(RedemptionRequestStatus.PendingSalesMan);
        chain[0].ToStatus.Should().Be(RedemptionRequestStatus.PendingZoneManager);
        chain[1].FromStatus.Should().Be(RedemptionRequestStatus.PendingZoneManager);
        chain[1].ToStatus.Should().Be(RedemptionRequestStatus.PendingAdmin);
        chain[2].FromStatus.Should().Be(RedemptionRequestStatus.PendingAdmin);
        chain[2].ToStatus.Should().Be(RedemptionRequestStatus.AdminApproved);
    }

    [Fact]
    public async Task RedemptionRequest_Rejection_CanHappenAtAnyLevel()
    {
        await using var ctx = _factory.CreateContext();
        await _factory.SeedUsersAsync(ctx, "seller1", "zm1");
        var request = new RedemptionRequest
        {
            UserId = "seller1",
            Method = RedemptionMethod.BankTransfer,
            Status = RedemptionRequestStatus.Rejected,
            PointsAmount = 500,
            SarRate = 10,
            SarAmount = 50,
            RejectionReason = "Insufficient documentation",
            RejectedById = "zm1"
        };
        ctx.RedemptionRequests.Add(request);
        ctx.RedemptionApprovals.Add(new RedemptionApproval
        {
            RedemptionRequestId = request.Id,
            ApproverId = "zm1",
            Action = ApprovalAction.Rejected,
            RejectionReason = "Insufficient documentation",
            FromStatus = RedemptionRequestStatus.PendingZoneManager,
            ToStatus = RedemptionRequestStatus.Rejected
        });
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var loaded = await verify.RedemptionRequests.Include(r => r.Approvals).FirstAsync();
        loaded.Status.Should().Be(RedemptionRequestStatus.Rejected);
        loaded.RejectionReason.Should().NotBeNull();
        loaded.Approvals.Should().ContainSingle();
        loaded.Approvals[0].Action.Should().Be(ApprovalAction.Rejected);
    }

    // ═══════════════════════════════════════════════════
    //  REGISTRATION APPROVAL CHAIN (SM → ZM)
    //  Business rule: PendingSalesman → PendingZoneManager → Approved
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task ApprovalRecord_TwoLevelRegistrationChain()
    {
        await using var ctx = _factory.CreateContext();
        var userId = Guid.NewGuid().ToString();
        await _factory.SeedUsersAsync(ctx, userId, "sm1", "zm1");

        ctx.ApprovalRecords.AddRange(
            new ApprovalRecord
            {
                UserId = userId,
                ApproverId = "sm1",
                Action = ApprovalAction.Approved,
                FromStatus = RegistrationStatus.PendingSalesman,
                ToStatus = RegistrationStatus.PendingZoneManager
            },
            new ApprovalRecord
            {
                UserId = userId,
                ApproverId = "zm1",
                Action = ApprovalAction.Approved,
                FromStatus = RegistrationStatus.PendingZoneManager,
                ToStatus = RegistrationStatus.Approved
            });
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var records = await verify.ApprovalRecords
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.FromStatus)
            .ToListAsync();

        records.Should().HaveCount(2);
        records[0].FromStatus.Should().Be(RegistrationStatus.PendingSalesman);
        records[0].ToStatus.Should().Be(RegistrationStatus.PendingZoneManager);
        records[1].FromStatus.Should().Be(RegistrationStatus.PendingZoneManager);
        records[1].ToStatus.Should().Be(RegistrationStatus.Approved);
    }

    // ═══════════════════════════════════════════════════
    //  PROFILE UNIQUENESS
    //  Business rule: One profile per user per type
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task ShopOwnerProfile_DuplicateUserId_ShouldFail()
    {
        await using var ctx = _factory.CreateContext();
        var erp = new ErpCustomer { CustomerCode = "PROF-A", CustomerName = "A" };
        var erp2 = new ErpCustomer { CustomerCode = "PROF-B", CustomerName = "B" };
        ctx.ErpCustomers.AddRange(erp, erp2);
        var userId = Guid.NewGuid().ToString();

        ctx.ShopOwnerProfiles.AddRange(
            new ShopOwnerProfile { UserId = userId, CustomerCode = "PROF-A" },
            new ShopOwnerProfile { UserId = userId, CustomerCode = "PROF-B" });

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("one ShopOwnerProfile per user");
    }

    [Fact]
    public async Task SellerProfile_DuplicateUserId_ShouldFail()
    {
        await using var ctx = _factory.CreateContext();
        ctx.ErpCustomers.AddRange(
            new ErpCustomer { CustomerCode = "SEL-A", CustomerName = "A" },
            new ErpCustomer { CustomerCode = "SEL-B", CustomerName = "B" });
        var userId = Guid.NewGuid().ToString();

        ctx.SellerProfiles.AddRange(
            new SellerProfile { UserId = userId, CustomerCode = "SEL-A" },
            new SellerProfile { UserId = userId, CustomerCode = "SEL-B" });

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("one SellerProfile per user");
    }

    // ═══════════════════════════════════════════════════
    //  SOFT DELETE + UNIQUE CONSTRAINTS
    //  Business rule: Soft-deleted records don't block new ones
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task Product_SoftDeleted_SameCodeCanBeReused()
    {
        await using var ctx = _factory.CreateContext();
        var product = MakeProduct("REUSE-001");
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        // Soft-delete
        ctx.Products.Remove(product);
        await ctx.SaveChangesAsync();

        // Verify soft-deleted (still in DB)
        var deleted = await ctx.Products.IgnoreQueryFilters().FirstAsync(p => p.ProductCode == "REUSE-001");
        deleted.IsDeleted.Should().BeTrue();

        // Create new product with same code — should work because filter is [IsDeleted] = 0
        await using var ctx2 = _factory.CreateContext();
        ctx2.Products.Add(MakeProduct("REUSE-001"));
        await ctx2.SaveChangesAsync();

        var all = await ctx2.Products.IgnoreQueryFilters().Where(p => p.ProductCode == "REUSE-001").ToListAsync();
        all.Should().HaveCount(2, "soft-deleted + new active = 2 rows, no unique violation");
    }

    // ═══════════════════════════════════════════════════
    //  NOTIFICATION PREFERENCES (composite key)
    //  Business rule: One preference per user per notification type
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task UserNotificationPreference_DuplicateCompositeKey_ShouldFail()
    {
        await using var ctx = _factory.CreateContext();
        var userId = Guid.NewGuid().ToString();
        await _factory.SeedUsersAsync(ctx, userId);

        ctx.UserNotificationPreferences.Add(
            new UserNotificationPreference { UserId = userId, NotificationType = NotificationType.PointsEarned });
        await ctx.SaveChangesAsync();

        // Use a new context to bypass change tracker composite key detection
        await using var ctx2 = _factory.CreateContext();
        ctx2.UserNotificationPreferences.Add(
            new UserNotificationPreference { UserId = userId, NotificationType = NotificationType.PointsEarned });

        var act = () => ctx2.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("composite PK prevents duplicate preferences");
    }

    [Fact]
    public async Task UserNotificationPreference_DifferentTypes_ShouldSucceed()
    {
        await using var ctx = _factory.CreateContext();
        var userId = Guid.NewGuid().ToString();
        await _factory.SeedUsersAsync(ctx, userId);

        ctx.UserNotificationPreferences.AddRange(
            new UserNotificationPreference { UserId = userId, NotificationType = NotificationType.PointsEarned, IsPushMuted = true },
            new UserNotificationPreference { UserId = userId, NotificationType = NotificationType.AdminMessage, IsPushMuted = false });
        await ctx.SaveChangesAsync();

        var prefs = await ctx.UserNotificationPreferences.Where(p => p.UserId == userId).ToListAsync();
        prefs.Should().HaveCount(2);
    }

    // ═══════════════════════════════════════════════════
    //  OTP CODE (non-TrackableEntity, unique PinId)
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task OtpCode_DuplicatePinId_ShouldFail()
    {
        await using var ctx = _factory.CreateContext();
        ctx.OtpCodes.AddRange(
            new Domain.Entities.OTP.OtpCode { PinId = "VE-SAME-PIN", MobileNumber = "+966555000001" },
            new Domain.Entities.OTP.OtpCode { PinId = "VE-SAME-PIN", MobileNumber = "+966555000002" });

        var act = () => ctx.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>("PinId must be unique");
    }

    // ═══════════════════════════════════════════════════
    //  CONTENT ENTITIES (singletons)
    // ═══════════════════════════════════════════════════

    [Fact]
    public async Task AboutAppContent_Persists()
    {
        await using var ctx = _factory.CreateContext();
        ctx.AboutAppContents.Add(new AboutAppContent { Content = "About text" });
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var content = await verify.AboutAppContents.FirstAsync();
        content.Content.Should().Be("About text");
    }

    [Fact]
    public async Task ContactUsContent_PersistsAllFields()
    {
        await using var ctx = _factory.CreateContext();
        ctx.ContactUsContents.Add(new ContactUsContent
        {
            Phone = "+966500000000",
            Email = "test@test.com",
            WhatsApp = "+966500000000",
            Address = "Riyadh",
            WorkingHours = "9-5"
        });
        await ctx.SaveChangesAsync();

        await using var verify = _factory.CreateContext();
        var content = await verify.ContactUsContents.FirstAsync();
        content.Email.Should().Be("test@test.com");
    }

    // ═══════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════

    private static async Task<(Region region, City city)> SeedRegionAndCity(
        Infrastructure.Persistance.ApplicationDbContext ctx)
    {
        var region = new Region { NameAr = "الرياض", NameEn = "Riyadh" };
        ctx.Regions.Add(region);
        var city = new City { NameAr = "الرياض", NameEn = "Riyadh", RegionId = region.Id };
        ctx.Cities.Add(city);
        await ctx.SaveChangesAsync();
        return (region, city);
    }

    private static ShopData MakeShopData(string customerCode, string cityId, string vat, string crn, string shortAddr) => new()
    {
        CustomerCode = customerCode,
        StoreName = "Store " + customerCode,
        VAT = vat,
        CRN = crn,
        ShopImageUrl = "/img.png",
        ShortAddress = shortAddr,
        District = "النسيم",
        CityId = cityId,
        Street = "شارع الملك",
        PostalCode = "12345",
        BuildingNumber = 1000,
        SubNumber = 1000,
        EnteredByUserId = "user1"
    };

    private static Product MakeProduct(string code) => new()
    {
        Name = "Product " + code,
        ProductCode = code,
        PointValue = 10,
        Price = 50m,
        Category = "Test"
    };
}
