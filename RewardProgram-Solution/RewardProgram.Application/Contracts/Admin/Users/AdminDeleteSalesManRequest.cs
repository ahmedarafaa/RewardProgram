namespace RewardProgram.Application.Contracts.Admin.Users;

public record AdminCityReassignment(
    string CityId,
    string NewSalesManId
);

public record AdminDeleteSalesManRequest(
    List<AdminCityReassignment> CityReassignments
);
