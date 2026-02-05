using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace RewardProgram.Infrastructure.InfobipDtos;

public record SendOtpResponse(
    [property: JsonPropertyName("pinId")] string PinId,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("ncStatus")] string? NcStatus,
    [property: JsonPropertyName("smsStatus")] string? SmsStatus
);