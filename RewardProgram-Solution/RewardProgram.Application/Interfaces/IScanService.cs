using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Scans;
using RewardProgram.Application.Contracts.Scan;

namespace RewardProgram.Application.Interfaces;

public interface IScanService
{
    Task<Result<ScanBarcodeResponse>> ScanBarcodeAsync(ScanBarcodeRequest request, string userId, CancellationToken ct = default);
    Task<Result<PaginatedResult<ScanHistoryItemResponse>>> GetScanHistoryAsync(string userId, ScanHistoryQuery query, CancellationToken ct = default);
    Task<Result<PaginatedResult<AdminScanListItemResponse>>> GetAdminScanListAsync(AdminScanListQuery query, CancellationToken ct = default);
}
