using RewardProgram.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Domain.Entities.OTP;

public class OtpCode
{
    /// <summary>
    /// Default OTP validity period in minutes.
    /// </summary>
    public const int DefaultExpirationMinutes = 5;

    /// <summary>
    /// Rate limit window in minutes for OTP requests per mobile number.
    /// </summary>
    public const int RateLimitWindowMinutes = 15;

    /// <summary>
    /// Maximum OTP requests allowed per mobile number within the rate limit window.
    /// </summary>
    public const int MaxRequestsPerWindow = 3;

    public int Id { get; set; }
    public string PinId { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(DefaultExpirationMinutes);

    /// <summary>
    /// Number of verification attempts made for this OTP.
    /// </summary>
    public int VerificationAttempts { get; set; }

    /// <summary>
    /// Maximum allowed verification attempts before OTP is invalidated.
    /// </summary>
    public const int MaxVerificationAttempts = 5;

    /// <summary>
    /// Minimum seconds between OTP resend requests.
    /// </summary>
    public const int ResendCooldownSeconds = 30;

    // For registration: store form data until OTP verified
    public string? RegistrationData { get; set; }  // JSON

    /// <summary>
    /// Check if OTP has expired.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Check if OTP is still valid (not used, not expired, attempts not exceeded).
    /// </summary>
    public bool IsValid => !IsUsed && !IsExpired && VerificationAttempts < MaxVerificationAttempts;
}
