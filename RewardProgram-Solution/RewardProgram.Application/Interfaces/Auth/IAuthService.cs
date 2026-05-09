using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

namespace RewardProgram.Application.Interfaces.Auth;

public interface IAuthService
{
    // Registration — OTP-first flow
    Task<Result<SendOtpResponse>> SendRegistrationOtpAsync(SendOtpRequest request, CancellationToken ct = default);
    Task<Result<VerifyRegistrationOtpResponse>> VerifyRegistrationOtpAsync(VerifyRegistrationOtpRequest request, CancellationToken ct = default);
    Task<Result<ValidateRegistrationFieldsResponse>> ValidateRegistrationFieldsAsync(ValidateRegistrationFieldsRequest request, CancellationToken ct = default);
    Task<Result<RegisterResponse>> RegisterShopOwnerAsync(RegisterShopOwnerRequest request, CancellationToken ct = default);
    Task<Result<RegisterResponse>> RegisterSellerAsync(RegisterSellerRequest request, CancellationToken ct = default);
    Task<Result<RegisterResponse>> RegisterTechnicianAsync(RegisterTechnicianRequest request, CancellationToken ct = default);

    // Login
    Task<Result<SendOtpResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<AuthResponse>> VerifyLoginAsync(LoginVerifyRequest request, CancellationToken ct = default);

    // Token
    Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<Result<AuthResponse>> RefreshAdminTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<Result> RevokeTokenAsync(string refreshToken, string currentUserId, CancellationToken ct = default);
}
