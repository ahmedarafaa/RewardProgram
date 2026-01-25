using FluentValidation;
using RewardProgram.Application.DTOs.Auth.UsersDTO;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Contracts.Validators.RegisterValidators;

public class RegisterSellerRequestValidator : AbstractValidator<RegisterSellerRequest>
{
    public RegisterSellerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("الاسم مطلوب")
            .MaximumLength(100).WithMessage("الاسم يجب ألا يتجاوز 100 حرف");

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage("رقم الجوال مطلوب")
            .Matches(@"^05\d{8}$").WithMessage("رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام");

        RuleFor(x => x.ShopCode)
            .NotEmpty().WithMessage("كود المحل مطلوب")
            .Length(6).WithMessage("كود المحل يجب أن يتكون من 6 أرقام")
            .Matches(@"^\d{6}$").WithMessage("كود المحل يجب أن يتكون من أرقام فقط");

        RuleFor(x => x.NationalAddress)
        .NotNull().WithMessage("العنوان الوطني مطلوب")
        .SetValidator(new NationalAddressDtoValidator(), "Default");
    }
}