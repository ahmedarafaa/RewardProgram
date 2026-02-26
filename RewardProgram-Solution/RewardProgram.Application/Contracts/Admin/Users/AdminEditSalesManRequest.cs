namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminEditSalesManRequest(
    string Name,
    string MobileNumber,
    List<string> CityIds
);
