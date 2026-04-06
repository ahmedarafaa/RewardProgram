using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Errors;

public static class ProfileErrors
{
    public static readonly Error InvalidImageType =
        new("Profile.InvalidImageType", "نوع الصورة غير مدعوم، يرجى استخدام JPG أو PNG", 400);

    public static readonly Error ImageTooLarge =
        new("Profile.ImageTooLarge", "حجم الصورة يتجاوز الحد المسموح (5 ميجابايت)", 400);
}
