using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Abstractions;

namespace RewardProgram.Application.Interfaces.Files;

public interface IImageProcessor
{
    Task<Result<ProcessedImage>> ProcessAsync(IFormFile file, CancellationToken ct = default);
}

public sealed record ProcessedImage(Stream Content, string FileName, string ContentType);
