namespace RewardProgram.Application.Contracts.Auth;

public record NationalAddressResponse
(
    int BuildingNumber,
    string Street,
    string PostalCode,
    int SubNumber
);
