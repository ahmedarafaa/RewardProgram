namespace RewardProgram.Application.Contracts.Lookups;

public record CustomerShopDataStatusResponse(
    bool CustomerCodeExists,
    string? CustomerName,
    bool ShopDataExists
);
