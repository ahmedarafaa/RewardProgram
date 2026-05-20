using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Barcodes;
using RewardProgram.Application.Contracts.Admin.Scans;

namespace RewardProgram.Application.Interfaces.Admin;

public interface IAdminBarcodeService
{
    Task<Result<AdminGenerateBarcodesResponse>> GenerateBarcodesAsync(AdminGenerateBarcodesRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<PaginatedResult<AdminBarcodeListItemResponse>>> ListBarcodesAsync(AdminBarcodeListQuery query, CancellationToken ct = default);
    Task<Result<List<AdminBarcodeListItemResponse>>> ExportBarcodesAsync(AdminBarcodeListQuery query, CancellationToken ct = default);
    Task<Result<PaginatedResult<AdminScanListItemResponse>>> GetAdminScanListAsync(AdminScanListQuery query, CancellationToken ct = default);
    Task<Result<List<AdminScanListItemResponse>>> ExportScansAsync(AdminScanListQuery query, CancellationToken ct = default);
    Task<Result<AdminCancelScanResponse>> CancelScanAsync(string scanId, string adminUserId, CancellationToken ct = default);
}
