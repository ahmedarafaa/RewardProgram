using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Content.Validators;

public class UpdateContactUsRequestValidator : AbstractValidator<UpdateContactUsRequest>
{
    public UpdateContactUsRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage(L["Content.Phone.NotEmpty"])
            .MaximumLength(20).WithMessage(L["Content.Phone.MaxLength"]);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(L["Content.Email.NotEmpty"])
            .EmailAddress().WithMessage(L["Content.Email.InvalidFormat"])
            .MaximumLength(200).WithMessage(L["Content.Email.MaxLength"]);

        RuleFor(x => x.WhatsApp)
            .NotEmpty().WithMessage(L["Content.WhatsApp.NotEmpty"])
            .MaximumLength(20).WithMessage(L["Content.WhatsApp.MaxLength"]);

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage(L["Content.Address.NotEmpty"])
            .MaximumLength(500).WithMessage(L["Content.Address.MaxLength"]);

        RuleFor(x => x.WorkingHours)
            .NotEmpty().WithMessage(L["Content.WorkingHours.NotEmpty"])
            .MaximumLength(200).WithMessage(L["Content.WorkingHours.MaxLength"]);
    }
}
