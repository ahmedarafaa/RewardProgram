using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class AuthErrors
{
    #region Registration Errors

    public static readonly Error MobileAlreadyRegistered =
        new("Auth.MobileAlreadyRegistered", "رقم الجوال مسجل مسبقاً", 409);

    public static readonly Error VatAlreadyExists =
        new("Auth.VatAlreadyExists", "الرقم الضريبي مسجل مسبقاً", 409);

    public static readonly Error CrnAlreadyExists =
        new("Auth.CrnAlreadyExists", "رقم السجل التجاري مسجل مسبقاً", 409);

    public static readonly Error RegistrationDataNotFound =
        new("Auth.RegistrationDataNotFound", "بيانات التسجيل غير موجودة، يرجى إعادة التسجيل", 400);

    public static readonly Error CreateUserFailed =
        new("Auth.CreateUserFailed", "فشل إنشاء الحساب", 500);

    #endregion

    #region User Status Errors

    public static readonly Error UserNotFound =
        new("Auth.UserNotFound", "المستخدم غير موجود", 404);

    public static readonly Error UserNotApproved =
        new("Auth.UserNotApproved", "حسابك قيد المراجعة", 403);

    public static readonly Error UserRejected =
        new("Auth.UserRejected", "تم رفض طلب التسجيل", 403);

    public static readonly Error UserDisabled =
        new("Auth.UserDisabled", "الحساب معطل، يرجى التواصل مع الإدارة", 403);

    #endregion

    #region Location Errors

    public static readonly Error CityNotFound =
        new("Auth.CityNotFound", "المدينة غير موجودة", 400);

    public static readonly Error DistrictNotFound =
        new("Auth.DistrictNotFound", "الحي غير موجود", 400);

    public static readonly Error DistrictNotInCity =
        new("Auth.DistrictNotInCity", "الحي لا يتبع المدينة المحددة", 400);

    public static readonly Error NoApprovalSalesMan =
        new("Auth.NoApprovalSalesMan", "لا يوجد مندوب مبيعات معتمد لهذه المدينة", 400);

    public static readonly Error NoSalesManForCity =
        new("Auth.NoSalesManForCity", "لا يوجد مندوب مبيعات لهذه المدينة", 400);

    #endregion

    #region ShopOwner/Seller Errors

    public static readonly Error InvalidShopCode =
        new("Auth.InvalidShopCode", "كود المحل غير صحيح", 400);

    public static readonly Error ShopOwnerNotApproved =
        new("Auth.ShopOwnerNotApproved", "صاحب المحل غير معتمد بعد", 400);

    public static readonly Error ShopOwnerNotFound =
        new("Auth.ShopOwnerNotFound", "صاحب المحل غير موجود", 404);

    #endregion

    #region OTP Errors

    public static readonly Error OtpNotFound =
        new("Auth.OtpNotFound", "رمز التحقق غير موجود", 400);

    public static readonly Error OtpExpired =
        new("Auth.OtpExpired", "انتهت صلاحية رمز التحقق", 400);

    public static readonly Error OtpInvalid =
        new("Auth.OtpInvalid", "رمز التحقق غير صحيح", 400);

    public static readonly Error OtpAlreadyUsed =
        new("Auth.OtpAlreadyUsed", "رمز التحقق مستخدم مسبقاً", 400);

    public static readonly Error OtpSendFailed =
        new("Auth.OtpSendFailed", "فشل إرسال رمز التحقق", 500);

    public static readonly Error TooManyOtpRequests =
        new("Auth.TooManyOtpRequests", "محاولات كثيرة، يرجى المحاولة لاحقاً", 429);

    public static readonly Error OtpResendTooSoon =
        new("Auth.OtpResendTooSoon", "يرجى الانتظار 30 ثانية قبل إعادة إرسال رمز التحقق", 429);

    public static readonly Error MaxVerificationAttemptsExceeded =
        new("Auth.MaxVerificationAttempts", "تم تجاوز الحد الأقصى لمحاولات التحقق", 400);

    #endregion

    #region Token Errors

    public static readonly Error InvalidRefreshToken =
        new("Auth.InvalidRefreshToken", "التوكن غير صالح", 401);

    public static readonly Error RefreshTokenExpired =
        new("Auth.RefreshTokenExpired", "انتهت صلاحية التوكن", 401);

    public static readonly Error RefreshTokenRevoked =
        new("Auth.RefreshTokenRevoked", "التوكن ملغي", 401);

    #endregion

    #region File Upload Errors

    public static readonly Error ImageUploadFailed =
        new("Auth.ImageUploadFailed", "فشل رفع الصورة", 500);

    public static readonly Error InvalidImageType =
        new("Auth.InvalidImageType", "صيغة الصورة غير مدعومة، يرجى استخدام JPG أو PNG", 400);

    public static readonly Error ImageTooLarge =
        new("Auth.ImageTooLarge", "حجم الصورة كبير جداً، الحد الأقصى 5 ميجابايت", 400);

    #endregion
}
