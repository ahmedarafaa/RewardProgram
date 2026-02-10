using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Infrastructure.Authentication;

public class TwilioOptions
{
    public const string SectionName = "Twilio";

    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string VerifyServiceSid { get; set; } = string.Empty;

    /// Enable mock mode for development/testing. MUST be false in production.
    /// When true, accepts any 6-digit OTP code.
    public bool UseMockMode { get; set; } = false;
    public string WhatsAppFromNumber { get; set; } = string.Empty;
}
