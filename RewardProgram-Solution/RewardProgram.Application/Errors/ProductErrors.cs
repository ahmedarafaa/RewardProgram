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
}
