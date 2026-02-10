using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Interfaces.Files;

public interface IFileStorageService
{
    Task<Result<string>> UploadAsync(IFormFile file, string folder, CancellationToken ct = default);
    Task<Result> DeleteAsync(string fileUrl);
}
