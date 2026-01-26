using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Auth;

public record AuthResponse
(
    string Token,
    string RefreshToken,
    int ExpiresIn,
    DateTime RefreshTokenExpiration,
    UserResponse User

);
public record UserResponse
(
    string Id,
    string Name,
    string MobileNumber,
    UserType UserType
);