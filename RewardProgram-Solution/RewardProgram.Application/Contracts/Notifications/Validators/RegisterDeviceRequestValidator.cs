using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Notifications.Validators;

public class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.FcmToken)
            .NotEmpty().WithMessage(L["FcmToken.NotEmpty"])
            .MaximumLength(512).WithMessage(L["FcmToken.MaxLength"]);
    }
}
