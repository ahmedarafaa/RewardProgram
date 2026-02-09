using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Contracts.Auth;

public record SendOtpResponse(
    string PinId,
    string MaskedMobileNumber
);
