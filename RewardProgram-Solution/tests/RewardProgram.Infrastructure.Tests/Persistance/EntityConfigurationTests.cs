using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Infrastructure.Tests.Persistance;

public class EntityConfigurationTests : IDisposable
{
    private readonly TestDbContextFactory _factory;

    public EntityConfigurationTests()
    {
        _factory = new TestDbContextFactory("admin");
    }

    public void Dispose() => _factory.Dispose();

    // ── Cascade Delete Disabled ──

    [Fact]
    public void AllForeignKeys_ShouldHaveRestrictDeleteBehavior()
    {
        using var ctx = _factory.CreateContext();
        var model = ctx.Model;

        var cascadeFKs = model.GetEntityTypes()
            .SelectMany(t => t.GetForeignKeys())
            .Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade && !fk.IsOwnership)
            .ToList();

        cascadeFKs.Should().BeEmpty(
            "all non-ownership FKs should use Restrict delete, not Cascade");
    }

    // ── Soft Delete Query Filters Registered ──

    [Fact]
    public void AllTrackableEntities_ShouldHaveQueryFilter()
    {
        using var ctx = _factory.CreateContext();
        var model = ctx.Model;

        var trackableTypes = model.GetEntityTypes()
            .Where(t => typeof(TrackableEntity).IsAssignableFrom(t.ClrType))
            .ToList();

        trackableTypes.Should().NotBeEmpty();

        foreach (var entityType in trackableTypes)
        {
            entityType.GetQueryFilter().Should().NotBeNull(
                $"{entityType.ClrType.Name} should have a soft-delete query filter");
        }
    }

    // ── DbSet Registration ──

    [Fact]
    public void DbContext_ShouldHaveAllExpectedDbSets()
    {
        using var ctx = _factory.CreateContext();

        ctx.Regions.Should().NotBeNull();
        ctx.Cities.Should().NotBeNull();
        ctx.ErpCustomers.Should().NotBeNull();
        ctx.ShopData.Should().NotBeNull();
        ctx.ShopOwnerProfiles.Should().NotBeNull();
        ctx.SellerProfiles.Should().NotBeNull();
        ctx.TechnicianProfiles.Should().NotBeNull();
        ctx.Products.Should().NotBeNull();
        ctx.ProductBarcodes.Should().NotBeNull();
        ctx.ScanRecords.Should().NotBeNull();
        ctx.Wallets.Should().NotBeNull();
        ctx.WalletTransactions.Should().NotBeNull();
        ctx.RewardSettings.Should().NotBeNull();
        ctx.RedemptionRequests.Should().NotBeNull();
        ctx.RedemptionApprovals.Should().NotBeNull();
        ctx.Notifications.Should().NotBeNull();
        ctx.ContactUsContents.Should().NotBeNull();
        ctx.AboutAppContents.Should().NotBeNull();
        ctx.OtpCodes.Should().NotBeNull();
    }

    // ── Product CRUD ──

    [Fact]
    public async Task Product_CanBeCreatedAndRetrieved()
    {
        await using var ctx = _factory.CreateContext();

        var product = new Product
        {
            Name = "Test Product",
            ProductCode = "TP001",
            PointValue = 5,
            Price = 49.99m,
            Category = "TestCat"
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        await using var verifyCtx = _factory.CreateContext();
        var found = await verifyCtx.Products.FirstAsync(p => p.ProductCode == "TP001");
        found.Name.Should().Be("Test Product");
        found.PointValue.Should().Be(5);
    }

    // ── Wallet Defaults ──

    [Fact]
    public async Task Wallet_ShouldPersistWithDefaultZeroBalances()
    {
        await using var ctx = _factory.CreateContext();

        var wallet = new Wallet { UserId = Guid.NewGuid().ToString() };
        ctx.Wallets.Add(wallet);
        await ctx.SaveChangesAsync();

        await using var verifyCtx = _factory.CreateContext();
        var found = await verifyCtx.Wallets.FirstAsync(w => w.Id == wallet.Id);
        found.Balance.Should().Be(0m);
        found.SarBalance.Should().Be(0m);
        found.HeldBalance.Should().Be(0m);
        found.HeldSarBalance.Should().Be(0m);
    }

    // ── Region ──

    [Fact]
    public async Task Region_ShouldPersistAndRetrieve()
    {
        await using var ctx = _factory.CreateContext();

        var region = new Region { NameAr = "الرياض", NameEn = "Riyadh" };
        ctx.Regions.Add(region);
        await ctx.SaveChangesAsync();

        await using var verifyCtx = _factory.CreateContext();
        var found = await verifyCtx.Regions.FirstAsync(r => r.Id == region.Id);
        found.NameAr.Should().Be("الرياض");
        found.NameEn.Should().Be("Riyadh");
        found.IsActive.Should().BeTrue();
    }

    // ── Notification ──

    [Fact]
    public async Task Notification_ShouldPersistAllFields()
    {
        await using var ctx = _factory.CreateContext();

        var notification = new Notification
        {
            UserId = Guid.NewGuid().ToString(),
            Type = Domain.Enums.NotificationType.PointsEarned,
            Title = "Points Earned",
            Body = "You earned 10 points"
        };
        ctx.Notifications.Add(notification);
        await ctx.SaveChangesAsync();

        await using var verifyCtx = _factory.CreateContext();
        var found = await verifyCtx.Notifications.FirstAsync(n => n.Id == notification.Id);
        found.Title.Should().Be("Points Earned");
        found.IsRead.Should().BeFalse();
    }

    // ── RewardSettings ──

    [Fact]
    public async Task RewardSettings_ShouldPersistDefaults()
    {
        await using var ctx = _factory.CreateContext();

        var settings = new RewardSettings();
        ctx.RewardSettings.Add(settings);
        await ctx.SaveChangesAsync();

        await using var verifyCtx = _factory.CreateContext();
        var found = await verifyCtx.RewardSettings.FirstAsync();
        found.PointsToSarRate.Should().Be(10m);
        found.InviterRewardPoints.Should().Be(100m);
        found.InviteeRewardPoints.Should().Be(50m);
        found.MinimumRedemptionPoints.Should().Be(1000m);
    }
}
