using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces.Files;

namespace RewardProgram.Application.Helpers;

public static class FileUploadHelper
{
    public static async Task<Result<string>> UploadShopImageAsync(
        IFormFile file,
        IFileStorageService fileStorageService,
        IImageProcessor imageProcessor,
        CancellationToken ct = default)
    {
        var validation = ImageUploadValidator.Validate(file);
        if (validation.IsFailure)
            return Result.Failure<string>(validation.Error);

        var processed = await imageProcessor.ProcessAsync(file, ct);
        if (processed.IsFailure)
            return Result.Failure<string>(processed.Error);

        await using var stream = processed.Value.Content;
        var uploadResult = await fileStorageService.UploadAsync(stream, processed.Value.FileName, "shops", ct);

        return uploadResult.IsFailure
            ? Result.Failure<string>(FileErrors.ImageUploadFailed)
            : uploadResult;
    }
}
