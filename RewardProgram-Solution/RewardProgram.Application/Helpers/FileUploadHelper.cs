using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces.Files;

namespace RewardProgram.Application.Helpers;

public static class FileUploadHelper
{
    public static async Task<Result<string>> UploadShopImageAsync(
        IFormFile file, IFileStorageService fileStorageService, CancellationToken ct = default)
    {
        var validation = ImageUploadValidator.Validate(file);
        if (validation.IsFailure)
            return Result.Failure<string>(validation.Error);

        using var stream = file.OpenReadStream();
        var uploadResult = await fileStorageService.UploadAsync(stream, file.FileName, "shops", ct);

        return uploadResult.IsFailure
            ? Result.Failure<string>(FileErrors.ImageUploadFailed)
            : uploadResult;
    }
}
