using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Errors;

namespace RewardProgram.Application.Helpers;

/// <summary>
/// Validates uploaded image files against an extension whitelist, MIME whitelist,
/// magic-byte signature, and size cap. Use before handing a stream to IFileStorageService.
/// </summary>
public static class ImageUploadValidator
{
    public const long DefaultMaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] WebpRiff = [0x52, 0x49, 0x46, 0x46]; // "RIFF"
    private static readonly byte[] WebpWebp = [0x57, 0x45, 0x42, 0x50]; // "WEBP"

    public static Result Validate(IFormFile file, long maxSizeBytes = DefaultMaxImageSizeBytes)
    {
        if (file is null || file.Length == 0)
            return Result.Failure(FileErrors.InvalidImageType);

        if (file.Length > maxSizeBytes)
            return Result.Failure(FileErrors.ImageTooLarge);

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            return Result.Failure(FileErrors.InvalidImageType);

        if (!AllowedContentTypes.Contains(file.ContentType))
            return Result.Failure(FileErrors.InvalidImageType);

        if (!HasValidImageMagicBytes(file))
            return Result.Failure(FileErrors.InvalidImageType);

        return Result.Success();
    }

    private static bool HasValidImageMagicBytes(IFormFile file)
    {
        Span<byte> header = stackalloc byte[12];
        using var stream = file.OpenReadStream();
        var read = stream.Read(header);
        if (read < 3) return false;

        if (StartsWith(header, JpegMagic))
            return true;

        if (read >= 8 && StartsWith(header, PngMagic))
            return true;

        if (read >= 12 && StartsWith(header, WebpRiff) && header[8..12].SequenceEqual(WebpWebp))
            return true;

        return false;
    }

    private static bool StartsWith(ReadOnlySpan<byte> source, ReadOnlySpan<byte> prefix)
    {
        if (source.Length < prefix.Length) return false;
        return source[..prefix.Length].SequenceEqual(prefix);
    }
}
