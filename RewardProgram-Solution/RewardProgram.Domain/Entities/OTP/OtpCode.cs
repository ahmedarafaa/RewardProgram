using RewardProgram.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Domain.Entities.OTP;

public class OtpCode
{ 
    // Default OTP validity period in minutes.   
    public const int DefaultExpirationMinutes = 10;

    // Rate limit window in minutes for OTP requests per mobile number.
    
    public const int RateLimitWindowMinutes = 15;

    /// Maximum OTP requests allowed per mobile number within the rate limit window.    
    public const int MaxRequestsPerWindow = 3;

    public int Id { get; set; }
    public string PinId { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(DefaultExpirationMinutes);

    // Number of verification attempts made for this OTP.
    public int VerificationAttempts { get; set; }

    // Maximum allowed verification attempts before OTP is invalidated.
    public const int MaxVerificationAttempts = 5;

    // Minimum seconds between OTP resend requests.
    public const int ResendCooldownSeconds = 30;

    // For registration: store form data until OTP verified
    public string? RegistrationData { get; set; }  // JSON

    // ── SMS fallback ─────────────────────────────────────────────────────
    // PinId is the public token the mobile app holds and sends back at verify time.
    // CurrentSid is the active Twilio Verify Sid used when talking to Twilio. They
    // start equal on insert; if fallback happens (sync-error retry at /send-otp,
    // or the Twilio delivery webhook firing later), CurrentSid rotates to the new
    // SMS-channel Sid while PinId stays unchanged — so the mobile app contract is
    // preserved across the channel switch.
    public string CurrentSid { get; set; } = string.Empty;

    // "whatsapp" on insert; flipped to "sms" after fallback fires.
    public string Channel { get; set; } = "whatsapp";

    // Set true after SMS fallback has fired (sync-error retry OR webhook),
    // so we never send a second SMS for the same row.
    public bool FallbackFired { get; set; }

    // Optimistic concurrency token. The Twilio status webhook can be retried by
    // Twilio (it does so on 5xx and on timeouts), and two near-simultaneous
    // invocations would otherwise both flip FallbackFired and fire SMS twice.
    // With RowVersion, the second SaveChanges throws DbUpdateConcurrencyException
    // — the handler treats that as "already handled" and returns 200.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Check if OTP has expired.
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    // Check if OTP is still valid (not used, not expired, attempts not exceeded).
    public bool IsValid => !IsUsed && !IsExpired && VerificationAttempts < MaxVerificationAttempts;
}
