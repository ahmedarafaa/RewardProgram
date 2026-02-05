using RewardProgram.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Domain.Entities.OTP;

public class OtpCode
{
    public int Id { get; set; }
    public string PinId { get; set; } = string.Empty;  
    public string MobileNumber { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // For registration: store form data until OTP verified
    public string? RegistrationData { get; set; }  // JSON
}
