using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminDeleteZoneManagerRequestValidator : AbstractValidator<AdminDeleteZoneManagerRequest>
{
    public AdminDeleteZoneManagerRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
    }
}
