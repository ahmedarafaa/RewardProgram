namespace RewardProgram.Domain.Entities;

public class Wallet : TrackableEntity
{
    public string UserId { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal SarBalance { get; set; }

    // Navigation
    public Users.ApplicationUser User { get; set; } = null!;
    public List<WalletTransaction> Transactions { get; set; } = [];
}
