namespace RewardProgram.Application.Helpers;

public static class MobileNumberHelper
{
    public static string Mask(string mobileNumber)
    {
        if (string.IsNullOrEmpty(mobileNumber) || mobileNumber.Length < 4)
            return "****";

        return $"{mobileNumber[..3]}****{mobileNumber[^3..]}";
    }
}
