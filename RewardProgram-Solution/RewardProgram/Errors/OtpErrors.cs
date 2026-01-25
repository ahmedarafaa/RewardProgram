namespace RewardProgram.API.Errors;

public static class OtpErrors
{
    public static readonly Error InvalidOtp =
        new("Otp.InvalidOtp", "رمز التحقق غير صحيح", StatusCodes.Status400BadRequest);

    public static readonly Error ExpiredOtp =
        new("Otp.ExpiredOtp", "انتهت صلاحية رمز التحقق", StatusCodes.Status400BadRequest);

    public static readonly Error AlreadyUsedOtp =
        new("Otp.AlreadyUsedOtp", "رمز التحقق مستخدم مسبقاً", StatusCodes.Status400BadRequest);

    public static readonly Error TooManyAttempts =
        new("Otp.TooManyAttempts", "محاولات كثيرة، يرجى المحاولة لاحقاً", StatusCodes.Status429TooManyRequests);

    public static readonly Error SendingFailed =
        new("Otp.SendingFailed", "فشل إرسال رمز التحقق", StatusCodes.Status500InternalServerError);
}