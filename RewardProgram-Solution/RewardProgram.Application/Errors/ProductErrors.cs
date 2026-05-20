using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class ProductErrors
{
    public static readonly Error ProductNotFound =
        new("Product.NotFound", "المنتج غير موجود", 404);

    public static readonly Error ProductCodeAlreadyExists =
        new("Product.CodeAlreadyExists", "كود المنتج مسجل مسبقاً", 409);

    public static readonly Error ProductHasBarcodes =
        new("Product.HasBarcodes", "لا يمكن حذف منتج مرتبط بباركودات", 400);

    public static readonly Error ProductImportInvalidFile =
        new("Product.Import.InvalidFile", "ملف غير صالح، يرجى رفع ملف Excel بصيغة xlsx", 400);

    public static readonly Error ProductImportFileTooLarge =
        new("Product.Import.FileTooLarge", "حجم الملف كبير جداً، الحد الأقصى 10 ميجابايت", 400);

    public static readonly Error ProductImportEmptyFile =
        new("Product.Import.EmptyFile", "لا يحتوي الملف على أي صفوف بيانات", 400);

    public static readonly Error ProductImportTooManyRows =
        new("Product.Import.TooManyRows", "عدد الصفوف يتجاوز الحد الأقصى المسموح", 400);
}
