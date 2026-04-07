using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Contracts.Admin.Content;
using RewardProgram.Application.Services.Admin;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities;

namespace RewardProgram.Application.Tests.Services.Admin;

public class AdminContentServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly AdminContentService _sut;

    public AdminContentServiceTests()
    {
        _context = TestDbContext.Create();
        _sut = new AdminContentService(_context, Substitute.For<ILogger<AdminContentService>>());
    }

    public void Dispose() => _context.Dispose();

    // ── ContactUs ──

    [Fact]
    public async Task GetContactUs_NoExistingRecord_ShouldCreateDefault()
    {
        var result = await _sut.GetContactUsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Phone.Should().BeEmpty();
        result.Value.Email.Should().BeEmpty();
        _context.ContactUsContents.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetContactUs_ExistingRecord_ShouldReturnExisting()
    {
        _context.ContactUsContents.Add(new ContactUsContent
        {
            Phone = "0500000000", Email = "test@test.com",
            WhatsApp = "0500000000", Address = "Riyadh", WorkingHours = "9-5"
        });
        await _context.SaveChangesAsync();

        var result = await _sut.GetContactUsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Phone.Should().Be("0500000000");
        result.Value.Email.Should().Be("test@test.com");
    }

    [Fact]
    public async Task UpdateContactUs_ShouldUpdateAllFields()
    {
        // Seed existing
        _context.ContactUsContents.Add(new ContactUsContent());
        await _context.SaveChangesAsync();

        var request = new UpdateContactUsRequest("0599999999", "new@test.com", "0599999999", "Jeddah", "8-4");

        var result = await _sut.UpdateContactUsAsync(request, "admin1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Phone.Should().Be("0599999999");
        result.Value.Email.Should().Be("new@test.com");
        result.Value.WhatsApp.Should().Be("0599999999");
        result.Value.Address.Should().Be("Jeddah");
        result.Value.WorkingHours.Should().Be("8-4");
    }

    [Fact]
    public async Task UpdateContactUs_NoExistingRecord_ShouldCreateAndUpdate()
    {
        var request = new UpdateContactUsRequest("0500000000", "a@b.com", "0500000000", "Addr", "9-5");

        var result = await _sut.UpdateContactUsAsync(request, "admin1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Phone.Should().Be("0500000000");
        _context.ContactUsContents.Should().HaveCount(1);
    }

    // ── AboutApp ──

    [Fact]
    public async Task GetAboutApp_NoExistingRecord_ShouldCreateDefault()
    {
        var result = await _sut.GetAboutAppAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().BeEmpty();
        _context.AboutAppContents.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAboutApp_ExistingRecord_ShouldReturnExisting()
    {
        _context.AboutAppContents.Add(new AboutAppContent { Content = "Welcome to our app" });
        await _context.SaveChangesAsync();

        var result = await _sut.GetAboutAppAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("Welcome to our app");
    }

    [Fact]
    public async Task UpdateAboutApp_ShouldUpdateContent()
    {
        _context.AboutAppContents.Add(new AboutAppContent { Content = "Old content" });
        await _context.SaveChangesAsync();

        var result = await _sut.UpdateAboutAppAsync(new UpdateAboutAppRequest("New content"), "admin1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("New content");
    }

    [Fact]
    public async Task UpdateAboutApp_NoExistingRecord_ShouldCreateAndUpdate()
    {
        var result = await _sut.UpdateAboutAppAsync(new UpdateAboutAppRequest("App info"), "admin1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("App info");
        _context.AboutAppContents.Should().HaveCount(1);
    }

    // ── Idempotency ──

    [Fact]
    public async Task GetContactUs_CalledTwice_ShouldNotDuplicate()
    {
        await _sut.GetContactUsAsync();
        await _sut.GetContactUsAsync();

        _context.ContactUsContents.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAboutApp_CalledTwice_ShouldNotDuplicate()
    {
        await _sut.GetAboutAppAsync();
        await _sut.GetAboutAppAsync();

        _context.AboutAppContents.Should().HaveCount(1);
    }
}
