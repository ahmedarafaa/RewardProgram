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


            RuleFor(x => x.City)
            .NotEmpty().WithMessage("المدينة مطلوبة")
            .MaximumLength(50).WithMessage("المدينة يجب ألا تتجاوز 50 حرف");
        });


        RuleSet("Strict", () =>
        {
            RuleFor(x => x.Street)
            .NotEmpty().WithMessage("الشارع مطلوب")
            .MaximumLength(100).WithMessage("الشارع يجب ألا يتجاوز 100 حرف");


            RuleFor(x => x.Neighborhood)
            .NotEmpty().WithMessage("الحي مطلوب")
            .MaximumLength(100).WithMessage("الحي يجب ألا تتجاوز 100 حرف");


            RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage("الرمز البريدي مطلوب")
            .Length(5).WithMessage("الرمز البريدي يجب أن يتكون من 5 أرقام");


            RuleFor(x => x.SubNumber)
            .GreaterThan(0)
            .WithMessage("الرقم الفرعي مطلوب");
        });
    }
}