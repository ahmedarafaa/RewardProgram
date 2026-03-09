using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class WalletErrors
{
    public static readonly Error WalletNotFound =
        new("Wallet.NotFound", "المحفظة غير موجودة", 404);

    public static readonly Error InsufficientBalance =
        new("Wallet.InsufficientBalance", "رصيد النقاط غير كافٍ", 400);
}
