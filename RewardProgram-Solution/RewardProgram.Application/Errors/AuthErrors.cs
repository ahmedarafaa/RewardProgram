
using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class AuthErrors
{
    #region Registration Errors

    public static readonly Error MobileAlreadyRegistered =
        new("Auth.MobileAlreadyRegistered", "رقم الجوال مسجل مسبقاً", StatusCodes.Status409Conflict);

    public static readonly Error VatAlreadyExists =
        new("Auth.VatAlreadyExists", "الرقم الضريبي مسجل مسبقاً", StatusCodes.Status409Conflict);

    public static readonly Error CrnAlreadyExists =
        new("Auth.CrnAlreadyExists", "رقم السجل التجاري مسجل مسبقاً", StatusCodes.Status409Conflict);

    public static readonly Error RegistrationDataNotFound =
        new("Auth.RegistrationDataNotFound", "بيانات التسجيل غير موجودة، يرجى إعادة التسجيل", StatusCodes.Status400BadRequest);

    public static readonly Error CreateUserFailed =
        new("Auth.CreateUserFailed", "فشل إنشاء الحساب", StatusCodes.Status500InternalServerError);

    #endregion

    #region User Status Errors

    public static readonly Error UserNotFound =
        new("Auth.UserNotFound", "المستخدم غير موجود", StatusCodes.Status404NotFound);

    public static readonly Error UserNotApproved =
        new("Auth.UserNotApproved", "حسابك قيد المراجعة", StatusCodes.Status403Forbidden);

    public static readonly Error UserRejected =
        new("Auth.UserRejected", "تم رفض طلب التسجيل", StatusCodes.Status403Forbidden);

    public static readonly Error UserDisabled =
        new("Auth.UserDisabled", "الحساب معطل، يرجى التواصل مع الإدارة", StatusCodes.Status403Forbidden);

    #endregion

    #region Location Errors

    public static readonly Error CityNotFound =
        new("Auth.CityNotFound", "المدينة غير موجودة", StatusCodes.Status400BadRequest);

    public static readonly Error DistrictNotFound =
        new("Auth.DistrictNotFound", "الحي غير موجود", StatusCodes.Status400BadRequest);

    public static readonly Error DistrictNotInCity =
        new("Auth.DistrictNotInCity", "الحي لا يتبع المدينة المحددة", StatusCodes.Status400BadRequest);

    public static readonly Error NoApprovalSalesMan =
        new("Auth.NoApprovalSalesMan", "لا يوجد مندوب مبيعات معتمد لهذا الحي", StatusCodes.Status400BadRequest);

    public static readonly Error NoSalesManForCity =
        new("Auth.NoSalesManForCity", "لا يوجد مندوب مبيعات لهذه المدينة", StatusCodes.Status400BadRequest);

    #endregion

    #region ShopOwner/Seller Errors

    public static readonly Error InvalidShopCode =
        new("Auth.InvalidShopCode", "كود المحل غير صحيح", StatusCodes.Status400BadRequest);

    public static readonly Error ShopOwnerNotApproved =
        new("Auth.ShopOwnerNotApproved", "صاحب المحل غير معتمد بعد", StatusCodes.Status400BadRequest);

    public static readonly Error ShopOwnerNotFound =
        new("Auth.ShopOwnerNotFound", "صاحب المحل غير موجود", StatusCodes.Status404NotFound);

    #endregion

    #region OTP Errors

    public static readonly Error OtpNotFound =
        new("Auth.OtpNotFound", "رمز التحقق غير موجود", StatusCodes.Status400BadRequest);

    public static readonly Error OtpExpired =
        new("Auth.OtpExpired", "انتهت صلاحية رمز التحقق", StatusCodes.Status400BadRequest);

    public static readonly Error OtpInvalid =
        new("Auth.OtpInvalid", "رمز التحقق غير صحيح", StatusCodes.Status400BadRequest);

    public static readonly Error OtpAlreadyUsed =
        new("Auth.OtpAlreadyUsed", "رمز التحقق مستخدم مسبقاً", StatusCodes.Status400BadRequest);

    public static readonly Error OtpSendFailed =
        new("Auth.OtpSendFailed", "فشل إرسال رمز التحقق", StatusCodes.Status500InternalServerError);

    public static readonly Error TooManyOtpRequests =
        new("Auth.TooManyOtpRequests", "محاولات كثيرة، يرجى المحاولة لاحقاً", StatusCodes.Status429TooManyRequests);

    #endregion

    #region Token Errors

    public static readonly Error InvalidRefreshToken =
        new("Auth.InvalidRefreshToken", "التوكن غير صالح", StatusCodes.Status401Unauthorized);

    public static readonly Error RefreshTokenExpired =
        new("Auth.RefreshTokenExpired", "انتهت صلاحية التوكن", StatusCodes.Status401Unauthorized);

    public static readonly Error RefreshTokenRevoked =
        new("Auth.RefreshTokenRevoked", "التوكن ملغي", StatusCodes.Status401Unauthorized);

    #endregion

    #region File Upload Errors

    public static readonly Error ImageUploadFailed =
        new("Auth.ImageUploadFailed", "فشل رفع الصورة", StatusCodes.Status500InternalServerError);

    public static readonly Error InvalidImageType =
        new("Auth.InvalidImageType", "صيغة الصورة غير مدعومة، يرجى استخدام JPG أو PNG", StatusCodes.Status400BadRequest);

    public static readonly Error ImageTooLarge =
        new("Auth.ImageTooLarge", "حجم الصورة كبير جداً، الحد الأقصى 5 ميجابايت", StatusCodes.Status400BadRequest);

    #endregion

}