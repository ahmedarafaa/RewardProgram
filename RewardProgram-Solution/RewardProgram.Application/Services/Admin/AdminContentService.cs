using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Admin.Content;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Entities;

namespace RewardProgram.Application.Services.Admin;

public class AdminContentService : IAdminContentService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AdminContentService> _logger;

    public AdminContentService(
        IApplicationDbContext context,
        ILogger<AdminContentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Contact Us

    public async Task<Result<ContactUsResponse>> GetContactUsAsync(CancellationToken ct = default)
    {
        var content = await GetOrCreateContactUsAsync(ct);
        return Result.Success(MapContactUs(content));
    }

    public async Task<Result<ContactUsResponse>> UpdateContactUsAsync(
        UpdateContactUsRequest request, string adminUserId, CancellationToken ct = default)
    {
        var content = await GetOrCreateContactUsAsync(ct);

        content.Phone = request.Phone;
        content.Email = request.Email;
        content.WhatsApp = request.WhatsApp;
        content.Address = request.Address;
        content.WorkingHours = request.WorkingHours;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("ContactUs content updated by admin {AdminId}", adminUserId);

        return Result.Success(MapContactUs(content));
    }

    private async Task<ContactUsContent> GetOrCreateContactUsAsync(CancellationToken ct)
    {
        var content = await _context.ContactUsContents.FirstOrDefaultAsync(ct);

        if (content is not null)
            return content;

        content = new ContactUsContent();

        try
        {
            await _context.ContactUsContents.AddAsync(content, ct);
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _logger.LogWarning("Concurrent ContactUsContent creation detected, re-querying");
            content = await _context.ContactUsContents.FirstAsync(ct);
        }

        return content;
    }

    private static ContactUsResponse MapContactUs(ContactUsContent content)
    {
        return new ContactUsResponse(
            content.Phone,
            content.Email,
            content.WhatsApp,
            content.Address,
            content.WorkingHours);
    }

    #endregion

    #region About App

    public async Task<Result<AboutAppResponse>> GetAboutAppAsync(CancellationToken ct = default)
    {
        var content = await GetOrCreateAboutAppAsync(ct);
        return Result.Success(new AboutAppResponse(content.Content));
    }

    public async Task<Result<AboutAppResponse>> UpdateAboutAppAsync(
        UpdateAboutAppRequest request, string adminUserId, CancellationToken ct = default)
    {
        var content = await GetOrCreateAboutAppAsync(ct);

        content.Content = request.Content;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("AboutApp content updated by admin {AdminId}", adminUserId);

        return Result.Success(new AboutAppResponse(content.Content));
    }

    private async Task<AboutAppContent> GetOrCreateAboutAppAsync(CancellationToken ct)
    {
        var content = await _context.AboutAppContents.FirstOrDefaultAsync(ct);

        if (content is not null)
            return content;

        content = new AboutAppContent();

        try
        {
            await _context.AboutAppContents.AddAsync(content, ct);
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _logger.LogWarning("Concurrent AboutAppContent creation detected, re-querying");
            content = await _context.AboutAppContents.FirstAsync(ct);
        }

        return content;
    }

    #endregion
}
