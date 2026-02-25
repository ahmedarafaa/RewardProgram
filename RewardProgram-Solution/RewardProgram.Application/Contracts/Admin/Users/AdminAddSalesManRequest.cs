namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminAddSalesManRequest(
    string Name,
    string MobileNumber,
    List<string> CityIds
);
