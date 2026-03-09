using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Admin.Barcodes;

public record AdminBarcodeListItemResponse(
    string Id,
    string Code,
    string ProductName,
    int PointValue,
    BarcodeStatus Status,
    DateTime CreatedAt
);
