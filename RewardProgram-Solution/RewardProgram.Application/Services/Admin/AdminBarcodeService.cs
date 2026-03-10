using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NanoidDotNet;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Barcodes;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Services.Admin;

public class AdminBarcodeService : IAdminBarcodeService
{
    private const string BarcodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz";
    private const int BarcodeLength = 12;

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

        var barcodes = new List<ProductBarcode>(request.Quantity);

        for (var i = 0; i < request.Quantity; i++)
        {
            barcodes.Add(new ProductBarcode
            {
                Code = Nanoid.Generate(BarcodeAlphabet, BarcodeLength),
                ProductId = product.Id,
                Status = BarcodeStatus.Available
            });
        }

        await _context.ProductBarcodes.AddRangeAsync(barcodes, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Generated {Count} barcodes for product '{ProductCode}' by admin {AdminId}",
            request.Quantity, product.ProductCode, adminUserId);

        var codes = barcodes.Select(b => b.Code).ToList();
        return Result.Success(new AdminGenerateBarcodesResponse(request.Quantity, product.Name, product.ProductCode, codes));
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

        var totalCount = await dbQuery.CountAsync(ct);

        var items = await dbQuery
            .OrderByDescending(b => b.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(b => new AdminBarcodeListItemResponse(
                b.Id,
                b.Code,
                b.Product.Name,
                b.Product.PointValue,
                b.Status,
                b.CreatedAt
            ))
            .ToListAsync(ct);

        return Result.Success(new PaginatedResult<AdminBarcodeListItemResponse>(items, totalCount, query.Page, query.PageSize));
    }
}
