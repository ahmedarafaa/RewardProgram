using Microsoft.Extensions.Localization;

namespace RewardProgram.Application.Helpers;

public static class LocalizedEnum
{
    // Resolves Enum.{TypeName}.{Value} via the localizer; falls back to the C# name
    // if the resource key is missing so a forgotten translation never surfaces as a
    // blank cell. Add the missing key to ErrorMessages.{ar,en}.resx to fix.
    public static string Display<TEnum>(TEnum value, IStringLocalizer localizer)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        var key = $"Enum.{typeof(TEnum).Name}.{name}";
        var localized = localizer[key];
        return localized.ResourceNotFound ? name : localized.Value;
    }

    public static string? Display<TEnum>(TEnum? value, IStringLocalizer localizer)
        where TEnum : struct, Enum
        => value.HasValue ? Display(value.Value, localizer) : null;
}
