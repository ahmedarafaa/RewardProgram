using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces.Files;

namespace RewardProgram.Application.Helpers;

public static class FileUploadHelper
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedExtensions =
        [".jpg", ".jpeg", ".png", ".webp"];

    public static async Task<Result<string>> UploadShopImageAsync(
        IFormFile file, IFileStorageService fileStorageService, CancellationToken ct = default)
    {
        if (file.Length > MaxImageSizeBytes)
            return Result.Failure<string>(FileErrors.ImageTooLarge);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return Result.Failure<string>(FileErrors.InvalidImageType);

        using var stream = file.OpenReadStream();
        var uploadResult = await fileStorageService.UploadAsync(stream, file.FileName, "shops", ct);

        return uploadResult.IsFailure
            ? Result.Failure<string>(FileErrors.ImageUploadFailed)
            : uploadResult;
    }
}
