namespace RewardProgram.Application.Helpers;

public static class MobileNumberHelper
{
    /// <summary>
    /// Normalizes Saudi mobile numbers to +966XXXXXXXXX format.
    /// Accepts: 05XX, 966XX, +966XX formats.
    /// </summary>
    public static string Normalize(string mobileNumber)
    {
        if (string.IsNullOrWhiteSpace(mobileNumber))
            return mobileNumber;

        var trimmed = mobileNumber.Trim();

        if (trimmed.StartsWith("05") && trimmed.Length == 10)
            return $"+966{trimmed[1..]}";

        if (trimmed.StartsWith("966") && !trimmed.StartsWith("+"))
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
