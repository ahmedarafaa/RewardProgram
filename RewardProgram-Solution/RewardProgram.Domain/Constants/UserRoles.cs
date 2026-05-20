namespace RewardProgram.Domain.Constants;

/// <summary>
/// Constants for user role names to avoid magic strings.
/// </summary>
public static class UserRoles
{
    public const string ShopOwner = "ShopOwner";
    public const string Seller = "Seller";
    public const string Technician = "Technician";
    public const string SalesMan = "SalesMan";
    public const string ZoneManager = "ZoneManager";
    public const string SystemAdmin = "SystemAdmin";

    /// <summary>
    /// Admin-dashboard user restricted to explicitly granted permissions.
    /// SystemAdmin, by contrast, bypasses all permission checks.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>Both roles that may sign in to the admin dashboard.</summary>
    public const string AdminDashboard = $"{SystemAdmin},{Admin}";
}
