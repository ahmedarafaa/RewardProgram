using FluentValidation;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class SendOtpRequestValidator : AbstractValidator<SendOtpRequest>
{
    public SendOtpRequestValidator()
    {
        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage("رقم الجوال مطلوب")
            .Matches(@"^(05\d{8}|\+\d{10,15})$").WithMessage("رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام أو يبدأ بـ + متبوعاً برمز الدولة");
    }
}
