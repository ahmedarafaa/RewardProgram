using RewardProgram.Application.Contracts.Redemption;

namespace RewardProgram.Application.Contracts.Wallet;

/// <summary>
/// Wallet history payload. <see cref="ActiveRedemption"/> is the user's single
/// in-flight redemption request (if any) — pinned above the paginated ledger so
/// it stays visible regardless of its date, and carries the cash OTP while the
/// request is pending. <see cref="Transactions"/> is the normal transaction ledger.
/// </summary>
public record WalletHistoryResponse(
    RedemptionRequestResponse? ActiveRedemption,
    PaginatedResult<WalletTransactionResponse> Transactions
);
