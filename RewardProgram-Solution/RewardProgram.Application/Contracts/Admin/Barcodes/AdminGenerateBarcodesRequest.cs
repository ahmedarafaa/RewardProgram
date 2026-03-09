namespace RewardProgram.Application.Contracts.Admin.Barcodes;

public record AdminGenerateBarcodesRequest(
    string ProductId,
    int Quantity
);
