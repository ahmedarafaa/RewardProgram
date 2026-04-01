using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.PinId)
            .NotEmpty().WithMessage(L["PinId.NotEmpty"]);

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage(L["Otp.NotEmpty"])
            .Length(6).WithMessage(L["Otp.Length"])
            .Matches(@"^\d{6}$").WithMessage(L["Otp.DigitsOnly"]);
    }
}