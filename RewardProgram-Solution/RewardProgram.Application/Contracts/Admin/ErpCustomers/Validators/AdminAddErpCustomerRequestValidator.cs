using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.ErpCustomers.Validators;

public class AdminAddErpCustomerRequestValidator : AbstractValidator<AdminAddErpCustomerRequest>
{
    public AdminAddErpCustomerRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.CustomerCode)
            .NotEmpty().WithMessage(L["ErpCustomer.CustomerCode.NotEmpty"])
            .MaximumLength(50).WithMessage(L["ErpCustomer.CustomerCode.MaxLength"]);

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage(L["ErpCustomer.CustomerName.NotEmpty"])
            .MaximumLength(200).WithMessage(L["ErpCustomer.CustomerName.MaxLength"]);
    }
}
