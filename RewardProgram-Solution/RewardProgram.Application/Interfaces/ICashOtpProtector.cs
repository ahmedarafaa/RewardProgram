namespace RewardProgram.Application.Interfaces;

/// <summary>
/// Encrypts and decrypts the cash-handover OTP for at-rest storage, so the code
/// can be displayed back to the owning user in their wallet history.
/// <para>
/// Verification of the OTP at handover still relies on the one-way SHA-256 hash
/// (<c>RedemptionRequest.CashOtpHash</c>) — this protector is display-only.
/// Decryption degrades gracefully: if the Data Protection key ring is rotated
/// or lost, <see cref="TryUnprotect"/> returns <c>null</c> and the user simply
/// falls back to the OTP copy delivered via WhatsApp / push notification.
/// </para>
/// </summary>
public interface ICashOtpProtector
{
    /// <summary>Encrypts a plaintext OTP for storage.</summary>
    string Protect(string otp);

    /// <summary>
    /// Decrypts a stored OTP. Returns <c>null</c> when the input is null/empty
    /// or cannot be decrypted (e.g. the key ring is no longer available).
    /// </summary>
    string? TryUnprotect(string? protectedOtp);
}
