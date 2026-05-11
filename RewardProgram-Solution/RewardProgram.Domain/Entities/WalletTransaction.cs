using RewardProgram.Domain.Enums;

namespace RewardProgram.Domain.Entities;

public class WalletTransaction : TrackableEntity
{
    public string WalletId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public WalletTransactionType Type { get; set; }
    public string? ReferenceId { get; set; }
    public string? Description { get; set; }
    // Localization: services write a resource key + JSON-encoded args at event
    // time. Read paths render the localized string at request time using
    // IStringLocalizer<ErrorMessages>. Older rows have null here; reads fall back
    // to the pre-rendered Description (Arabic).
    public string? DescriptionKey { get; set; }
    public string? DescriptionArgs { get; set; }  // JSON array of strings
    public decimal SarRate { get; set; }
    public decimal SarAmount { get; set; }
    public decimal RemainingAmount { get; set; }

    // Navigation
    public Wallet Wallet { get; set; } = null!;
}
