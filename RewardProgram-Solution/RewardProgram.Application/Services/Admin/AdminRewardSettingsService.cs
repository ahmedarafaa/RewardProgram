using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Admin.RewardSettings;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Entities;

namespace RewardProgram.Application.Services.Admin;

public class AdminRewardSettingsService : IAdminRewardSettingsService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AdminRewardSettingsService> _logger;

    public AdminRewardSettingsService(
        IApplicationDbContext context,
        ILogger<AdminRewardSettingsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<RewardSettingsResponse>> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await GetOrCreateSettingsAsync(ct);
        return Result.Success(MapToResponse(settings));
    }

    public async Task<Result<RewardSettingsResponse>> UpdateSettingsAsync(
        UpdateRewardSettingsRequest request, string adminUserId, CancellationToken ct = default)
    {
        var settings = await GetOrCreateSettingsAsync(ct);

        var oldRate = settings.PointsToSarRate;
        settings.PointsToSarRate = request.PointsToSarRate;
        settings.InviterRewardPoints = request.InviterRewardPoints;
        settings.InviteeRewardPoints = request.InviteeRewardPoints;
        settings.MinimumRedemptionPoints = request.MinimumRedemptionPoints;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "RewardSettings updated by admin {AdminId}: PointsToSarRate changed from {OldRate} to {NewRate}",
            adminUserId, oldRate, request.PointsToSarRate);

        return Result.Success(MapToResponse(settings));
    }

    private async Task<RewardSettings> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        var settings = await _context.RewardSettings.FirstOrDefaultAsync(ct);

        if (settings is not null)
            return settings;

        settings = new RewardSettings
        {
            PointsToSarRate = 10m
        };

        try
        {
            await _context.RewardSettings.AddAsync(settings, ct);
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Concurrent insert won the race — re-query the existing row
            _logger.LogWarning("Concurrent RewardSettings creation detected, re-querying");
            settings = await _context.RewardSettings.FirstAsync(ct);
        }

        return settings;
    }

    private static RewardSettingsResponse MapToResponse(RewardSettings settings)
    {
        return new RewardSettingsResponse(
            settings.Id,
            settings.PointsToSarRate,
            settings.InviterRewardPoints,
            settings.InviteeRewardPoints,
            settings.MinimumRedemptionPoints);
    }
}
