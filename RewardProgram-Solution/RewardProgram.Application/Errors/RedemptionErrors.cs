using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class RedemptionErrors
{
    public static readonly Error InsufficientBalance =
        new("Redemption.InsufficientBalance", "الرصيد المتاح غير كافٍ للاسترداد", 400);

    public static readonly Error BelowMinimum =
        new("Redemption.BelowMinimum", "المبلغ المطلوب أقل من الحد الأدنى للاسترداد", 400);

    public static readonly Error NotIntegerSar =
        new("Redemption.NotIntegerSar", "مبلغ الريال يجب أن يكون عدد صحيح", 400);

    public static readonly Error AlreadyHasPendingRequest =
        new("Redemption.AlreadyHasPendingRequest", "لديك طلب استرداد قائم بالفعل", 409);

    public static readonly Error RequestNotFound =
        new("Redemption.RequestNotFound", "طلب الاسترداد غير موجود", 404);

    public static readonly Error NotPendingApproval =
        new("Redemption.NotPendingApproval", "الطلب ليس في حالة انتظار الموافقة", 400);

    public static readonly Error NotAuthorizedToApprove =
        new("Redemption.NotAuthorizedToApprove", "لا يحق لك الموافقة على هذا الطلب", 403);

    public static readonly Error InvalidOtp =
        new("Redemption.InvalidOtp", "رمز التحقق غير صحيح", 400);

    public static readonly Error OtpExpired =
        new("Redemption.OtpExpired", "رمز التحقق منتهي الصلاحية", 400);

    public static readonly Error NotInCashHandoverState =
        new("Redemption.NotInCashHandoverState", "الطلب ليس في حالة تسليم نقدي", 400);

    public static readonly Error UserNotApproved =
        new("Redemption.UserNotApproved", "الحساب غير مفعل أو غير معتمد", 403);

    public static readonly Error OtpMaxAttemptsExceeded =
        new("Redemption.OtpMaxAttemptsExceeded", "تم تجاوز الحد الأقصى لمحاولات رمز التحقق", 429);

    public static readonly Error CannotCancelScanWithInsufficientBalance =
        new("Redemption.CannotCancelScanWithInsufficientBalance", "لا يمكن إلغاء المسح — الرصيد المتاح غير كافٍ (قد تكون النقاط مسترداة أو محتجزة)", 400);
}
