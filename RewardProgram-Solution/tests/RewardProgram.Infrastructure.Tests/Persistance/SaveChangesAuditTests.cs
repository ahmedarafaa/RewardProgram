using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Infrastructure.Tests.Persistance;

public class SaveChangesAuditTests : IDisposable
{
    private const string TestUserId = "test-admin-id";
    private readonly TestDbContextFactory _factory;

    public SaveChangesAuditTests()
    {
        _factory = new TestDbContextFactory(TestUserId);
    }

    public void Dispose() => _factory.Dispose();

    // ── Created ──

    [Fact]
    public async Task Add_ShouldSet_CreatedAtAndCreatedBy()
    {
        await using var ctx = _factory.CreateContext();

        var before = DateTime.UtcNow;
        var region = new Region { NameAr = "منطقة", NameEn = "Region" };
        ctx.Regions.Add(region);
        await ctx.SaveChangesAsync();
        var after = DateTime.UtcNow;

        region.CreatedBy.Should().Be(TestUserId);
        region.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Add_WithNoUser_ShouldSet_CreatedByToNull()
    {
        using var anonFactory = new TestDbContextFactory(userId: null);
        await using var ctx = anonFactory.CreateContext();

        var region = new Region { NameAr = "منطقة", NameEn = "Region" };
        ctx.Regions.Add(region);
        await ctx.SaveChangesAsync();

        region.CreatedBy.Should().BeNull();
    }

    // ── Modified ──

    [Fact]
    public async Task Modify_ShouldSet_UpdatedAtAndUpdatedBy()
    {
        await using var ctx = _factory.CreateContext();
        var region = new Region { NameAr = "منطقة", NameEn = "Region" };
        ctx.Regions.Add(region);
        await ctx.SaveChangesAsync();

        var before = DateTime.UtcNow;
        region.NameEn = "Updated Region";
        await ctx.SaveChangesAsync();
        var after = DateTime.UtcNow;

        region.UpdatedBy.Should().Be(TestUserId);
        region.UpdatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Modify_ShouldNotChange_CreatedFields()
    {
        await using var ctx = _factory.CreateContext();
        var region = new Region { NameAr = "منطقة", NameEn = "Region" };
        ctx.Regions.Add(region);
        await ctx.SaveChangesAsync();

        var originalCreatedAt = region.CreatedAt;
        var originalCreatedBy = region.CreatedBy;

        region.NameEn = "Updated";
        await ctx.SaveChangesAsync();

        region.CreatedAt.Should().Be(originalCreatedAt);
        region.CreatedBy.Should().Be(originalCreatedBy);
    }

    // ── Soft Delete ──

    [Fact]
    public async Task Delete_ShouldConvertToSoftDelete()
    {
        await using var ctx = _factory.CreateContext();
        var region = new Region { NameAr = "منطقة", NameEn = "Region" };
        ctx.Regions.Add(region);
        await ctx.SaveChangesAsync();

        var before = DateTime.UtcNow;
        ctx.Regions.Remove(region);
        await ctx.SaveChangesAsync();
        var after = DateTime.UtcNow;

        // Entity should still exist in DB but marked deleted
        await using var verifyCtx = _factory.CreateContext();
        var found = await verifyCtx.Regions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == region.Id);

        found.Should().NotBeNull();
        found!.IsDeleted.Should().BeTrue();
        found.DeletedBy.Should().Be(TestUserId);
        found.DeletedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Delete_ShouldNotPhysicallyRemoveRow()
    {
        await using var ctx = _factory.CreateContext();
        var region = new Region { NameAr = "منطقة", NameEn = "Region" };
        ctx.Regions.Add(region);
        await ctx.SaveChangesAsync();

        ctx.Regions.Remove(region);
        await ctx.SaveChangesAsync();

        // Count with IgnoreQueryFilters to see actual row
        await using var verifyCtx = _factory.CreateContext();
        var totalRows = await verifyCtx.Regions.IgnoreQueryFilters().CountAsync();
        totalRows.Should().Be(1);
    }

    // ── Multiple entities ──

    [Fact]
    public async Task SaveChanges_ShouldAuditMultipleEntities()
    {
        await using var ctx = _factory.CreateContext();

        var product1 = new Product { Name = "P1", ProductCode = "PC001", PointValue = 10, Price = 100, Category = "Cat" };
        var product2 = new Product { Name = "P2", ProductCode = "PC002", PointValue = 20, Price = 200, Category = "Cat" };
        ctx.Products.AddRange(product1, product2);
        await ctx.SaveChangesAsync();

        product1.CreatedBy.Should().Be(TestUserId);
        product2.CreatedBy.Should().Be(TestUserId);
    }

    [Fact]
    public async Task MixedOperations_ShouldAuditEachCorrectly()
    {
        await using var ctx = _factory.CreateContext();

        // Add
        var product1 = new Product { Name = "P1", ProductCode = "PC001", PointValue = 10, Price = 100, Category = "Cat" };
        var product2 = new Product { Name = "P2", ProductCode = "PC002", PointValue = 20, Price = 200, Category = "Cat" };
        ctx.Products.AddRange(product1, product2);
        await ctx.SaveChangesAsync();

        // Modify one, delete other
        product1.Name = "P1-updated";
        ctx.Products.Remove(product2);
        await ctx.SaveChangesAsync();

        product1.UpdatedBy.Should().Be(TestUserId);
        product1.UpdatedAt.Should().NotBeNull();

        // product2 should be soft-deleted
        await using var verifyCtx = _factory.CreateContext();
        var deleted = await verifyCtx.Products.IgnoreQueryFilters()
            .FirstAsync(p => p.Id == product2.Id);
        deleted.IsDeleted.Should().BeTrue();
        deleted.DeletedBy.Should().Be(TestUserId);
    }
}
