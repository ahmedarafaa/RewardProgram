using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.DTOs.Auth;

public record VerifyOtpRequest
(
    string Otp
);