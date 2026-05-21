using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.ErpCustomers.Validators;

public class AdminEditErpCustomerRequestValidator : AbstractValidator<AdminEditErpCustomerRequest>
{
    public AdminEditErpCustomerRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage(L["ErpCustomer.CustomerName.NotEmpty"])
            .MaximumLength(200).WithMessage(L["ErpCustomer.CustomerName.MaxLength"]);
    }
}
