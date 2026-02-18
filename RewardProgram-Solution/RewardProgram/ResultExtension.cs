using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Abstractions;

namespace RewardProgram.API;

public static class ResultExtension
{
    public static ObjectResult ToProblem(this Result result)
    {
        if (result.IsSuccess)
            throw new InvalidOperationException("cannot convert Success result to Problem");

        var problemDetails = new ProblemDetails
        {
            Status = result.Error.StatusCode,
            Extensions =
            {
                ["error"] = new[]
                {
                    new
                    {
                        result.Error.Code,
                        result.Error.Description,
                    }
                }
            }
        };

        return new ObjectResult(problemDetails)
        {
            StatusCode = result.Error.StatusCode
        };
    }
}
