using FluentValidation;
using RewardProgram.Application.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class VerifyLoginRequestValidator : AbstractValidator<VerifyLoginRequest>
{
    public VerifyLoginRequestValidator()
    {
        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage("رقم الجوال مطلوب")
            .Matches(@"^05\d{8}$").WithMessage("رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام");

        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("رمز التحقق مطلوب")
            .Length(6).WithMessage("رمز التحقق يجب أن يتكون من 6 أرقام")
            .Matches(@"^\d{6}$").WithMessage("رمز التحقق يجب أن يتكون من أرقام فقط");
    }
}