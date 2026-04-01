using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators;

public class NationalAddressDtoValidator : AbstractValidator<NationalAddressResponse>
{
    public NationalAddressDtoValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleSet("Default", () =>
        {
            RuleFor(x => x.BuildingNumber)
            .InclusiveBetween(1000, 9999)
            .WithMessage(L["Address.BuildingNumber.Range"]);

            RuleFor(x => x.Street)
            .NotEmpty().WithMessage(L["Address.Street.NotEmpty"])
            .MinimumLength(3).WithMessage(L["Address.Street.MinLength"])
            .MaximumLength(100).WithMessage(L["Address.Street.MaxLength"]);

            RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage(L["Address.PostalCode.NotEmpty"])
            .Matches(@"^\d{5}$").WithMessage(L["Address.PostalCode.InvalidFormat"]);

            RuleFor(x => x.SubNumber)
            .InclusiveBetween(1000, 9999)
            .WithMessage(L["Address.SubNumber.Range"]);

            RuleFor(x => x.District)
            .MaximumLength(100).WithMessage(L["Address.District.MaxLength"])
            .When(x => x.District != null);
        });
    }
}