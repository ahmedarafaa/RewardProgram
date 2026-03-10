using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class FileErrors
{
    public static readonly Error ImageUploadFailed =
        new("File.ImageUploadFailed", "فشل رفع الصورة", 500);

    public static readonly Error InvalidImageType =
        new("File.InvalidImageType", "صيغة الصورة غير مدعومة، يرجى استخدام JPG أو PNG أو WEBP", 400);

    public static readonly Error ImageTooLarge =
        new("File.ImageTooLarge", "حجم الصورة كبير جداً، الحد الأقصى 5 ميجابايت", 400);
}
