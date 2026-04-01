using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminEditTechnicianRequestValidator : AbstractValidator<AdminEditTechnicianRequest>
{
    public AdminEditTechnicianRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(L["Name.NotEmpty"])
            .MinimumLength(3).WithMessage(L["Name.MinLength"])
            .MaximumLength(100).WithMessage(L["Name.MaxLength"])
            .Matches(@"^[\p{L}\s]+$").WithMessage(L["Name.LettersOnly"]);
    }
}
