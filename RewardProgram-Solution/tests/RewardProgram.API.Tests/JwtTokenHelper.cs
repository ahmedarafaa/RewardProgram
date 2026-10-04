using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RewardProgram.API.Tests;

public static class JwtTokenHelper
{
    // Fed to the app as Jwt:Key by TestWebApplicationFactory. Startup refuses the
    // appsettings placeholder and keys under 32 chars, so tests need their own.
    public const string TestKey = "api-tests-signing-key-not-a-secret-0123456789";
    private const string Issuer = "RewardApp";
    private const string Audience = "RewardApp users";

    public static string GenerateToken(string userId, string role, string? name = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role)
        };

        if (name is not null)
            claims.Add(new Claim(ClaimTypes.Name, name));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void AddAuthHeader(HttpClient client, string userId, string role)
    {
        var token = GenerateToken(userId, role);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
}
