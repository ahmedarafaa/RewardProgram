using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class AdminUserErrors
{
    public static readonly Error MobileAlreadyExists =
        new("Admin.MobileAlreadyExists", "رقم الجوال مسجل مسبقاً", 409);

    public static readonly Error CityNotFound =
        new("Admin.CityNotFound", "المدينة غير موجودة", 400);

    public static readonly Error SomeCitiesNotFound =
        new("Admin.SomeCitiesNotFound", "بعض المدن المحددة غير موجودة أو غير مفعلة", 400);

    public static readonly Error RegionNotFound =
        new("Admin.RegionNotFound", "المنطقة غير موجودة", 400);

    public static readonly Error RegionAlreadyHasZoneManager =
        new("Admin.RegionAlreadyHasZoneManager", "المنطقة لديها مدير منطقة بالفعل", 409);

    public static readonly Error CustomerCodeNotFound =
        new("Admin.CustomerCodeNotFound", "كود العميل غير موجود في النظام", 400);

    public static readonly Error ShopDataRequired =
        new("Admin.ShopDataRequired", "بيانات المحل مطلوبة — لا توجد بيانات سابقة لهذا الكود", 400);

    public static readonly Error VatAlreadyExists =
        new("Admin.VatAlreadyExists", "الرقم الضريبي مسجل مسبقاً", 409);

    public static readonly Error CrnAlreadyExists =
        new("Admin.CrnAlreadyExists", "رقم السجل التجاري مسجل مسبقاً", 409);

    public static readonly Error CreateUserFailed =
        new("Admin.CreateUserFailed", "فشل إنشاء الحساب", 500);

    public static readonly Error ImageUploadFailed =
        new("Admin.ImageUploadFailed", "فشل رفع الصورة", 500);

    public static readonly Error NoApprovalSalesMan =
        new("Admin.NoApprovalSalesMan", "لا يوجد مندوب مبيعات معتمد لهذه المدينة", 400);

    public static readonly Error UserNotFound =
        new("Admin.UserNotFound", "المستخدم غير موجود", 404);

    public static readonly Error UserIsSystemAdmin =
        new("Admin.UserIsSystemAdmin", "لا يمكن تعديل حساب مدير النظام", 403);

    public static readonly Error UserTypeMismatch =
        new("Admin.UserTypeMismatch", "نوع المستخدم غير متطابق", 400);

    public static readonly Error MobileAlreadyInUse =
        new("Admin.MobileAlreadyInUse", "رقم الجوال مستخدم من قبل مستخدم آخر", 409);

    public static readonly Error UpdateUserFailed =
        new("Admin.UpdateUserFailed", "فشل تحديث بيانات المستخدم", 500);
}
