using FluentValidation;
using RewardProgram.Application.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Contracts.Validators.AuthValidators;

public class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(x => x.Otp)
            .NotEmpty().WithMessage("رمز التحقق مطلوب")
            .Length(6).WithMessage("رمز التحقق يجب أن يتكون من 6 أرقام")
            .Matches(@"^\d{6}$").WithMessage("رمز التحقق يجب أن يتكون من أرقام فقط");
    }
}