using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Admin.Users;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminAddSalesManRequestValidator : AbstractValidator<AdminAddSalesManRequest>
{
    public AdminAddSalesManRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(L["Name.NotEmpty"])
            .MinimumLength(3).WithMessage(L["Name.MinLength"])
            .MaximumLength(100).WithMessage(L["Name.MaxLength"])
            .Matches(@"^[\p{L}\s]+$").WithMessage(L["Name.LettersOnly"]);

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage(L["MobileNumber.NotEmpty"])
            .Matches(@"^(05\d{8}|\+\d{10,15})$").WithMessage(L["MobileNumber.InvalidFormat"]);

        RuleFor(x => x.CityIds)
            .NotEmpty().WithMessage(L["CityIds.NotEmpty"]);
    }
}
