using FluentAssertions;
using RewardProgram.Domain.Constants;
using System.Net;
using System.Net.Http.Json;

namespace RewardProgram.API.Tests;

public class AuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthorizationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Unauthenticated access to protected endpoints ──

    [Theory]
    [InlineData("/api/wallet/balance")]
    [InlineData("/api/wallet/transactions")]
    [InlineData("/api/scan/history")]
    [InlineData("/api/notifications")]
    [InlineData("/api/profile")]
    [InlineData("/api/invitation")]
    [InlineData("/api/dashboard")]
    public async Task ProtectedEndpoint_WithoutToken_ShouldReturn401(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Admin endpoints require SystemAdmin role ──

    [Theory]
    [InlineData("/api/admin/content/contact-us")]
    [InlineData("/api/admin/content/about-app")]
    [InlineData("/api/admin/dashboard")]
    [InlineData("/api/admin/users")]
    public async Task AdminEndpoint_WithSellerToken_ShouldReturn403(string url)
    {
        JwtTokenHelper.AddAuthHeader(_client, "seller-1", UserRoles.Seller);

        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/admin/content/contact-us")]
    [InlineData("/api/admin/content/about-app")]
    public async Task AdminEndpoint_WithAdminToken_ShouldReturn200(string url)
    {
        var client = new TestWebApplicationFactory().CreateClient();
        JwtTokenHelper.AddAuthHeader(client, "admin-1", UserRoles.SystemAdmin);

        var response = await client.GetAsync(url);

        // These return 200 because they auto-create defaults
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Role-restricted endpoints ──

    [Fact]
    public async Task WalletBalance_WithAdminToken_ShouldReturn403()
    {
        var client = new TestWebApplicationFactory().CreateClient();
        JwtTokenHelper.AddAuthHeader(client, "admin-1", UserRoles.SystemAdmin);

        var response = await client.GetAsync("/api/wallet/balance");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WalletBalance_WithSellerToken_ShouldNotReturn401Or403()
    {
        var client = new TestWebApplicationFactory().CreateClient();
        JwtTokenHelper.AddAuthHeader(client, "seller-1", UserRoles.Seller);

        var response = await client.GetAsync("/api/wallet/balance");

        // May return 404 if wallet not found, but NOT 401/403
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }
}
