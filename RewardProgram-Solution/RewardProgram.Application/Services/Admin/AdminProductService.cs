using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Products;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Services.Admin;

public class AdminProductService : IAdminProductService
{
    // Products total ~1,100 — a generous ceiling that still bounds a single import.
    private const int MaxImportRows = 10000;

    // Upper bound for the Price column (SQL decimal(10,2) → 8 integer digits).
    // A value past this would overflow on save and abort the whole batch.
    private const decimal MaxPrice = 99_999_999.99m;

    // Numeric parsing for import cells: a plain decimal only — no thousands
    // separators (so a text "1,5" cannot silently parse as 15) and no exponent.
    private const NumberStyles ImportNumberStyles =
        NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
        | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    private readonly IApplicationDbContext _context;
    private readonly IProductImportReader _importReader;
    private readonly IStringLocalizer<ErrorMessages> _localizer;
    private readonly ILogger<AdminProductService> _logger;

    public AdminProductService(
        IApplicationDbContext context,
        IProductImportReader importReader,
        IStringLocalizer<ErrorMessages> localizer,
        ILogger<AdminProductService> logger)
    {
        _context = context;
        _importReader = importReader;
        _localizer = localizer;
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
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId, ct);
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
        await using var transaction = await _context.BeginTransactionAsync(ct);
        try
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId, ct);
            if (product is null)
                return Result.Failure(ProductErrors.ProductNotFound);

            var hasBarcodes = await _context.ProductBarcodes
                .AnyAsync(b => b.ProductId == productId, ct);

            if (hasBarcodes)
                return Result.Failure(ProductErrors.ProductHasBarcodes);

            // SaveChangesAsync interceptor converts Remove to soft-delete (IsDeleted=true)
            // and populates DeletedBy/DeletedAt from current user claims.
            _context.Products.Remove(product);
            await _context.SaveChangesAsync(ct);

            // Re-check inside the transaction in case a concurrent barcode generation
            // committed between our hasBarcodes check and SaveChangesAsync.
            var racedBarcodes = await _context.ProductBarcodes
                .AnyAsync(b => b.ProductId == productId, ct);
            if (racedBarcodes)
            {
                await transaction.RollbackAsync(ct);
                return Result.Failure(ProductErrors.ProductHasBarcodes);
            }

            await transaction.CommitAsync(ct);

            _logger.LogInformation("Product '{ProductCode}' deleted by admin {AdminId}", product.ProductCode, adminUserId);

            return Result.Success();
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<PaginatedResult<AdminProductResponse>>> ListProductsAsync(
        AdminProductListQuery query, CancellationToken ct = default)
    {
        var dbQuery = BuildProductsFilteredQuery(query);

        var (page, pageSize) = PaginationHelper.Normalize(query.Page, query.PageSize);

        var totalCount = await dbQuery.CountAsync(ct);

        var products = await dbQuery
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = await MapProductsToResponsesAsync(products, ct);

        return Result.Success(new PaginatedResult<AdminProductResponse>(items, totalCount, page, pageSize));
    }

    public async Task<Result<List<AdminProductResponse>>> ExportProductsAsync(
        AdminProductListQuery query, CancellationToken ct = default)
    {
        var dbQuery = BuildProductsFilteredQuery(query);

        var totalCount = await dbQuery.CountAsync(ct);
        if (totalCount > ExcelExportHelper.MaxExportRows)
            return Result.Failure<List<AdminProductResponse>>(ExportErrors.TooManyRows);

        var products = await dbQuery
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var items = await MapProductsToResponsesAsync(products, ct);
        return Result.Success(items);
    }

    private IQueryable<Product> BuildProductsFilteredQuery(AdminProductListQuery query)
    {
        var dbQuery = _context.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            dbQuery = dbQuery.Where(p =>
                p.Name.Contains(search) || p.ProductCode.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
            dbQuery = dbQuery.Where(p => p.Category == query.Category);

        return dbQuery;
    }

    private async Task<List<AdminProductResponse>> MapProductsToResponsesAsync(
        List<Product> products, CancellationToken ct)
    {
        if (products.Count == 0)
            return [];

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

        return products.Select(p =>
        {
            barcodeStats.TryGetValue(p.Id, out var stats);
            return MapToResponse(p, stats?.Total ?? 0, stats?.Available ?? 0);
        }).ToList();
    }

    public async Task<Result<AdminProductResponse>> GetProductAsync(
        string productId, CancellationToken ct = default)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null)
            return Result.Failure<AdminProductResponse>(ProductErrors.ProductNotFound);

        var barcodeStats = await GetBarcodeStatsAsync(productId, ct);
        return Result.Success(MapToResponse(product, barcodeStats.Total, barcodeStats.Available));
    }

    public async Task<Result<PaginatedResult<CategoryItem>>> ListCategoriesAsync(
        AdminCategoryListQuery query, CancellationToken ct = default)
    {
        var dbQuery = _context.Products
            .AsNoTracking()
            .Where(p => p.Category != null && p.Category != string.Empty)
            .Select(p => p.Category!)
            .Distinct();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            dbQuery = dbQuery.Where(c => c.Contains(search));
        }

        var ordered = dbQuery.OrderBy(c => c);

        var (page, pageSize) = PaginationHelper.Normalize(query.Page, query.PageSize);
        var totalCount = await ordered.CountAsync(ct);

        var categories = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = categories
            .Select((name, index) => new CategoryItem((page - 1) * pageSize + index + 1, name))
            .ToList();

        return Result.Success(new PaginatedResult<CategoryItem>(items, totalCount, page, pageSize));
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

    public async Task<Result<ProductImportResultResponse>> ImportProductsAsync(
        Stream xlsxStream, string adminUserId, CancellationToken ct = default)
    {
        IReadOnlyList<ProductImportRow> rows;
        try
        {
            rows = _importReader.Read(xlsxStream, MaxImportRows);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Product import: could not parse the uploaded file (admin {AdminId})", adminUserId);
            return Result.Failure<ProductImportResultResponse>(ProductErrors.ProductImportInvalidFile);
        }

        if (rows.Count == 0)
            return Result.Failure<ProductImportResultResponse>(ProductErrors.ProductImportEmptyFile);

        if (rows.Count > MaxImportRows)
            return Result.Failure<ProductImportResultResponse>(ProductErrors.ProductImportTooManyRows);

        // Load every product whose code appears in the file so each valid row
        // becomes a case-insensitive upsert. Query filters are ignored so a code
        // that belongs to a soft-deleted product revives that row instead of
        // creating a duplicate (and a stale ghost) alongside it.
        var fileCodes = rows
            .Select(r => r.ProductCode?.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Select(c => c!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matched = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => fileCodes.Contains(p.ProductCode))
            .ToListAsync(ct);

        // GroupBy guards against the (collation-dependent) chance of two codes
        // differing only by case mapping to the same case-insensitive key.
        var existing = matched
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.ProductCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var softDeleted = matched
            .Where(p => p.IsDeleted)
            .GroupBy(p => p.ProductCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var errors = new List<ProductImportRowError>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var created = 0;
        var updated = 0;

        foreach (var row in rows)
        {
            var parsed = ValidateRow(row, errors);
            if (parsed is null)
                continue;

            var (name, code, pointValue, price, category) = parsed.Value;

            // A code repeated within the same file is reported rather than
            // applied twice — the first occurrence already won the upsert.
            if (!seenCodes.Add(code))
            {
                errors.Add(new ProductImportRowError(row.RowNumber, code,
                    _localizer["ProductImport.Row.DuplicateInFile"]));
                continue;
            }

            if (existing.TryGetValue(code, out var product))
            {
                product.Name = name;
                product.PointValue = pointValue;
                product.Price = price;
                product.Category = category;
                updated++;
            }
            else if (softDeleted.TryGetValue(code, out var revived))
            {
                // Re-importing a code revives the previously deleted product —
                // keeping its Id and history — instead of leaving a stale row.
                revived.IsDeleted = false;
                revived.DeletedAt = null;
                revived.DeletedBy = null;
                revived.Name = name;
                revived.PointValue = pointValue;
                revived.Price = price;
                revived.Category = category;
                updated++;
            }
            else
            {
                await _context.Products.AddAsync(new Product
                {
                    Name = name,
                    ProductCode = code,
                    PointValue = pointValue,
                    Price = price,
                    Category = category
                }, ct);
                created++;
            }
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Validation above mirrors every column constraint, so a failure
            // here is unexpected — surface a clean error rather than a raw 500,
            // and keep the (rolled-back) batch all-or-nothing.
            _logger.LogError(ex,
                "Product import by admin {AdminId}: persisting {Total} rows failed",
                adminUserId, rows.Count);
            return Result.Failure<ProductImportResultResponse>(ProductErrors.ProductImportFailed);
        }

        _logger.LogInformation(
            "Product import by admin {AdminId}: {Created} created, {Updated} updated, {Failed} failed of {Total} rows",
            adminUserId, created, updated, errors.Count, rows.Count);

        return Result.Success(new ProductImportResultResponse(
            rows.Count, created, updated, errors.Count, errors));
    }

    // Validates one raw import row. On the first problem it appends a localized
    // ProductImportRowError and returns null; otherwise returns the parsed values.
    private (string Name, string Code, int PointValue, decimal Price, string? Category)?
        ValidateRow(ProductImportRow row, List<ProductImportRowError> errors)
    {
        var name = row.Name?.Trim() ?? string.Empty;
        var code = row.ProductCode?.Trim() ?? string.Empty;
        var category = string.IsNullOrWhiteSpace(row.Category) ? null : row.Category.Trim();
        var codeForError = code.Length > 0 ? code : null;

        if (name.Length is 0 or > 200)
        {
            errors.Add(new ProductImportRowError(row.RowNumber, codeForError,
                _localizer["ProductImport.Row.NameInvalid"]));
            return null;
        }

        if (code.Length is 0 or > 50)
        {
            errors.Add(new ProductImportRowError(row.RowNumber, codeForError,
                _localizer["ProductImport.Row.ProductCodeInvalid"]));
            return null;
        }

        if (!decimal.TryParse(row.PointValue, ImportNumberStyles, CultureInfo.InvariantCulture, out var pv)
            || pv <= 0 || pv != Math.Truncate(pv) || pv > int.MaxValue)
        {
            errors.Add(new ProductImportRowError(row.RowNumber, code,
                _localizer["ProductImport.Row.PointValueInvalid"]));
            return null;
        }

        // Reject anything the Price column cannot store losslessly — a negative
        // value, an overflow of decimal(10,2), or more than two decimal places —
        // so a bad cell becomes a per-row error instead of aborting the save.
        if (!decimal.TryParse(row.Price, ImportNumberStyles, CultureInfo.InvariantCulture, out var price)
            || price < 0 || price > MaxPrice || decimal.Round(price, 2) != price)
        {
            errors.Add(new ProductImportRowError(row.RowNumber, code,
                _localizer["ProductImport.Row.PriceInvalid"]));
            return null;
        }

        if (category is { Length: > 100 })
        {
            errors.Add(new ProductImportRowError(row.RowNumber, code,
                _localizer["ProductImport.Row.CategoryTooLong"]));
            return null;
        }

        return (name, code, (int)pv, price, category);
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
