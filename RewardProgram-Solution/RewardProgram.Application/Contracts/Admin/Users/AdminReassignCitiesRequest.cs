namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminReassignCitiesRequest(
    List<string> CityIds,
    string ToSalesManId
);
