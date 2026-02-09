using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Infrastructure.TwilioDtos;
public record TwilioVerificationCheckResponse(
    string Sid,
    string Status,
    string To,
    bool Valid
);