using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Barcodes.Validators;

public class AdminGenerateBarcodesRequestValidator : AbstractValidator<AdminGenerateBarcodesRequest>
{
    public AdminGenerateBarcodesRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage(L["Barcode.ProductId.NotEmpty"]);

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 1000).WithMessage(L["Barcode.Quantity.Range"]);
    }
}
