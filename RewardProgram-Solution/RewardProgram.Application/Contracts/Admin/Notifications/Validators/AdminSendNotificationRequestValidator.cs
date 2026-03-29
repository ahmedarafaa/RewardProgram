using FluentValidation;

namespace RewardProgram.Application.Contracts.Admin.Notifications.Validators;

public class AdminSendNotificationRequestValidator : AbstractValidator<AdminSendNotificationRequest>
{
    public AdminSendNotificationRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان الإشعار مطلوب")
            .MaximumLength(200).WithMessage("عنوان الإشعار يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("نص الإشعار مطلوب")
            .MaximumLength(1000).WithMessage("نص الإشعار يجب ألا يتجاوز 1000 حرف");
    }
}
