using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Application.Interfaces.Auth;

public interface ITokenService
{
    Task<(string Token, int ExpiresIn)> GenerateAccessTokenAsync(ApplicationUser user);
    (string Token, DateTime Expiration) GenerateRefreshToken();
    Task<AuthResponse> GenerateAuthResponseAsync(ApplicationUser user);
}
