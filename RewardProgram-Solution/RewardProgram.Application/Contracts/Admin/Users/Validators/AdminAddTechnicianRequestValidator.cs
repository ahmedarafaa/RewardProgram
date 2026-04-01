using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Admin.Users;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminAddTechnicianRequestValidator : AbstractValidator<AdminAddTechnicianRequest>
{
    public AdminAddTechnicianRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(L["Name.NotEmpty"])
            .MinimumLength(3).WithMessage(L["Name.MinLength"])
            .MaximumLength(100).WithMessage(L["Name.MaxLength"])
            .Matches(@"^[\p{L}\s]+$").WithMessage(L["Name.LettersOnly"]);

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage(L["MobileNumber.NotEmpty"])
            .Matches(@"^(05\d{8}|\+\d{10,15})$").WithMessage(L["MobileNumber.InvalidFormat"]);

        RuleFor(x => x.CityId)
            .NotEmpty().WithMessage(L["CityId.NotEmpty"]);

        RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage(L["PostalCode.NotEmpty"])
            .Matches(@"^\d{5}$").WithMessage(L["PostalCode.InvalidFormat"]);

        RuleFor(x => x.District)
            .NotEmpty().WithMessage(L["District.NotEmpty"])
            .MaximumLength(100).WithMessage(L["District.MaxLength"]);
    }
}
