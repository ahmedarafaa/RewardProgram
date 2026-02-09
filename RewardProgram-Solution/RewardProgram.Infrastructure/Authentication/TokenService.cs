using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RewardProgram.Infrastructure.Authentication;

public class TokenService : ITokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly UserManager<ApplicationUser> _userManager;

    public TokenService(
        IOptions<JwtOptions> jwtOptions,
        UserManager<ApplicationUser> userManager)
    {
        _jwtOptions = jwtOptions.Value;
        _userManager = userManager;
    }

    public async Task<(string Token, int ExpiresIn)> GenerateAccessTokenAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.MobilePhone, user.MobileNumber),
            new("UserType", ((int)user.UserType).ToString()),
            new("RegistrationStatus", ((int)user.RegistrationStatus).ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresIn = _jwtOptions.AccessTokenExpirationMinutes;
        var expires = DateTime.UtcNow.AddMinutes(expiresIn);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return (tokenString, expiresIn * 60); // Return expires in seconds
    }

    public (string Token, DateTime Expiration) GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var token = Convert.ToBase64String(randomBytes);
        var expiration = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

        return (token, expiration);
    }

    public async Task<AuthResponse> GenerateAuthResponseAsync(ApplicationUser user)
    {
        var (accessToken, expiresIn) = await GenerateAccessTokenAsync(user);
        var (refreshToken, refreshTokenExpiration) = GenerateRefreshToken();

        // Store new refresh token
        user.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshToken,
            ExpiresOn = refreshTokenExpiration,
            CreatedOn = DateTime.UtcNow
        });

        await _userManager.UpdateAsync(user);

        var userResponse = new UserResponse(
            Id: user.Id,
            Name: user.Name,
            MobileNumber: user.MobileNumber,
            UserType: user.UserType,
            RegistrationStatus: user.RegistrationStatus
        );

        return new AuthResponse(
            Token: accessToken,
            RefreshToken: refreshToken,
            ExpiresIn: expiresIn,
            RefreshTokenExpiration: refreshTokenExpiration,
            User: userResponse
        );
    }
}
