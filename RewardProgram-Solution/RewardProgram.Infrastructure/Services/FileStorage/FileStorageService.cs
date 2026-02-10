using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Interfaces.Files;

namespace RewardProgram.Infrastructure.Services.FileStorage;

public class FileStorageService(IWebHostEnvironment environment, ILogger<FileStorageService> logger) : IFileStorageService
{
    private readonly IWebHostEnvironment _environment = environment;
    private readonly ILogger<FileStorageService> _logger = logger;

    public async Task<Result<string>> UploadAsync(IFormFile file, string folder, CancellationToken ct = default)
    {
        try
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream, ct);
            }

            var fileUrl = $"/uploads/{folder}/{uniqueFileName}";
            return Result.Success(fileUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file");
            return Result.Failure<string>(new Error("File.UploadFailed", "فشل رفع الملف", 500));
        }
    }

    public Task<Result> DeleteAsync(string fileUrl)
    {
        try
        {
            var filePath = Path.Combine(_environment.WebRootPath, fileUrl.TrimStart('/'));

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file");
            return Task.FromResult(Result.Failure(new Error("File.DeleteFailed", "فشل حذف الملف", 500)));
        }
    }
}
