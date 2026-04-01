using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage(L["RefreshToken.NotEmpty"]);
    }
}