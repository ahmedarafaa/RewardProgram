using FluentValidation;

namespace RewardProgram.Application.Contracts.Admin.RewardSettings.Validators;

public class UpdateRewardSettingsRequestValidator : AbstractValidator<UpdateRewardSettingsRequest>
{
    public UpdateRewardSettingsRequestValidator()
    {
        RuleFor(x => x.PointsToSarRate)
            .GreaterThan(0).WithMessage("معدل التحويل يجب أن يكون أكبر من صفر");

        RuleFor(x => x.InviterRewardPoints)
            .GreaterThanOrEqualTo(0).WithMessage("نقاط مكافأة الداعي يجب أن تكون صفر أو أكثر");

        RuleFor(x => x.InviteeRewardPoints)
            .GreaterThanOrEqualTo(0).WithMessage("نقاط مكافأة المدعو يجب أن تكون صفر أو أكثر");
    }
}
