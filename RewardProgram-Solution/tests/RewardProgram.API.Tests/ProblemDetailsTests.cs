using FluentAssertions;
using System.Net;

namespace RewardProgram.API.Tests;

public class ProblemDetailsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProblemDetailsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task NotFoundRegion_ShouldReturnProblemDetails()
    {
        var response = await _client.GetAsync("/api/lookup/regions/nonexistent/cities");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("status");
        body.Should().Contain("error");
    }

    [Fact]
    public async Task NonExistentEndpoint_ShouldReturn404()
    {
        var response = await _client.GetAsync("/api/this-does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CustomerShopDataStatus_NonExistentCode_ShouldReturn404()
    {
        var response = await _client.GetAsync("/api/lookup/customer/INVALID/shop-data-status");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("error");
    }
}
