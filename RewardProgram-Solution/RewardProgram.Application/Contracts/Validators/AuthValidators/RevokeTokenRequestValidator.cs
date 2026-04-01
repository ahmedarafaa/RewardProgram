using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class RevokeTokenRequestValidator : AbstractValidator<RevokeTokenRequest>
{
    public RevokeTokenRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage(L["RefreshToken.NotEmpty"]);
    }
}
