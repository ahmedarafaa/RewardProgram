namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminEditSalesManRequest(
    string Name,
    List<string> CityIds
);
