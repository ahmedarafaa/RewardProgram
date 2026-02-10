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


    }
}