using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace RewardProgram.Infrastructure.InfobipDtos;

public record VerifyOtpRequest(
    [property: JsonPropertyName("pin")] string Pin
);