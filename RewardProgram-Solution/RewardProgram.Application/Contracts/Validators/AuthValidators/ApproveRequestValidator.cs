using FluentValidation;
using RewardProgram.Application.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class ApproveRequestValidator : AbstractValidator<ApproveRequest>
{
    public ApproveRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("معرف المستخدم مطلوب");
    }
}