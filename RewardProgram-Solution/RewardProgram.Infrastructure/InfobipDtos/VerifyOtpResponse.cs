using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace RewardProgram.Infrastructure.InfobipDtos;

public record VerifyOtpResponse(
    [property: JsonPropertyName("pinId")] string PinId,
    [property: JsonPropertyName("msisdn")] string? Msisdn,
    [property: JsonPropertyName("verified")] bool Verified,
    [property: JsonPropertyName("attemptsRemaining")] int AttemptsRemaining
);