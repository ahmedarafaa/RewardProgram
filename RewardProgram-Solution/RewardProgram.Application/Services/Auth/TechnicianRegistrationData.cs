using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Services.Auth;

internal record TechnicianRegistrationData(
    UserType UserType,
    string Name,
    string MobileNumber,
    string CityId,
    string DistrictId,
    string PostalCode,
    string AssignedSalesManId
);
