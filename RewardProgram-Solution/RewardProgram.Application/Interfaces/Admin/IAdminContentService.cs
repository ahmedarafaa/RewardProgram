using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Admin.Content;

namespace RewardProgram.Application.Interfaces.Admin;

public interface IAdminContentService
{
    Task<Result<ContactUsResponse>> GetContactUsAsync(CancellationToken ct = default);
    Task<Result<ContactUsResponse>> UpdateContactUsAsync(UpdateContactUsRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AboutAppResponse>> GetAboutAppAsync(CancellationToken ct = default);
    Task<Result<AboutAppResponse>> UpdateAboutAppAsync(UpdateAboutAppRequest request, string adminUserId, CancellationToken ct = default);
}
