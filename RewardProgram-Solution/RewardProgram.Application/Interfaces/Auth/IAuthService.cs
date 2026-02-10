using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Auth;
using RewardProgram.Application.Contracts.Auth.UsersRegistrationDTO;

namespace RewardProgram.Application.Interfaces.Auth;

public interface IAuthService
{
    // Registration
    Task<Result<SendOtpResponse>> RegisterShopOwnerAsync(RegisterShopOwnerRequest request, CancellationToken ct = default);
    Task<Result<SendOtpResponse>> RegisterSellerAsync(RegisterSellerRequest request, CancellationToken ct = default);
    Task<Result<SendOtpResponse>> RegisterTechnicianAsync(RegisterTechnicianRequest request, CancellationToken ct = default);
    Task<Result<RegisterResponse>> VerifyRegistrationAsync(VerifyOtpRequest request, CancellationToken ct = default);

    // Login
    Task<Result<SendOtpResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<AuthResponse>> VerifyLoginAsync(LoginVerifyRequest request, CancellationToken ct = default);

    // Token
    Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<Result> RevokeTokenAsync(string refreshToken, CancellationToken ct = default);
}
