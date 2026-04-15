using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminDeleteSalesManRequestValidator : AbstractValidator<AdminDeleteSalesManRequest>
{
    public AdminDeleteSalesManRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.CityReassignments)
            .NotNull().WithMessage(L["CityReassignments.NotNull"]);

        RuleForEach(x => x.CityReassignments).ChildRules(item =>
        {
            item.RuleFor(r => r.CityId)
                .NotEmpty().WithMessage(L["CityId.NotEmpty"]);
            item.RuleFor(r => r.NewSalesManId)
                .NotEmpty().WithMessage(L["SalesManId.NotEmpty"]);
        });
    }
}
