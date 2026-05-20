using RewardProgram.Application.Interfaces;

namespace RewardProgram.Application.Tests.TestHelpers;

/// <summary>
/// Test double for <see cref="ICashOtpProtector"/> — round-trips the OTP
/// unchanged. No real encryption is needed in unit tests; this lets tests
/// assert on the stored/displayed OTP value directly.
/// </summary>
public class FakeCashOtpProtector : ICashOtpProtector
{
    public string Protect(string otp) => otp;

    public string? TryUnprotect(string? protectedOtp) => protectedOtp;
}
