using RewardProgram.Application.Abstractions;
using RewardProgram.Application.DTOs.Auth;
using RewardProgram.Application.DTOs.Auth.Additional;
using RewardProgram.Application.DTOs.Auth.UsersDTO;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Interfaces.Auth;

public interface IAuthService
{
    // Registration
    Task<Result<RegistrationResponse>> RegisterShopOwnerAsync(RegisterShopOwnerRequest request);
    Task<Result<RegistrationResponse>> RegisterSellerAsync(RegisterSellerRequest request);
    Task<Result<RegistrationResponse>> RegisterTechnicianAsync(RegisterTechnicianRequest request);
    Task<Result> VerifyRegistrationAsync(VerifyOtpRequest request);

    // Login
    Task<Result<RegistrationResponse>> LoginAsync(LoginRequest request);
    Task<Result<AuthResponse>> VerifyLoginAsync(VerifyLoginRequest request);

    // Token Management
    Task<Result<RefreshTokenResponse>> RefreshTokenAsync(RefreshTokenResponse request);
    Task<Result> RevokeTokenAsync(RefreshTokenResponse request);
}