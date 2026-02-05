using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace RewardProgram.Infrastructure.InfobipDtos;

public record InfobipErrorResponse(
    [property: JsonPropertyName("requestError")] RequestError? RequestError
);

public record RequestError(
    [property: JsonPropertyName("serviceException")] ServiceException? ServiceException
);

public record ServiceException(
    [property: JsonPropertyName("messageId")] string? MessageId,
    [property: JsonPropertyName("text")] string? Text
);