namespace RewardProgram.API.Errors;

public static class AuthErrors
{
    public static readonly Error MobileAlreadyRegistered =
        new("Auth.MobileAlreadyRegistered", "رقم الجوال مسجل مسبقاً", StatusCodes.Status409Conflict);

    public static readonly Error UserNotFound =
        new("Auth.UserNotFound", "المستخدم غير موجود", StatusCodes.Status404NotFound);

    public static readonly Error UserNotApproved =
        new("Auth.UserNotApproved", "حسابك قيد المراجعة", StatusCodes.Status403Forbidden);

    public static readonly Error UserRejected =
        new("Auth.UserRejected", "تم رفض طلب التسجيل", StatusCodes.Status403Forbidden);

    public static readonly Error UserDisabled =
        new("Auth.UserDisabled", "الحساب معطل، يرجى التواصل مع الإدارة", StatusCodes.Status403Forbidden);

    public static readonly Error InvalidShopCode =
        new("Auth.InvalidShopCode", "كود المحل غير صحيح", StatusCodes.Status400BadRequest);

    public static readonly Error ShopOwnerNotApproved =
        new("Auth.ShopOwnerNotApproved", "صاحب المحل غير معتمد بعد", StatusCodes.Status400BadRequest);

    public static readonly Error NoSalesManForCity =
        new("Auth.NoSalesManForCity", "لا يوجد مندوب مبيعات لهذه المدينة", StatusCodes.Status400BadRequest);

    public static readonly Error InvalidRefreshToken =
        new("Auth.InvalidRefreshToken", "التوكن غير صالح", StatusCodes.Status401Unauthorized);

    public static readonly Error RegistrationDataNotFound =
        new("Auth.RegistrationDataNotFound", "بيانات التسجيل غير موجودة، يرجى إعادة التسجيل", StatusCodes.Status400BadRequest);
}