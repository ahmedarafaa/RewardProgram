using Microsoft.AspNetCore.Http;
using RewardProgram.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace RewardProgram.Application.Interfaces.Files;

public interface IFileStorageService
{
    Task<Result<string>> UploadAsync(IFormFile file, string folder);
    Task<Result> DeleteAsync(string fileUrl);
    
}
