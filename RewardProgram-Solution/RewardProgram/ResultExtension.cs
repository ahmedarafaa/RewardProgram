using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Errors;

namespace RewardProgram.API;

public static class ResultExtension
{
    public static ObjectResult ToProblem(this Result result)
    {
        if (result.IsSuccess)
            throw new InvalidOperationException("cannot convert Success result to Problem");

        var description = result.Error.Description;

        var httpContext = new HttpContextAccessor().HttpContext;
        if (httpContext is not null)
        {
            var localizer = httpContext.RequestServices.GetService<IStringLocalizer<ErrorMessages>>();
            if (localizer is not null)
            {
                var localized = localizer[result.Error.Code];
                if (!localized.ResourceNotFound)
                    description = localized.Value;
            }
        }

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
                        Description = description,
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
