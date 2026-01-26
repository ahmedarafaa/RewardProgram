using FluentValidation;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class RefreshTokenResponseValidator : AbstractValidator<RefreshTokenResponse>
{
    public RefreshTokenResponseValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("التوكن مطلوب");
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("التوكن مطلوب");
    }
}