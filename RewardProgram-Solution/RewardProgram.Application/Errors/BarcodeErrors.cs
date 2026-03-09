using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class BarcodeErrors
{
    public static readonly Error BarcodeNotFound =
        new("Barcode.NotFound", "الباركود غير موجود", 404);

    public static readonly Error BarcodeAlreadyScanned =
        new("Barcode.AlreadyScanned", "تم مسح هذا الباركود مسبقاً من قبل نفس الدور", 409);

    public static readonly Error BarcodeConsumed =
        new("Barcode.Consumed", "الباركود مستهلك بالكامل", 400);

    public static readonly Error InvalidQuantity =
        new("Barcode.InvalidQuantity", "الكمية يجب أن تكون بين 1 و 10000", 400);

    public static readonly Error ConcurrencyConflict =
        new("Barcode.ConcurrencyConflict", "حدث تعارض أثناء المعالجة، يرجى المحاولة مرة أخرى", 409);
}
