namespace RewardProgram.Application.Helpers;

public static class MobileNumberHelper
{
    /// <summary>
    /// Normalizes Saudi and Egyptian mobile numbers to international format.
    /// Saudi: 05XX → +966XX, 966XX → +966XX, +966XX → as-is.
    /// Egyptian: 01XX → +201XX, 20XX → +20XX, +20XX → as-is.
    /// </summary>
    public static string Normalize(string mobileNumber)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber))
            return mobileNumber;

        var trimmed = mobileNumber.Trim();

        // Saudi: 05XXXXXXXX → +966XXXXXXXXX
        if (trimmed.StartsWith("05") && trimmed.Length == 10)
            return $"+966{trimmed[1..]}";

        if (trimmed.StartsWith("966") && !trimmed.StartsWith("+"))
            return $"+{trimmed}";

        // Egyptian: 01XXXXXXXXX → +201XXXXXXXXX
        if (trimmed.StartsWith("01") && trimmed.Length == 11)
            return $"+2{trimmed}";

        if (trimmed.StartsWith("20") && !trimmed.StartsWith("+"))
            return $"+{trimmed}";

        return trimmed;
    }

    public static string Mask(string mobileNumber)
    {
        if (string.IsNullOrEmpty(mobileNumber) || mobileNumber.Length < 4)
            return "****";

        return $"{mobileNumber[..3]}****{mobileNumber[^3..]}";
    }
}
