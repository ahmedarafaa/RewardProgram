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
            .InclusiveBetween(1000, 9999)
            .WithMessage("رقم المبنى يجب أن يتكون من 4 أرقام");

            RuleFor(x => x.Street)
            .NotEmpty().WithMessage("الشارع مطلوب")
            .MaximumLength(100).WithMessage("الشارع يجب ألا يتجاوز 100 حرف");

            RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage("الرمز البريدي مطلوب")
            .Matches(@"^\d{5}$").WithMessage("الرمز البريدي يجب أن يتكون من 5 أرقام");

            RuleFor(x => x.SubNumber)
            .InclusiveBetween(1000, 9999)
            .WithMessage("الرقم الفرعي يجب أن يتكون من 4 أرقام");

            RuleFor(x => x.District)
            .MaximumLength(100).WithMessage("الحي يجب ألا يتجاوز 100 حرف")
            .When(x => x.District != null);
        });
    }
}