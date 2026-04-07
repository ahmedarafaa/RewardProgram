using RewardProgram.Domain.Enums;

namespace RewardProgram.Domain.Entities;

public class UserNotificationPreference
{
    public string UserId { get; set; } = null!;
    public NotificationType NotificationType { get; set; }
    public bool IsPushMuted { get; set; }
}
