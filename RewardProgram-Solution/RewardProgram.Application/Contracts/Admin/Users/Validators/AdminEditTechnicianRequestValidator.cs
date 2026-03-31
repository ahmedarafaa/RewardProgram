using FluentValidation;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminEditTechnicianRequestValidator : AbstractValidator<AdminEditTechnicianRequest>
{
    public AdminEditTechnicianRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("الاسم مطلوب")
            .MinimumLength(3).WithMessage("الاسم يجب أن يكون 3 أحرف على الأقل")
            .MaximumLength(100).WithMessage("الاسم يجب ألا يتجاوز 100 حرف")
            .Matches(@"^[\p{L}\s]+$").WithMessage("الاسم يجب أن يحتوي على أحرف فقط");

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
