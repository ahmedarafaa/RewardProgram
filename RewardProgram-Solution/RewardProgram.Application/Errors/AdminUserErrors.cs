using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class AdminUserErrors
{
    public static readonly Error MobileAlreadyExists =
        new("Admin.MobileAlreadyExists", "رقم الجوال مسجل مسبقاً", 409);

    public static readonly Error MobileBelongsToDeletedAccount =
        new("Admin.MobileBelongsToDeletedAccount",
            "رقم الجوال مرتبط بحساب محذوف — يمكن استعادته بدلاً من إنشاء حساب جديد",
            409);

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

    public static readonly Error CreateUserFailed =
        new("Admin.CreateUserFailed", "فشل إنشاء الحساب", 500);

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

    public static readonly Error CustomerCodeAlreadyOwned =
        new("Admin.CustomerCodeAlreadyOwned", "كود العميل مسجل لصاحب محل آخر", 409);

    public static readonly Error CityAlreadyHasSalesMan =
        new("Admin.CityAlreadyHasSalesMan", "المدينة لديها مندوب مبيعات بالفعل", 409);

    public static readonly Error AllCitiesMustBeReassigned =
        new("Admin.AllCitiesMustBeReassigned", "يجب إعادة تعيين جميع المدن التابعة للمندوب قبل الحذف", 400);

    public static readonly Error ReplacementZoneManagerRequired =
        new("Admin.ReplacementZoneManagerRequired", "يجب تحديد مدير منطقة بديل قبل الحذف", 400);

    public static readonly Error ReassignmentTargetNotSalesMan =
        new("Admin.ReassignmentTargetNotSalesMan", "المستخدم المستهدف ليس مندوب مبيعات", 400);

    public static readonly Error ReassignmentTargetNotZoneManager =
        new("Admin.ReassignmentTargetNotZoneManager", "المستخدم المستهدف ليس مدير منطقة", 400);

    public static readonly Error ReassignmentTargetInactive =
        new("Admin.ReassignmentTargetInactive", "لا يمكن نقل المدن أو المنطقة إلى حساب معطل أو محذوف", 400);

    public static readonly Error ReassignTerritoryBeforeDisable =
        new("Admin.ReassignTerritoryBeforeDisable", "يجب إعادة تعيين المدن أو المنطقة التابعة لهذا المستخدم قبل تعطيله", 400);

    public static readonly Error CannotToggleDeletedUser =
        new("Admin.CannotToggleDeletedUser", "لا يمكن تغيير حالة حساب محذوف، قم باستعادته أولاً", 400);

    public static readonly Error CannotRestoreArchivedRegistration =
        new("Admin.CannotRestoreArchivedRegistration", "لا يمكن استعادة طلب تسجيل مرفوض تمت أرشفته", 400);

    public static readonly Error DuplicateCityReassignment =
        new("Admin.DuplicateCityReassignment", "تم تكرار نفس المدينة أكثر من مرة في قائمة إعادة التعيين", 400);

    public static readonly Error CityNotOwnedBySalesMan =
        new("Admin.CityNotOwnedBySalesMan", "المدينة ليست تابعة لهذا المندوب", 400);

    public static readonly Error ZoneManagerAlreadyAssigned =
        new("Admin.ZoneManagerAlreadyAssigned", "مدير المنطقة مخصص لمنطقة أخرى بالفعل", 409);

    public static readonly Error CannotReassignToSelf =
        new("Admin.CannotReassignToSelf", "لا يمكن إعادة التعيين لنفس المستخدم", 400);

    public static readonly Error ConcurrencyConflict =
        new("Admin.ConcurrencyConflict", "تم تعديل البيانات من مستخدم آخر، حاول مرة أخرى", 409);

    public static readonly Error AccountNotDeleted =
        new("Admin.AccountNotDeleted", "الحساب غير محذوف، لا يمكن استعادته", 400);

    // ── Staff role set (SalesMan / ZoneManager / both) ──

    public static readonly Error UserNotStaff =
        new("Admin.UserNotStaff", "هذا الإجراء متاح فقط لحسابات مندوبي المبيعات ومديري المناطق", 400);

    public static readonly Error InvalidStaffRole =
        new("Admin.InvalidStaffRole", "الأدوار المسموح بها هي مندوب مبيعات و/أو مدير منطقة", 400);

    public static readonly Error CannotChangeRolesOfInactiveUser =
        new("Admin.CannotChangeRolesOfInactiveUser", "لا يمكن تغيير أدوار حساب معطل أو محذوف", 400);

    public static readonly Error OtherRoleTerritoryMustBeHandedOff =
        new("Admin.OtherRoleTerritoryMustBeHandedOff",
            "هذا المستخدم يملك مدناً أو منطقة بدوره الآخر — أعد تعيينها أو أزل الدور الآخر أولاً", 400);
}
