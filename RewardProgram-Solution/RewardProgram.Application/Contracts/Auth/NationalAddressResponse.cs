namespace RewardProgram.Application.Contracts.Auth;

public record NationalAddressResponse
(
    int BuildingNumber,
    string City,
    string Street,
    string Neighborhood,
    string PostalCode,
    int SubNumber
);
