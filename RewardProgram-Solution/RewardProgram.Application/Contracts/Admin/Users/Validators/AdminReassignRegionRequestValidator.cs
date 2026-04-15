using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminReassignRegionRequestValidator : AbstractValidator<AdminReassignRegionRequest>
{
    public AdminReassignRegionRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.RegionId)
            .NotEmpty().WithMessage(L["RegionId.NotEmpty"]);

        RuleFor(x => x.ToZoneManagerId)
            .NotEmpty().WithMessage(L["ZoneManagerId.NotEmpty"]);
    }
}
