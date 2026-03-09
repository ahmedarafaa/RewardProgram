using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Barcodes;

namespace RewardProgram.Application.Interfaces.Admin;

public interface IAdminBarcodeService
{
    Task<Result<AdminGenerateBarcodesResponse>> GenerateBarcodesAsync(AdminGenerateBarcodesRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<PaginatedResult<AdminBarcodeListItemResponse>>> ListBarcodesAsync(AdminBarcodeListQuery query, CancellationToken ct = default);
}
