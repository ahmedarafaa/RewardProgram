using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Infrastructure.TwilioDtos;
public record TwilioVerificationResponse(
    string Sid,
    string Status,
    string To,
    string Channel
);