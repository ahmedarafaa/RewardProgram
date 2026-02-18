using FluentValidation;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators;

public class NationalAddressDtoValidator : AbstractValidator<NationalAddressResponse>
{
    public NationalAddressDtoValidator()
    {
        RuleSet("Default", () =>
        {
            RuleFor(x => x.BuildingNumber)
            .GreaterThan(0)
            .WithMessage("رقم المبنى مطلوب");

            RuleFor(x => x.Street)
            .NotEmpty().WithMessage("الشارع مطلوب")
            .MaximumLength(100).WithMessage("الشارع يجب ألا يتجاوز 100 حرف");

            RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage("الرمز البريدي مطلوب")
            .Matches(@"^\d{5}$").WithMessage("الرمز البريدي يجب أن يتكون من 5 أرقام");

            RuleFor(x => x.SubNumber)
            .GreaterThan(0)
            .WithMessage("الرقم الفرعي مطلوب");
        });
    }
}