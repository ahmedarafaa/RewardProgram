using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class AdminAccountErrors
{
    public static readonly Error NotFound =
        new("AdminAccount.NotFound", "حساب المسؤول غير موجود", 404);

    public static readonly Error UsernameAlreadyExists =
        new("AdminAccount.UsernameAlreadyExists", "اسم المستخدم مسجل مسبقاً", 409);

    public static readonly Error CannotModifySystemAdmin =
        new("AdminAccount.CannotModifySystemAdmin", "لا يمكن تعديل أو حذف حساب مدير النظام", 400);

    public static readonly Error CreateFailed =
        new("AdminAccount.CreateFailed", "فشل إنشاء حساب المسؤول", 400);

    public static readonly Error UpdateFailed =
        new("AdminAccount.UpdateFailed", "فشل تحديث حساب المسؤول", 400);

    public static readonly Error InvalidPermission =
        new("AdminAccount.InvalidPermission", "صلاحية غير معروفة", 400);

    public static readonly Error HasHistory =
        new("AdminAccount.HasHistory", "لا يمكن حذف الحساب لارتباطه بسجل نشاط — يمكنك تعطيله بدلاً من حذفه", 400);
}
