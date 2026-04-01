using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminEditShopOwnerRequestValidator : AbstractValidator<AdminEditShopOwnerRequest>
{
    public AdminEditShopOwnerRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.OwnerName)
            .NotEmpty().WithMessage(L["Name.NotEmpty"])
            .MinimumLength(3).WithMessage(L["Name.MinLength"])
            .MaximumLength(100).WithMessage(L["Name.MaxLength"])
            .Matches(@"^[\p{L}\s]+$").WithMessage(L["Name.LettersOnly"]);
    }
}
