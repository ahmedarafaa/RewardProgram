namespace RewardProgram.Application.Contracts.Auth;

public record NationalAddressResponse
(
    int BuildingNumber,
    string City,
    string Street,
    string District,
    string PostalCode,
    int SubNumber   
);
