using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Infrastructure.Persistance;
using System.Net;

namespace RewardProgram.API.Tests;

public class LookupEndpointsTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LookupEndpointsTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public async ValueTask InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var region = new Region { Id = "r1", NameAr = "الرياض", NameEn = "Riyadh" };
        ctx.Regions.Add(region);
        ctx.Cities.Add(new City { Id = "c1", NameAr = "الرياض", NameEn = "Riyadh", RegionId = "r1" });
        ctx.Cities.Add(new City { Id = "c2", NameAr = "الخرج", NameEn = "Al Kharj", RegionId = "r1" });
        await ctx.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task GetRegions_ShouldReturn200()
    {
        var response = await _client.GetAsync("/api/lookup/regions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Riyadh");
    }

    [Fact]
    public async Task GetCitiesByRegion_ValidRegion_ShouldReturn200()
    {
        var response = await _client.GetAsync("/api/lookup/regions/r1/cities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Al Kharj");
    }

    [Fact]
    public async Task GetCitiesByRegion_InvalidRegion_ShouldReturn404()
    {
        var response = await _client.GetAsync("/api/lookup/regions/nonexistent/cities");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllCities_ShouldReturn200()
    {
        var response = await _client.GetAsync("/api/lookup/cities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Riyadh");
    }
}
