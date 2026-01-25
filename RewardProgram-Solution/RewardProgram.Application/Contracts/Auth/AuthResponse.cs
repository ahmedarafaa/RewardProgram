using RewardProgram.Domain.Enums.UserEnums;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.DTOs.Auth;

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