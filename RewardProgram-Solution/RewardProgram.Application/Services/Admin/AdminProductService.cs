using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Products;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Services.Admin;

public class AdminProductService : IAdminProductService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<AdminProductService> _logger;

    public AdminProductService(
        IApplicationDbContext context,
        ILogger<AdminProductService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<AdminProductResponse>> AddProductAsync(
        AdminAddProductRequest request, string adminUserId, CancellationToken ct = default)
    {
        var codeExists = await _context.Products
            .AnyAsync(p => p.ProductCode == request.ProductCode, ct);

        if (codeExists)
            return Result.Failure<AdminProductResponse>(ProductErrors.ProductCodeAlreadyExists);

        var product = new Product
        {
            Name = request.Name,
            ProductCode = request.ProductCode,
            PointValue = request.PointValue,
            Price = request.Price,
            Category = request.Category
        };

        await _context.Products.AddAsync(product, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Product '{ProductCode}' created by admin {AdminId}", request.ProductCode, adminUserId);

        return Result.Success(MapToResponse(product, 0, 0));
    }

    public async Task<Result<AdminProductResponse>> EditProductAsync(
        string productId, AdminEditProductRequest request, string adminUserId, CancellationToken ct = default)
    {
        var product = await _context.Products.FindAsync([productId], ct);
        if (product is null)
            return Result.Failure<AdminProductResponse>(ProductErrors.ProductNotFound);

        var codeExists = await _context.Products
            .AnyAsync(p => p.ProductCode == request.ProductCode && p.Id != productId, ct);

        if (codeExists)
            return Result.Failure<AdminProductResponse>(ProductErrors.ProductCodeAlreadyExists);

        product.Name = request.Name;
        product.ProductCode = request.ProductCode;
        product.PointValue = request.PointValue;
        product.Price = request.Price;
        product.Category = request.Category;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Product '{ProductCode}' updated by admin {AdminId}", request.ProductCode, adminUserId);

        var barcodeStats = await GetBarcodeStatsAsync(productId, ct);
        return Result.Success(MapToResponse(product, barcodeStats.Total, barcodeStats.Available));
    }

    public async Task<Result> DeleteProductAsync(
        string productId, string adminUserId, CancellationToken ct = default)
    {
        var product = await _context.Products.FindAsync([productId], ct);
        if (product is null)
            return Result.Failure(ProductErrors.ProductNotFound);

        var hasBarcodes = await _context.ProductBarcodes
            .AnyAsync(b => b.ProductId == productId, ct);

        if (hasBarcodes)
            return Result.Failure(ProductErrors.ProductHasBarcodes);

        _context.Products.Remove(product);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Product '{ProductCode}' deleted by admin {AdminId}", product.ProductCode, adminUserId);

        return Result.Success();
    }

    public async Task<Result<PaginatedResult<AdminProductResponse>>> ListProductsAsync(
        AdminProductListQuery query, CancellationToken ct = default)
    {
        var dbQuery = _context.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            dbQuery = dbQuery.Where(p =>
                p.Name.Contains(search) || p.ProductCode.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
            dbQuery = dbQuery.Where(p => p.Category == query.Category);

        var totalCount = await dbQuery.CountAsync(ct);

        var products = await dbQuery
            .OrderBy(p => p.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var productIds = products.Select(p => p.Id).ToList();

        var barcodeStats = await _context.ProductBarcodes
            .Where(b => productIds.Contains(b.ProductId))
            .GroupBy(b => b.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Total = g.Count(),
                Available = g.Count(b => b.Status == BarcodeStatus.Available)
            })
            .ToDictionaryAsync(x => x.ProductId, ct);

        var items = products.Select(p =>
        {
            barcodeStats.TryGetValue(p.Id, out var stats);
            return MapToResponse(p, stats?.Total ?? 0, stats?.Available ?? 0);
        }).ToList();

        return Result.Success(new PaginatedResult<AdminProductResponse>(items, totalCount, query.Page, query.PageSize));
    }

    public async Task<Result<AdminProductResponse>> GetProductAsync(
        string productId, CancellationToken ct = default)
    {
        var product = await _context.Products.FindAsync([productId], ct);
        if (product is null)
            return Result.Failure<AdminProductResponse>(ProductErrors.ProductNotFound);

        var barcodeStats = await GetBarcodeStatsAsync(productId, ct);
        return Result.Success(MapToResponse(product, barcodeStats.Total, barcodeStats.Available));
    }

    private async Task<(int Total, int Available)> GetBarcodeStatsAsync(string productId, CancellationToken ct)
    {
        var stats = await _context.ProductBarcodes
            .Where(b => b.ProductId == productId)
            .GroupBy(b => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Available = g.Count(b => b.Status == BarcodeStatus.Available)
            })
            .FirstOrDefaultAsync(ct);

        return (stats?.Total ?? 0, stats?.Available ?? 0);
    }

    private static AdminProductResponse MapToResponse(Product product, int totalBarcodes, int availableBarcodes)
    {
        return new AdminProductResponse(
            product.Id,
            product.Name,
            product.ProductCode,
            product.PointValue,
            product.Price,
            product.Category,
            totalBarcodes,
            availableBarcodes
        );
    }
}
