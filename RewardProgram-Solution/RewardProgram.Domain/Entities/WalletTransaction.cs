using RewardProgram.Domain.Enums;

namespace RewardProgram.Domain.Entities;

public class WalletTransaction : TrackableEntity
{
    public string WalletId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public WalletTransactionType Type { get; set; }
    public string? ReferenceId { get; set; }
    public string? Description { get; set; }

    // Navigation
    public Wallet Wallet { get; set; } = null!;
}
