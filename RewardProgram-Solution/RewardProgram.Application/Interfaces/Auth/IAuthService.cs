using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Contracts.Auth.Additional;
using RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

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