using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class BarcodeErrors
{
    public static readonly Error BarcodeNotFound =
        new("Barcode.NotFound", "الباركود غير موجود", 404);

    public static readonly Error BarcodeAlreadyScanned =
        new("Barcode.AlreadyScanned", "تم مسح هذا الباركود مسبقاً", 409);

    public static readonly Error BarcodeConsumed =
        new("Barcode.Consumed", "الباركود مستهلك بالكامل", 400);

    public static readonly Error InvalidQuantity =
        new("Barcode.InvalidQuantity", "الكمية يجب أن تكون بين 1 و 1000", 400);

    public static readonly Error ConcurrencyConflict =
        new("Barcode.ConcurrencyConflict", "حدث تعارض أثناء المعالجة، يرجى المحاولة مرة أخرى", 409);

    public static readonly Error CollisionRetryExhausted =
        new("Barcode.CollisionRetryExhausted", "فشل إنشاء أكواد فريدة، يرجى المحاولة مرة أخرى", 500);

    public static readonly Error ScanNotFound =
        new("Scan.NotFound", "سجل المسح غير موجود", 404);

    public static readonly Error ScanAlreadyCancelled =
        new("Scan.AlreadyCancelled", "تم إلغاء هذا المسح مسبقاً", 400);

    public static readonly Error CannotCancelFirstScan =
        new("Scan.CannotCancelFirstScan", "لا يمكن إلغاء المسح الأول بعد اكتمال المسح الثاني — قم بإلغاء المسح الثاني أولاً", 400);
}
