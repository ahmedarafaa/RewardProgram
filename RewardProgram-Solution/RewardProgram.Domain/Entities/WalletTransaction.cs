using RewardProgram.Domain.Enums;

namespace RewardProgram.Domain.Entities;

public class WalletTransaction : TrackableEntity
{
    public string WalletId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public WalletTransactionType Type { get; set; }
    public string? ReferenceId { get; set; }
    public string? Description { get; set; }
    public decimal SarRate { get; set; }
    public decimal SarAmount { get; set; }
    public decimal RemainingAmount { get; set; }

    // Navigation
    public Wallet Wallet { get; set; } = null!;
}
