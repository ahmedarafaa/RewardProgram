using FluentValidation;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class VerifyLoginRequestValidator : AbstractValidator<LoginVerifyRequest>
{
    public VerifyLoginRequestValidator()
    {
        RuleFor(x => x.PinId)
            .NotEmpty().WithMessage("معرف الطلب مطلوب");

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("رمز التحقق مطلوب")
            .Length(6).WithMessage("رمز التحقق يجب أن يتكون من 6 أرقام")
            .Matches(@"^\d{6}$").WithMessage("رمز التحقق يجب أن يتكون من أرقام فقط");
    }
}