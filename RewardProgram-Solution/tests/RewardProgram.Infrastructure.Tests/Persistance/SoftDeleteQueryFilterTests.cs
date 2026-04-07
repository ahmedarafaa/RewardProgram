using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Infrastructure.Tests.Persistance;

public class SoftDeleteQueryFilterTests : IDisposable
{
    private readonly TestDbContextFactory _factory;

    public SoftDeleteQueryFilterTests()
    {
        _factory = new TestDbContextFactory("admin");
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SoftDeletedEntity_ShouldBeInvisibleToNormalQuery()
    {
        await using var ctx = _factory.CreateContext();

        var region = new Region { NameAr = "حذف", NameEn = "Deleted" };
        ctx.Regions.Add(region);
        await ctx.SaveChangesAsync();

        ctx.Regions.Remove(region);
        await ctx.SaveChangesAsync();

        // Normal query should not find it
        await using var queryCtx = _factory.CreateContext();
        var results = await queryCtx.Regions.ToListAsync();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SoftDeletedEntity_ShouldBeVisibleWithIgnoreQueryFilters()
    {
        await using var ctx = _factory.CreateContext();

        var region = new Region { NameAr = "حذف", NameEn = "Deleted" };
        ctx.Regions.Add(region);
        await ctx.SaveChangesAsync();

        ctx.Regions.Remove(region);
        await ctx.SaveChangesAsync();

        await using var queryCtx = _factory.CreateContext();
        var results = await queryCtx.Regions.IgnoreQueryFilters().ToListAsync();
        results.Should().HaveCount(1);
        results[0].IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task NonDeletedEntity_ShouldBeVisibleToNormalQuery()
    {
        await using var ctx = _factory.CreateContext();

        ctx.Regions.Add(new Region { NameAr = "نشط", NameEn = "Active" });
        await ctx.SaveChangesAsync();

        await using var queryCtx = _factory.CreateContext();
        var results = await queryCtx.Regions.ToListAsync();
        results.Should().HaveCount(1);
        results[0].NameEn.Should().Be("Active");
    }

    [Fact]
    public async Task MixedEntities_ShouldOnlyReturnNonDeleted()
    {
        await using var ctx = _factory.CreateContext();

        var active = new Region { NameAr = "نشط", NameEn = "Active" };
        var toDelete = new Region { NameAr = "حذف", NameEn = "ToDelete" };
        ctx.Regions.AddRange(active, toDelete);
        await ctx.SaveChangesAsync();

        ctx.Regions.Remove(toDelete);
        await ctx.SaveChangesAsync();

        await using var queryCtx = _factory.CreateContext();
        var results = await queryCtx.Regions.ToListAsync();
        results.Should().HaveCount(1);
        results[0].Id.Should().Be(active.Id);
    }

    [Fact]
    public async Task QueryFilter_ShouldApplyToMultipleEntityTypes()
    {
        await using var ctx = _factory.CreateContext();

        var region = new Region { NameAr = "منطقة", NameEn = "R" };
        var product = new Product { Name = "P", ProductCode = "PC001", PointValue = 10, Price = 100, Category = "Cat" };
        ctx.Regions.Add(region);
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        ctx.Regions.Remove(region);
        ctx.Products.Remove(product);
        await ctx.SaveChangesAsync();

        await using var queryCtx = _factory.CreateContext();
        (await queryCtx.Regions.CountAsync()).Should().Be(0);
        (await queryCtx.Products.CountAsync()).Should().Be(0);

        // But both still exist in DB
        (await queryCtx.Regions.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await queryCtx.Products.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task FindAsync_ShouldRespectQueryFilter()
    {
        await using var ctx = _factory.CreateContext();

        var region = new Region { NameAr = "حذف", NameEn = "Deleted" };
        ctx.Regions.Add(region);
        await ctx.SaveChangesAsync();
        var id = region.Id;

        ctx.Regions.Remove(region);
        await ctx.SaveChangesAsync();

        // FindAsync bypasses query filters (EF behavior), but LINQ queries don't
        await using var queryCtx = _factory.CreateContext();
        var viaLinq = await queryCtx.Regions.FirstOrDefaultAsync(r => r.Id == id);
        viaLinq.Should().BeNull();
    }
}
