using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.DTOs.Auth.Additional;

public record RegistrationResponse
(
    string Message,
    int OtpExpiresInSeconds
);
