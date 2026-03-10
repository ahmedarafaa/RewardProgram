using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class ShopDataErrors
{
    public static readonly Error VatAlreadyExists =
        new("ShopData.VatAlreadyExists", "الرقم الضريبي مسجل مسبقاً", 409);

    public static readonly Error CrnAlreadyExists =
        new("ShopData.CrnAlreadyExists", "رقم السجل التجاري مسجل مسبقاً", 409);

    public static readonly Error ShortAddressAlreadyExists =
        new("ShopData.ShortAddressAlreadyExists", "العنوان المختصر مسجل مسبقاً", 409);
}
