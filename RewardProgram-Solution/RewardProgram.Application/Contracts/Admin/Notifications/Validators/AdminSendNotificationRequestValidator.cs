using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Notifications.Validators;

public class AdminSendNotificationRequestValidator : AbstractValidator<AdminSendNotificationRequest>
{
    public AdminSendNotificationRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage(L["Notification.Title.NotEmpty"])
            .MaximumLength(200).WithMessage(L["Notification.Title.MaxLength"]);

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage(L["Notification.Body.NotEmpty"])
            .MaximumLength(1000).WithMessage(L["Notification.Body.MaxLength"]);
    }
}
