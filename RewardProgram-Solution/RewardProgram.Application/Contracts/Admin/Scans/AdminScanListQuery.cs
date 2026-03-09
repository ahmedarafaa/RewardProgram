using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Contracts.Admin.Scans;

public record AdminScanListQuery(
    string? UserId,
    string? ProductId,
    ScannerRole? ScannerRole,
    BarcodeStatus? BarcodeStatus,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20
);
