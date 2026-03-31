using FluentValidation;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminEditZoneManagerRequestValidator : AbstractValidator<AdminEditZoneManagerRequest>
{
    public AdminEditZoneManagerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("الاسم مطلوب")
            .MinimumLength(3).WithMessage("الاسم يجب أن يكون 3 أحرف على الأقل")
            .MaximumLength(100).WithMessage("الاسم يجب ألا يتجاوز 100 حرف")
            .Matches(@"^[\p{L}\s]+$").WithMessage("الاسم يجب أن يحتوي على أحرف فقط");

        RuleFor(x => x.RegionId)
            .NotEmpty().WithMessage("المنطقة مطلوبة");
    }
}
