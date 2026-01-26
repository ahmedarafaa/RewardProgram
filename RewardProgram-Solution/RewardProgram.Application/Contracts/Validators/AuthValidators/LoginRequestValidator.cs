using FluentValidation;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage("رقم الجوال مطلوب")
            .Matches(@"^05\d{8}$").WithMessage("رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام");
    }
}