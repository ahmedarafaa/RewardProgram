using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts.Scan;

namespace RewardProgram.Application.Interfaces;

public interface IScanService
{
    Task<Result<ScanBarcodeResponse>> ScanBarcodeAsync(ScanBarcodeRequest request, string userId, CancellationToken ct = default);
}
