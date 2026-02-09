using FluentValidation;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class RefreshTokenResponseValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenResponseValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("التوكن مطلوب");
    }
}