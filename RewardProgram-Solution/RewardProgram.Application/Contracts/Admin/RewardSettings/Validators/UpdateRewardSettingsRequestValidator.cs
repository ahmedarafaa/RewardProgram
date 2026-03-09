using FluentValidation;

namespace RewardProgram.Application.Contracts.Admin.RewardSettings.Validators;

public class UpdateRewardSettingsRequestValidator : AbstractValidator<UpdateRewardSettingsRequest>
{
    public UpdateRewardSettingsRequestValidator()
    {
        RuleFor(x => x.PointsToSarRate)
            .GreaterThan(0).WithMessage("معدل التحويل يجب أن يكون أكبر من صفر");
    }
}
