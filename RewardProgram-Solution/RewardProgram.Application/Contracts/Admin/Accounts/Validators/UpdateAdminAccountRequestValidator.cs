using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Accounts.Validators;

public class UpdateAdminAccountRequestValidator : AbstractValidator<UpdateAdminAccountRequest>
{
    public UpdateAdminAccountRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(L["Name.NotEmpty"])
            .MinimumLength(3).WithMessage(L["Name.MinLength"])
            .MaximumLength(100).WithMessage(L["Name.MaxLength"]);

        // NewPassword is optional — only validated when supplied. Must satisfy
        // ASP.NET Identity's password policy (see CreateAdminAccountRequestValidator).
        RuleFor(x => x.NewPassword)
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$")
                .WithMessage(L["AdminAccount.PasswordInvalid"])
            .When(x => !string.IsNullOrEmpty(x.NewPassword));
    }
}
