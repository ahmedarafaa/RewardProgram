using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Interfaces.Auth;

public interface IAuthService
{
    // Registration
    Task<Result<SendOtpResponse>> RegisterShopOwnerAsync(RegisterShopOwnerRequest request);
    Task<Result<SendOtpResponse>> RegisterSellerAsync(RegisterSellerRequest request);
    Task<Result<SendOtpResponse>> RegisterTechnicianAsync(RegisterTechnicianRequest request);
    Task<Result<RegisterResponse>> VerifyRegistrationAsync(VerifyOtpRequest request);

    // Login
    Task<Result<SendOtpResponse>> LoginAsync(LoginRequest request);
    Task<Result<AuthResponse>> VerifyLoginAsync(LoginVerifyRequest request);

    // Token
    Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken);
    Task<Result> RevokeTokenAsync(string refreshToken);
}
