using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RewardProgram.Domain.Entities;
using RewardProgram.Infrastructure.Persistance;
using System.Net;

namespace RewardProgram.API.Tests;

public class ContentEndpointsTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ContentEndpointsTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public async ValueTask InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ctx.ContactUsContents.Add(new ContactUsContent
        {
            Phone = "0500000000",
            Email = "test@raed.com",
            WhatsApp = "0500000000",
            Address = "Riyadh",
            WorkingHours = "9-5"
        });

        ctx.AboutAppContents.Add(new AboutAppContent { Content = "Welcome to Raed" });
        await ctx.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task GetContactUs_Public_ShouldReturn200()
    {
        var response = await _client.GetAsync("/api/content/contact-us");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("0500000000");
    }

    [Fact]
    public async Task GetAboutApp_Public_ShouldReturn200()
    {
        var response = await _client.GetAsync("/api/content/about-app");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Welcome to Raed");
    }
}
