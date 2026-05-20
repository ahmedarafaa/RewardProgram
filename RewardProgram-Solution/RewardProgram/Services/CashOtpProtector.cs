using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using RewardProgram.Application.Interfaces;

namespace RewardProgram.API.Services;

/// <summary>
/// <see cref="ICashOtpProtector"/> backed by ASP.NET Core Data Protection.
/// The key ring is persisted to disk and namespaced to the application
/// (see <c>AddDataProtection()</c> in <c>DependencyInjection</c>), so encrypted
/// OTPs survive app restarts.
/// </summary>
public class CashOtpProtector : ICashOtpProtector
{
    // Purpose string — isolates this protector's keys from any other use of
    // Data Protection in the app. Versioned so the purpose can be rotated later.
    private const string Purpose = "RewardProgram.CashOtp.v1";

    private readonly IDataProtector _protector;
    private readonly ILogger<CashOtpProtector> _logger;

    public CashOtpProtector(IDataProtectionProvider provider, ILogger<CashOtpProtector> logger)
    {
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    public string Protect(string otp) => _protector.Protect(otp);

    public string? TryUnprotect(string? protectedOtp)
    {
        if (string.IsNullOrEmpty(protectedOtp))
            return null;

        try
        {
            return _protector.Unprotect(protectedOtp);
        }
        catch (CryptographicException ex)
        {
            // Key ring rotated or unavailable — the OTP is not displayable.
            // The user still holds it via the WhatsApp message and push notification.
            _logger.LogWarning(ex, "Cash OTP could not be decrypted for display — key unavailable.");
            return null;
        }
    }
}
