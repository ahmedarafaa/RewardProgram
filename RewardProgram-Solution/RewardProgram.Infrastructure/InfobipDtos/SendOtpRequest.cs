using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace RewardProgram.Infrastructure.InfobipDtos;

public record SendOtpRequest(
    [property: JsonPropertyName("applicationId")] string ApplicationId,
    [property: JsonPropertyName("messageId")] string MessageId,
    [property: JsonPropertyName("to")] string To
);