using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Application.Interfaces.Auth;

public interface IJwtProvider
{
    (string token, int expiresIn) GenerateToken(ApplicationUser user);
    string? ValidateToken(string token);
}
