using FluentAssertions;
using RewardProgram.Domain.Constants;
using System.Net;
using System.Net.Http.Json;

namespace RewardProgram.API.Tests;

public class AdminContentEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AdminContentEndpointsTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        JwtTokenHelper.AddAuthHeader(_client, "admin-1", UserRoles.SystemAdmin);
    }

    [Fact]
    public async Task GetContactUs_ShouldAutoCreateDefault()
    {
        var response = await _client.GetAsync("/api/admin/content/contact-us");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("phone");
    }

    [Fact]
    public async Task UpdateContactUs_ShouldReturn200()
    {
        var payload = new
        {
            phone = "0599999999",
            email = "contact@raed.com",
            whatsApp = "0599999999",
            address = "Riyadh, KSA",
            workingHours = "9 AM - 5 PM"
        };

        var response = await _client.PutAsJsonAsync("/api/admin/content/contact-us", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("0599999999");
    }

    [Fact]
    public async Task GetAboutApp_ShouldAutoCreateDefault()
    {
        var response = await _client.GetAsync("/api/admin/content/about-app");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateAboutApp_ShouldReturn200()
    {
        var payload = new { content = "About Raed Rewards" };

        var response = await _client.PutAsJsonAsync("/api/admin/content/about-app", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("About Raed Rewards");
    }
}
