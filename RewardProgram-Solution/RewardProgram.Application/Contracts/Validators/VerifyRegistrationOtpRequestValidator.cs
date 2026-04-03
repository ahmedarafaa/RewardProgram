using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators;

public class VerifyRegistrationOtpRequestValidator : AbstractValidator<VerifyRegistrationOtpRequest>
{
    public VerifyRegistrationOtpRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.PinId)
            .NotEmpty().WithMessage(L["PinId.NotEmpty"]);

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage(L["Otp.NotEmpty"]);

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage(L["MobileNumber.NotEmpty"])
            .Matches(@"^(05\d{8}|\+\d{10,15})$").WithMessage(L["MobileNumber.InvalidFormat"]);
    }
}
