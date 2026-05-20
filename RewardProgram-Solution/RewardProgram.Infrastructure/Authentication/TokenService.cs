using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Domain.Constants;
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
    private readonly IApplicationDbContext _context;

    public TokenService(
        IOptions<JwtOptions> jwtOptions,
        UserManager<ApplicationUser> userManager,
        IApplicationDbContext context)
    {
        _jwtOptions = jwtOptions.Value;
        _userManager = userManager;
        _context = context;
    }

    public async Task<(string Token, int ExpiresIn)> GenerateAccessTokenAsync(ApplicationUser user)
        => await GenerateAccessTokenInternalAsync(user, isAdmin: false);

    private async Task<(string Token, int ExpiresIn)> GenerateAccessTokenInternalAsync(ApplicationUser user, bool isAdmin)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Name),
            new("scope", isAdmin ? JwtScopes.Admin : JwtScopes.User)
        };

        // Mobile app clients rely on these claims; admin dashboard does not need them
        if (!isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.MobilePhone, user.MobileNumber));
            claims.Add(new Claim("UserType", ((int)user.UserType).ToString()));
            claims.Add(new Claim("RegistrationStatus", ((int)user.RegistrationStatus).ToString()));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // An Admin-role dashboard user carries their granted permissions as claims
        // so authorization needs no per-request DB lookup. SystemAdmin bypasses
        // permission checks entirely, so it needs no permission claims.
        if (isAdmin && !roles.Contains(UserRoles.SystemAdmin))
        {
            var permissions = await _context.AdminUserPermissions
                .Where(p => p.UserId == user.Id)
                .Select(p => p.Permission)
                .ToListAsync();

            foreach (var permission in permissions)
            {
                claims.Add(new Claim(AdminPermissions.ClaimType, permission));
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresIn = isAdmin
            ? _jwtOptions.AdminAccessTokenExpirationMinutes
            : _jwtOptions.AccessTokenExpirationMinutes;
        var expires = DateTime.UtcNow.AddMinutes(expiresIn);
        var audience = isAdmin ? _jwtOptions.AdminAudience : _jwtOptions.Audience;

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return (tokenString, expiresIn * 60); // Return expires in seconds
    }

    public (string Token, DateTime Expiration) GenerateRefreshToken()
        => GenerateRefreshTokenInternal(isAdmin: false);

    private (string Token, DateTime Expiration) GenerateRefreshTokenInternal(bool isAdmin)
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var token = Convert.ToBase64String(randomBytes);
        var days = isAdmin
            ? _jwtOptions.AdminRefreshTokenExpirationDays
            : _jwtOptions.RefreshTokenExpirationDays;
        var expiration = DateTime.UtcNow.AddDays(days);

        return (token, expiration);
    }

    public Task<AuthResponse> GenerateAuthResponseAsync(ApplicationUser user)
        => GenerateAuthResponseInternalAsync(user, isAdmin: false);

    public Task<AuthResponse> GenerateAdminAuthResponseAsync(ApplicationUser user)
        => GenerateAuthResponseInternalAsync(user, isAdmin: true);

    private async Task<AuthResponse> GenerateAuthResponseInternalAsync(ApplicationUser user, bool isAdmin)
    {
        var (accessToken, expiresIn) = await GenerateAccessTokenInternalAsync(user, isAdmin);
        var (refreshToken, refreshTokenExpiration) = GenerateRefreshTokenInternal(isAdmin);

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
