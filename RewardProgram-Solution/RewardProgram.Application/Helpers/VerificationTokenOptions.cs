namespace RewardProgram.Application.Helpers;

public class VerificationTokenOptions
{
    public string HmacKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 10;
}
