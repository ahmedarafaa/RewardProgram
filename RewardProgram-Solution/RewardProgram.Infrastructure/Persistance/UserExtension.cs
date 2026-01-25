using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace RewardProgram.Infrastructure.Persistance;

public static class UserExtensions
{
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier);
}