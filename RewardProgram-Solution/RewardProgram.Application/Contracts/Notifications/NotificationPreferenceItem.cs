using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Notifications;

public record NotificationPreferenceItem(NotificationType Type, bool IsPushMuted);

public record UpdateNotificationPreferencesRequest(List<NotificationPreferenceItem> Preferences);
