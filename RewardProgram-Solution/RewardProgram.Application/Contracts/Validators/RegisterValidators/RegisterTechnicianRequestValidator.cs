using FluentValidation;
using RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

namespace RewardProgram.Application.Contracts.Validators.RegisterValidators;

public class RegisterTechnicianRequestValidator : AbstractValidator<RegisterTechnicianRequest>
{
    public RegisterTechnicianRequestValidator()
    {
        RuleFor(x => x.PinId)
            .NotEmpty().WithMessage("معرف رمز التحقق مطلوب");

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("رمز التحقق مطلوب");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("الاسم مطلوب")
            .MinimumLength(2).WithMessage("الاسم يجب أن يكون حرفين على الأقل")
            .MaximumLength(100).WithMessage("الاسم يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage("رقم الجوال مطلوب")
            .Matches(@"^(05\d{8}|\+\d{10,15})$").WithMessage("رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام أو يبدأ بـ + متبوعاً برمز الدولة");

        RuleFor(x => x.CityId)
            .NotEmpty().WithMessage("المدينة مطلوبة");

        RuleFor(x => x.PostalCode)
            .NotEmpty().WithMessage("الرمز البريدي مطلوب")
            .Matches(@"^\d{5}$").WithMessage("الرمز البريدي يجب أن يتكون من 5 أرقام");

        RuleFor(x => x.District)
            .NotEmpty().WithMessage("الحي مطلوب")
            .MaximumLength(100).WithMessage("الحي يجب ألا يتجاوز 100 حرف");
    }
}
