using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class NotificationErrors
{
    public static readonly Error NotificationNotFound =
        new("Notification.NotFound", "الإشعار غير موجود", 404);

    public static readonly Error NotificationNotOwned =
        new("Notification.NotOwned", "لا يمكنك الوصول لهذا الإشعار", 403);

    public static readonly Error UserNotFound =
        new("Notification.UserNotFound", "المستخدم غير موجود", 404);
}
