using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Abstractions;

public record Error(string Code, string Description, int? StatusCode)
{
    public static readonly Error None = new(string.Empty, string.Empty, null);

}
