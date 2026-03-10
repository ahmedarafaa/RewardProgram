using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NanoidDotNet;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Barcodes;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Services.Admin;

public class AdminBarcodeService : IAdminBarcodeService
{
    private const string BarcodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz";
    private const int BarcodeLength = 12;
    private const int MaxCollisionRetries = 3;

    private readonly IApplicationDbContext _context;
    private readonly ILogger<AdminBarcodeService> _logger;

    public AdminBarcodeService(
        IApplicationDbContext context,
        ILogger<AdminBarcodeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<AdminGenerateBarcodesResponse>> GenerateBarcodesAsync(
        AdminGenerateBarcodesRequest request, string adminUserId, CancellationToken ct = default)
    {
        var product = await _context.Products.FindAsync([request.ProductId], ct);
        if (product is null)
            return Result.Failure<AdminGenerateBarcodesResponse>(ProductErrors.ProductNotFound);

        var codes = new HashSet<string>(request.Quantity);

        // Generate unique codes with collision detection
        for (var i = 0; i < request.Quantity; i++)
            codes.Add(GenerateUniqueCode(codes));

        // Check for collisions against existing DB codes (retry on conflict)
        for (var attempt = 0; attempt <= MaxCollisionRetries; attempt++)
        {
            var existingCodes = await _context.ProductBarcodes
                .Where(b => codes.Contains(b.Code))
                .Select(b => b.Code)
                .ToListAsync(ct);

            if (existingCodes.Count == 0)
                break;

            if (attempt == MaxCollisionRetries)
            {
                _logger.LogError("Failed to generate unique barcode codes after {MaxRetries} retries", MaxCollisionRetries);
                return Result.Failure<AdminGenerateBarcodesResponse>(BarcodeErrors.CollisionRetryExhausted);
            }

            foreach (var collision in existingCodes)
            {
                codes.Remove(collision);
                codes.Add(GenerateUniqueCode(codes));
            }
        }

        var barcodes = codes.Select(code => new ProductBarcode
        {
            Code = code,
            ProductId = product.Id,
            Status = BarcodeStatus.Available
        }).ToList();

        await _context.ProductBarcodes.AddRangeAsync(barcodes, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Generated {Count} barcodes for product '{ProductCode}' by admin {AdminId}",
            request.Quantity, product.ProductCode, adminUserId);

        return Result.Success(new AdminGenerateBarcodesResponse(
            request.Quantity, product.Name, product.ProductCode, codes.ToList()));
    }

    private static string GenerateUniqueCode(HashSet<string> existingCodes)
    {
        string code;
        do
        {
            code = Nanoid.Generate(BarcodeAlphabet, BarcodeLength);
        } while (existingCodes.Contains(code));

        return code;
    }

    public async Task<Result<PaginatedResult<AdminBarcodeListItemResponse>>> ListBarcodesAsync(
        AdminBarcodeListQuery query, CancellationToken ct = default)
    {
        var dbQuery = _context.ProductBarcodes
            .AsNoTracking()
            .Include(b => b.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.ProductId))
            dbQuery = dbQuery.Where(b => b.ProductId == query.ProductId);

        if (query.Status.HasValue)
            dbQuery = dbQuery.Where(b => b.Status == query.Status.Value);

        var (page, pageSize) = PaginationHelper.Normalize(query.Page, query.PageSize);

        var totalCount = await dbQuery.CountAsync(ct);

        var items = await dbQuery
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new AdminBarcodeListItemResponse(
                b.Id,
                b.Code,
                b.Product.Name,
                b.Product.PointValue,
                b.Status,
                b.CreatedAt
            ))
            .ToListAsync(ct);

        return Result.Success(new PaginatedResult<AdminBarcodeListItemResponse>(items, totalCount, page, pageSize));
    }
}
