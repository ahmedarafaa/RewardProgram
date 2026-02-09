using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class OtpErrors
{
    public static readonly Error InvalidOtp =
        new("Otp.InvalidOtp", "رمز التحقق غير صحيح", 400);

    public static readonly Error ExpiredOtp =
        new("Otp.ExpiredOtp", "انتهت صلاحية رمز التحقق", 400);

    public static readonly Error AlreadyUsedOtp =
        new("Otp.AlreadyUsedOtp", "رمز التحقق مستخدم مسبقاً", 400);

    public static readonly Error TooManyAttempts =
        new("Otp.TooManyAttempts", "محاولات كثيرة، يرجى المحاولة لاحقاً", 429);

    public static readonly Error SendingFailed =
        new("Otp.SendingFailed", "فشل إرسال رمز التحقق", 500);
}
