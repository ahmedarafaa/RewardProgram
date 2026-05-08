using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Interfaces.Files;

namespace RewardProgram.Infrastructure.Services.FileStorage;

public class FileStorageService(IWebHostEnvironment environment, ILogger<FileStorageService> logger) : IFileStorageService
{
    private readonly IWebHostEnvironment _environment = environment;
    private readonly ILogger<FileStorageService> _logger = logger;

    private string GetWebRootPath()
    {
        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

        if (!Directory.Exists(webRoot))
            Directory.CreateDirectory(webRoot);

        return webRoot;
    }

    public async Task<Result<string>> UploadAsync(Stream fileStream, string fileName, string folder, CancellationToken ct = default)
    {
        try
        {
            var uploadsFolder = Path.Combine(GetWebRootPath(), "uploads", folder);

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var output = new FileStream(filePath, FileMode.Create))
            {
                await fileStream.CopyToAsync(output, ct);
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
            var webRoot = GetWebRootPath();
            var resolved = Path.GetFullPath(Path.Combine(webRoot, fileUrl.TrimStart('/')));

            // Defense-in-depth: refuse anything that escapes the web root.
            if (!resolved.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Refusing to delete file outside webroot: {FileUrl}", fileUrl);
                return Task.FromResult(Result.Failure(new Error("File.DeleteFailed", "فشل حذف الملف", 500)));
            }

            if (File.Exists(resolved))
            {
                File.Delete(resolved);
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
