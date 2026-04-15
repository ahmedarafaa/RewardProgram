using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminReassignCitiesRequestValidator : AbstractValidator<AdminReassignCitiesRequest>
{
    public AdminReassignCitiesRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.CityIds)
            .NotEmpty().WithMessage(L["CityIds.NotEmpty"]);

        RuleFor(x => x.ToSalesManId)
            .NotEmpty().WithMessage(L["SalesManId.NotEmpty"]);
    }
}
