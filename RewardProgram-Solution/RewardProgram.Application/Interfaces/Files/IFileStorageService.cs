using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Interfaces.Files;

public interface IFileStorageService
{
    Task<Result<string>> UploadAsync(Stream fileStream, string fileName, string folder, CancellationToken ct = default);
    Task<Result> DeleteAsync(string fileUrl);
}
