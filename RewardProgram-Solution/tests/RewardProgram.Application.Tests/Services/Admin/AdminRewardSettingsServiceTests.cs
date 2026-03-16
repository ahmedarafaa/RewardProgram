using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Contracts.Admin.RewardSettings;
using RewardProgram.Application.Services.Admin;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities;

namespace RewardProgram.Application.Tests.Services.Admin;

public class AdminRewardSettingsServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly AdminRewardSettingsService _sut;
    private const string AdminId = "admin-1";

    public AdminRewardSettingsServiceTests()
    {
        _context = TestDbContext.Create();
        _sut = new AdminRewardSettingsService(_context, Substitute.For<ILogger<AdminRewardSettingsService>>());
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetSettings_NoExistingRecord_ShouldCreateWithDefault10()
    {
        var result = await _sut.GetSettingsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.PointsToSarRate.Should().Be(10m);
    }

    [Fact]
    public async Task GetSettings_ExistingRecord_ShouldReturnExistingRate()
    {
        _context.RewardSettings.Add(new RewardSettings { PointsToSarRate = 5m });
        await _context.SaveChangesAsync();

        var result = await _sut.GetSettingsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.PointsToSarRate.Should().Be(5m);
    }

    [Fact]
    public async Task UpdateSettings_ShouldChangeRate()
    {
        _context.RewardSettings.Add(new RewardSettings { PointsToSarRate = 10m });
        await _context.SaveChangesAsync();

        var request = new UpdateRewardSettingsRequest(20m, 100m, 50m);
        var result = await _sut.UpdateSettingsAsync(request, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.PointsToSarRate.Should().Be(20m);
    }

    [Fact]
    public async Task UpdateSettings_NoExistingRecord_ShouldCreateAndUpdate()
    {
        var request = new UpdateRewardSettingsRequest(15m, 100m, 50m);
        var result = await _sut.UpdateSettingsAsync(request, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.PointsToSarRate.Should().Be(15m);
    }
}
