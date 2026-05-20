namespace RewardProgram.Application.Contracts.Admin.Accounts;

/// <summary>
/// One module of grantable admin permissions, for the permission-assignment UI.
/// <see cref="ModuleName"/> and each item's label are localized to the request culture.
/// </summary>
public record PermissionCatalogModule(
    string Module,
    string ModuleName,
    IReadOnlyList<PermissionCatalogItem> Permissions
);

/// <summary>One grantable permission: its key and a localized action label.</summary>
public record PermissionCatalogItem(
    string Key,
    string Label
);
