using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Accounts.Validators;

public class CreateAdminAccountRequestValidator : AbstractValidator<CreateAdminAccountRequest>
{
    public CreateAdminAccountRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage(L["AdminAccount.UsernameInvalid"])
            .Length(3, 50).WithMessage(L["AdminAccount.UsernameInvalid"])
            .Matches(@"^[A-Za-z0-9._-]+$").WithMessage(L["AdminAccount.UsernameInvalid"]);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(L["Name.NotEmpty"])
            .MinimumLength(3).WithMessage(L["Name.MinLength"])
            .MaximumLength(100).WithMessage(L["Name.MaxLength"]);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(L["AdminAccount.PasswordInvalid"])
            .MinimumLength(8).WithMessage(L["AdminAccount.PasswordInvalid"]);
    }
}
