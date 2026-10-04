using FluentValidation;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts.Validators;
using RewardProgram.Domain.Constants;

namespace RewardProgram.Application.Contracts.Admin.Users.Validators;

public class AdminSetStaffRolesRequestValidator : AbstractValidator<AdminSetStaffRolesRequest>
{
    private static readonly string[] AllowedRoles = [UserRoles.SalesMan, UserRoles.ZoneManager];

    public AdminSetStaffRolesRequestValidator(IStringLocalizer<ValidationMessages> L)
    {
        RuleFor(x => x.Roles)
            .NotNull().WithMessage(L["StaffRoles.NotEmpty"])
            .Must(r => r is { Count: > 0 }).WithMessage(L["StaffRoles.NotEmpty"])
            .Must(r => r is null || r.All(IsAllowed)).WithMessage(L["StaffRoles.Invalid"])
            .Must(r => r is null || r.Select(Normalize).Distinct().Count() == r.Count)
                .WithMessage(L["StaffRoles.Duplicate"]);

        RuleForEach(x => x.CityReassignments).ChildRules(item =>
        {
            item.RuleFor(r => r.CityId)
                .NotEmpty().WithMessage(L["CityId.NotEmpty"]);
            item.RuleFor(r => r.NewSalesManId)
                .NotEmpty().WithMessage(L["SalesManId.NotEmpty"]);
        });

        RuleForEach(x => x.CityIds)
            .NotEmpty().WithMessage(L["CityId.NotEmpty"]);
    }

    private static bool IsAllowed(string? role) =>
        role is not null && AllowedRoles.Contains(Normalize(role), StringComparer.Ordinal);

    private static string Normalize(string? role) =>
        AllowedRoles.FirstOrDefault(a => a.Equals(role?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? (role ?? string.Empty);
}
