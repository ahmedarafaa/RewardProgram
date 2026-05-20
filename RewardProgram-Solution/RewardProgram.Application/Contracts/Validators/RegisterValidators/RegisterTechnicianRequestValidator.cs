using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

namespace RewardProgram.Application.Contracts.Validators.RegisterValidators;

public class RegisterTechnicianRequestValidator : AbstractValidator<RegisterTechnicianRequest>
{
    public RegisterTechnicianRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.VerificationToken)
            .NotEmpty().WithMessage(L["VerificationToken.NotEmpty"]);

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

        RuleFor(x => x.District)
            .NotEmpty().WithMessage(L["District.NotEmpty"])
            .MaximumLength(100).WithMessage(L["District.MaxLength"]);
    }
}
