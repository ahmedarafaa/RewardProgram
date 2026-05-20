using ImageMagick;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces.Files;

namespace RewardProgram.Infrastructure.Services.FileStorage;

public sealed class ImageProcessor : IImageProcessor
{
    private const int MaxLongEdgePx = 1920;
    private const uint JpegQuality = 85;

    private readonly ILogger<ImageProcessor> _logger;

    public ImageProcessor(ILogger<ImageProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<Result<ProcessedImage>> ProcessAsync(IFormFile file, CancellationToken ct = default)
    {
        try
        {
            await using var input = file.OpenReadStream();
            using var image = new MagickImage(input);

            // AutoOrient MUST run before Strip: it reads the EXIF Orientation tag and
            // rotates pixels to match. Strip then removes EXIF/GPS/etc. for privacy and
            // smaller output. Reversing this order leaves iPhone portraits sideways.
            image.AutoOrient();
            image.Strip();

            if (image.Width > MaxLongEdgePx || image.Height > MaxLongEdgePx)
            {
                var geometry = new MagickGeometry((uint)MaxLongEdgePx, (uint)MaxLongEdgePx);
                image.Resize(geometry);
            }

            image.Format = MagickFormat.Jpeg;
            image.Quality = JpegQuality;

            var output = new MemoryStream();
            await image.WriteAsync(output, ct);
            output.Position = 0;

            var fileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}.jpg";
            return Result.Success(new ProcessedImage(output, fileName, "image/jpeg"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image processing failed for {FileName}", file.FileName);
            return Result.Failure<ProcessedImage>(FileErrors.ImageProcessingFailed);
        }
    }
}
