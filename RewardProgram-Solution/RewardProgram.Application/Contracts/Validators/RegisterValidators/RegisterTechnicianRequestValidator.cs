using FluentValidation;
using RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

namespace RewardProgram.Application.Contracts.Validators.RegisterValidators;

public class RegisterTechnicianRequestValidator : AbstractValidator<RegisterTechnicianRequest>
{
    public RegisterTechnicianRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("الاسم مطلوب")
            .MaximumLength(100).WithMessage("الاسم يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage("رقم الجوال مطلوب")
            .Matches(@"^05\d{8}$").WithMessage("رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام");

        RuleFor(x => x.CityId)
            .NotEmpty().WithMessage("المدينة مطلوبة");

        RuleFor(x => x.DistrictId)
            .NotEmpty().WithMessage("الحي مطلوب");

        RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage("الرمز البريدي مطلوب")
            .Length(5).WithMessage("الرمز البريدي يجب أن يتكون من 5 خانات");
    }
}
