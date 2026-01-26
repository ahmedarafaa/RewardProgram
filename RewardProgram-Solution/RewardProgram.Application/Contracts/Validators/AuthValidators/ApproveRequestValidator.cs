using FluentValidation;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class ApproveRequestValidator : AbstractValidator<ApproveRequest>
{
    public ApproveRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("معرف المستخدم مطلوب");
    }
}