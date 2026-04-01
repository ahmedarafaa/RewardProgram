using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.RewardSettings.Validators;

public class UpdateRewardSettingsRequestValidator : AbstractValidator<UpdateRewardSettingsRequest>
{
    public UpdateRewardSettingsRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.PointsToSarRate)
            .GreaterThan(0).WithMessage(L["RewardSettings.PointsToSarRate.GreaterThanZero"]);

        RuleFor(x => x.InviterRewardPoints)
            .GreaterThanOrEqualTo(0).WithMessage(L["RewardSettings.InviterRewardPoints.NotNegative"]);

        RuleFor(x => x.InviteeRewardPoints)
            .GreaterThanOrEqualTo(0).WithMessage(L["RewardSettings.InviteeRewardPoints.NotNegative"]);

        RuleFor(x => x.MinimumRedemptionPoints)
            .GreaterThan(0).WithMessage(L["RewardSettings.MinimumRedemptionPoints.GreaterThanZero"]);
    }
}
