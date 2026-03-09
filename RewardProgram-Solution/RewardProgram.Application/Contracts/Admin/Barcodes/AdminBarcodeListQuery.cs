using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Admin.Barcodes;

public record AdminBarcodeListQuery(
    string? ProductId,
    BarcodeStatus? Status,
    int Page = 1,
    int PageSize = 20
);
