using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class AdminErpCustomerErrors
{
    public static readonly Error ErpCustomerNotFound =
        new("ErpCustomer.NotFound", "العميل غير موجود", 404);

    public static readonly Error CustomerCodeAlreadyExists =
        new("ErpCustomer.CodeAlreadyExists", "كود العميل مسجل مسبقاً", 409);

    public static readonly Error ShortAddressAlreadyExists =
        new("ErpCustomer.ShortAddressAlreadyExists", "العنوان المختصر مستخدم مسبقاً", 409);

    public static readonly Error ErpCustomerInUse =
        new("ErpCustomer.InUse", "لا يمكن حذف عميل مرتبط ببيانات متجر أو حسابات مستخدمين", 400);

    public static readonly Error ImportInvalidFile =
        new("ErpCustomer.Import.InvalidFile", "ملف غير صالح، يرجى رفع ملف Excel بصيغة xlsx", 400);

    public static readonly Error ImportFileTooLarge =
        new("ErpCustomer.Import.FileTooLarge", "حجم الملف كبير جداً، الحد الأقصى 10 ميجابايت", 400);

    public static readonly Error ImportEmptyFile =
        new("ErpCustomer.Import.EmptyFile", "لا يحتوي الملف على أي صفوف بيانات", 400);

    public static readonly Error ImportTooManyRows =
        new("ErpCustomer.Import.TooManyRows", "عدد الصفوف يتجاوز الحد الأقصى المسموح", 400);

    public static readonly Error ImportMissingColumns =
        new("ErpCustomer.Import.MissingColumns",
            "تعذّر التعرف على أعمدة الملف. يجب أن يحتوي الصف الأول على عناوين الأعمدة: كود العميل، اسم العميل", 400);

    public static readonly Error ImportFailed =
        new("ErpCustomer.Import.Failed", "تعذّر حفظ بيانات الاستيراد، يرجى المحاولة مرة أخرى", 500);
}
