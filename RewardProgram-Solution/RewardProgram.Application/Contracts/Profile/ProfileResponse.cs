using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Contracts.Profile;

public record ProfileResponse(
    string Id,
    string Name,
    string MobileNumber,
    UserType UserType,
    string? ProfileImageUrl,
    decimal Points,
    string? CityName,
    string? District,
    string? Street,
    int? BuildingNumber,
    string? PostalCode,
    int? SubNumber
);
