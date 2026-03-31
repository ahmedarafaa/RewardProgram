using FluentValidation;

namespace RewardProgram.Application.Contracts.Admin.Content.Validators;

public class UpdateContactUsRequestValidator : AbstractValidator<UpdateContactUsRequest>
{
    public UpdateContactUsRequestValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("رقم الهاتف مطلوب")
            .MaximumLength(20).WithMessage("رقم الهاتف يجب ألا يتجاوز 20 حرف");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
            .EmailAddress().WithMessage("البريد الإلكتروني غير صالح")
            .MaximumLength(200).WithMessage("البريد الإلكتروني يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.WhatsApp)
            .NotEmpty().WithMessage("رقم الواتساب مطلوب")
            .MaximumLength(20).WithMessage("رقم الواتساب يجب ألا يتجاوز 20 حرف");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("العنوان مطلوب")
            .MaximumLength(500).WithMessage("العنوان يجب ألا يتجاوز 500 حرف");

        RuleFor(x => x.WorkingHours)
            .NotEmpty().WithMessage("ساعات العمل مطلوبة")
            .MaximumLength(200).WithMessage("ساعات العمل يجب ألا تتجاوز 200 حرف");
    }
}
