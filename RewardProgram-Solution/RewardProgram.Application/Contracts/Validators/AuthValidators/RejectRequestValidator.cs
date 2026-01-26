using FluentValidation;
using RewardProgram.Application.Contracts.Auth;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class RejectRequestValidator : AbstractValidator<RejectRequest>
{
    public RejectRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("معرف المستخدم مطلوب");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("سبب الرفض مطلوب")
            .MaximumLength(500).WithMessage("سبب الرفض يجب ألا يتجاوز 500 حرف");
    }
}
