using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Domain.Entities;

public class Notification : TrackableEntity
{
    public string UserId { get; set; } = null!;
    public ApplicationUser? User { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public string? ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    // Localization: services write a resource key + JSON-encoded args at event
    // time. Read paths render the localized string at request time using
    // IStringLocalizer<ErrorMessages>. Older rows have null here; reads fall back
    // to the pre-rendered Title/Body (Arabic).
    public string? TitleKey { get; set; }
    public string? BodyKey { get; set; }
    public string? BodyArgs { get; set; }   // JSON array of strings
}
