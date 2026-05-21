namespace RewardProgram.Domain.Constants;

/// <summary>
/// Granular admin-dashboard permissions, named <c>Module.Action</c>. An
/// <see cref="UserRoles.Admin"/> user may only perform actions for which they
/// hold the matching permission; a <see cref="UserRoles.SystemAdmin"/> bypasses
/// these checks entirely.
/// </summary>
public static class AdminPermissions
{
    /// <summary>JWT claim type carrying a granted permission (one claim per permission).</summary>
    public const string ClaimType = "permission";

    // Users — SalesMan / ZoneManager management
    public const string UsersView = "Users.View";
    public const string UsersManage = "Users.Manage";

    // Products — catalogue (incl. import)
    public const string ProductsView = "Products.View";
    public const string ProductsManage = "Products.Manage";

    // Barcodes — generation
    public const string BarcodesView = "Barcodes.View";
    public const string BarcodesManage = "Barcodes.Manage";

    // Scans — listing + cancellation
    public const string ScansView = "Scans.View";
    public const string ScansManage = "Scans.Manage";

    // Redemptions — approvals
    public const string RedemptionsView = "Redemptions.View";
    public const string RedemptionsManage = "Redemptions.Manage";

    // Analytics / dashboard — read-only
    public const string AnalyticsView = "Analytics.View";

    // Content — AboutApp / ContactUs
    public const string ContentView = "Content.View";
    public const string ContentManage = "Content.Manage";

    // Settings — reward settings
    public const string SettingsView = "Settings.View";
    public const string SettingsManage = "Settings.Manage";

    // Notifications — broadcast + history
    public const string NotificationsView = "Notifications.View";
    public const string NotificationsManage = "Notifications.Manage";

    // ERP Customers — directory management (incl. import)
    public const string ErpCustomersView = "ErpCustomers.View";
    public const string ErpCustomersManage = "ErpCustomers.Manage";

    /// <summary>Every defined permission, in display order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        UsersView, UsersManage,
        ProductsView, ProductsManage,
        BarcodesView, BarcodesManage,
        ScansView, ScansManage,
        RedemptionsView, RedemptionsManage,
        AnalyticsView,
        ContentView, ContentManage,
        SettingsView, SettingsManage,
        NotificationsView, NotificationsManage,
        ErpCustomersView, ErpCustomersManage,
    ];

    private static readonly HashSet<string> Defined = new(All, StringComparer.Ordinal);

    /// <summary>True when <paramref name="permission"/> is a defined permission.</summary>
    public static bool IsValid(string permission) => Defined.Contains(permission);
}
