namespace RewardProgram.Application.Contracts.Admin.Users;

/// <summary>
/// Sets the full staff role set of a SalesMan / ZoneManager account in one call.
/// The service diffs <see cref="Roles"/> against the user's current roles:
/// a role being removed must hand off its territory (same rules as delete),
/// a role being added may optionally take new territory (same rules as add).
/// Covers SM→ZM, ZM→SM, single→dual and dual→single without delete+recreate,
/// so the user keeps their id, name, mobile and approval history. The primary
/// UserType is sticky: adding a role never changes it, removing the primary role does.
/// </summary>
public record AdminSetStaffRolesRequest(
    // Target set — any of "SalesMan" / "ZoneManager" (case-insensitive). Must be non-empty.
    List<string> Roles,

    // Required when SalesMan is being REMOVED and the user owns cities:
    // must cover every owned city exactly once (empty / null when idle).
    List<AdminCityReassignment>? CityReassignments,

    // Required when ZoneManager is being REMOVED and the user manages a region.
    string? NewZoneManagerId,

    // Optional when ZoneManager is being ADDED — region must have no ZM.
    string? RegionId,

    // Optional when SalesMan is being ADDED — cities must have no SM.
    List<string>? CityIds
);
