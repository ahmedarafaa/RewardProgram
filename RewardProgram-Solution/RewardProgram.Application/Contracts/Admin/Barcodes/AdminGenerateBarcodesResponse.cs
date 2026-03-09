namespace RewardProgram.Application.Contracts.Admin.Barcodes;

public record AdminGenerateBarcodesResponse(
    int GeneratedCount,
    string ProductName
);
