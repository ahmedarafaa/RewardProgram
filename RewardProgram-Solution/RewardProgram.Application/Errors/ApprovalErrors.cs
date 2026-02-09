using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class ApprovalErrors
{
    public static readonly Error UserNotPendingApproval =
        new("Approval.UserNotPendingApproval", "المستخدم ليس بانتظار الموافقة", 400);

    public static readonly Error NotAuthorizedToApprove =
        new("Approval.NotAuthorizedToApprove", "غير مصرح لك بالموافقة على هذا الطلب", 403);

    public static readonly Error SalesManHasNoZoneManager =
        new("Approval.SalesManHasNoZoneManager", "لا يوجد مدير منطقة مرتبط بمندوب المبيعات", 400);

    public static readonly Error ShopCodeGenerationFailed =
        new("Approval.ShopCodeGenerationFailed", "فشل إنشاء كود المحل", 500);
}
