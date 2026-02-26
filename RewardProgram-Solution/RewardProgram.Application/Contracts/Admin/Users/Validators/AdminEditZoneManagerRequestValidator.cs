using FluentValidation;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminEditZoneManagerRequestValidator : AbstractValidator<AdminEditZoneManagerRequest>
{
    public AdminEditZoneManagerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("الاسم مطلوب")
            .MinimumLength(2).WithMessage("الاسم يجب أن يكون حرفين على الأقل")
            .MaximumLength(100).WithMessage("الاسم يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage("رقم الجوال مطلوب")
            .Matches(@"^05\d{8}$").WithMessage("رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام");

        RuleFor(x => x.RegionId)
            .NotEmpty().WithMessage("المنطقة مطلوبة");
    }
}
