using FluentValidation;
using RewardProgram.Application.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class RefreshTokenResponseValidator : AbstractValidator<RefreshTokenResponse>
{
    public RefreshTokenResponseValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("التوكن مطلوب");
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("التوكن مطلوب");
    }
}