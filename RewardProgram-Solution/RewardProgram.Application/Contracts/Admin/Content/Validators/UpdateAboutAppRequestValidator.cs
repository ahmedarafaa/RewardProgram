using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Content.Validators;

public class UpdateAboutAppRequestValidator : AbstractValidator<UpdateAboutAppRequest>
{
    public UpdateAboutAppRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage(L["Content.Body.NotEmpty"]);
    }
}
