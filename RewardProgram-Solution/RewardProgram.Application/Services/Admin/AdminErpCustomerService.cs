using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.ErpCustomers;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Application.Services.Admin;

public class AdminErpCustomerService : IAdminErpCustomerService
{
    // ErpCustomers total ~3,200 — a generous ceiling that still bounds a single import.
    private const int MaxImportRows = 20000;

    private readonly IApplicationDbContext _context;
    private readonly IErpCustomerImportReader _importReader;
    private readonly IStringLocalizer<ErrorMessages> _localizer;
    private readonly ILogger<AdminErpCustomerService> _logger;

    public AdminErpCustomerService(
        IApplicationDbContext context,
        IErpCustomerImportReader importReader,
        IStringLocalizer<ErrorMessages> localizer,
        ILogger<AdminErpCustomerService> logger)
    {
        _context = context;
        _importReader = importReader;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<Result<AdminErpCustomerResponse>> AddErpCustomerAsync(
        AdminAddErpCustomerRequest request, string adminUserId, CancellationToken ct = default)
    {
        var code = request.CustomerCode.Trim();
        var name = request.CustomerName.Trim();
        var shortAddress = NormalizeShortAddress(request.ShortAddress);

        // Look across soft-deleted rows too: re-adding a code that belongs to a
        // soft-deleted customer revives that row (keeping its Id and history)
        // rather than failing or leaving a stale ghost. OrderBy(IsDeleted) so an
        // active row (if any) is seen before a soft-deleted one — reviving a
        // soft-deleted row while an active one exists would breach the unique code.
        var existing = await _context.ErpCustomers
            .IgnoreQueryFilters()
            .Where(e => e.CustomerCode == code)
            .OrderBy(e => e.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (existing is { IsDeleted: false })
            return Result.Failure<AdminErpCustomerResponse>(AdminErpCustomerErrors.CustomerCodeAlreadyExists);

        // ShortAddress is uniquely indexed among active customers. The revive
        // target (if any) is still soft-deleted here, so it is not counted
        // against itself.
        if (shortAddress is not null &&
            await _context.ErpCustomers.AnyAsync(e => e.ShortAddress == shortAddress, ct))
            return Result.Failure<AdminErpCustomerResponse>(AdminErpCustomerErrors.ShortAddressAlreadyExists);

        if (existing is not null)
        {
            // Soft-deleted customer with this code → revive it.
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.DeletedBy = null;
            existing.CustomerName = name;
            existing.ShortAddress = shortAddress;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("ERP customer '{Code}' revived by admin {AdminId}", code, adminUserId);

            var revivedStats = await GetDependentStatsAsync(code, ct);
            return Result.Success(MapToResponse(existing, revivedStats.HasShopData, revivedStats.LinkedUsers));
        }

        var customer = new ErpCustomer
        {
            CustomerCode = code,
            CustomerName = name,
            ShortAddress = shortAddress
        };

        await _context.ErpCustomers.AddAsync(customer, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("ERP customer '{Code}' created by admin {AdminId}", code, adminUserId);

        return Result.Success(MapToResponse(customer, false, 0));
    }

    public async Task<Result<AdminErpCustomerResponse>> EditErpCustomerAsync(
        string erpCustomerId, AdminEditErpCustomerRequest request, string adminUserId, CancellationToken ct = default)
    {
        var customer = await _context.ErpCustomers
            .FirstOrDefaultAsync(e => e.Id == erpCustomerId, ct);
        if (customer is null)
            return Result.Failure<AdminErpCustomerResponse>(AdminErpCustomerErrors.ErpCustomerNotFound);

        var shortAddress = NormalizeShortAddress(request.ShortAddress);

        // ShortAddress is uniquely indexed among active customers — exclude self.
        if (shortAddress is not null &&
            await _context.ErpCustomers.AnyAsync(
                e => e.ShortAddress == shortAddress && e.Id != customer.Id, ct))
            return Result.Failure<AdminErpCustomerResponse>(AdminErpCustomerErrors.ShortAddressAlreadyExists);

        // CustomerCode is immutable — it is the ERP key referenced by ShopData /
        // profile foreign keys. Only the display name and short address change.
        customer.CustomerName = request.CustomerName.Trim();
        customer.ShortAddress = shortAddress;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("ERP customer '{Code}' updated by admin {AdminId}", customer.CustomerCode, adminUserId);

        var stats = await GetDependentStatsAsync(customer.CustomerCode, ct);
        return Result.Success(MapToResponse(customer, stats.HasShopData, stats.LinkedUsers));
    }

    public async Task<Result> DeleteErpCustomerAsync(
        string erpCustomerId, string adminUserId, CancellationToken ct = default)
    {
        await using var transaction = await _context.BeginTransactionAsync(ct);
        try
        {
            var customer = await _context.ErpCustomers
                .FirstOrDefaultAsync(e => e.Id == erpCustomerId, ct);
            if (customer is null)
                return Result.Failure(AdminErpCustomerErrors.ErpCustomerNotFound);

            if (await HasDependentsAsync(customer.CustomerCode, ct))
                return Result.Failure(AdminErpCustomerErrors.ErpCustomerInUse);

            // SaveChangesAsync interceptor converts Remove to soft-delete (IsDeleted=true).
            _context.ErpCustomers.Remove(customer);
            await _context.SaveChangesAsync(ct);

            // Re-check inside the transaction in case shop data or a profile was
            // linked to this CustomerCode between our check and SaveChanges.
            if (await HasDependentsAsync(customer.CustomerCode, ct))
            {
                await transaction.RollbackAsync(ct);
                return Result.Failure(AdminErpCustomerErrors.ErpCustomerInUse);
            }

            await transaction.CommitAsync(ct);

            _logger.LogInformation("ERP customer '{Code}' deleted by admin {AdminId}", customer.CustomerCode, adminUserId);

            return Result.Success();
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<PaginatedResult<AdminErpCustomerResponse>>> ListErpCustomersAsync(
        AdminErpCustomerListQuery query, CancellationToken ct = default)
    {
        var dbQuery = BuildFilteredQuery(query);

        var (page, pageSize) = PaginationHelper.Normalize(query.Page, query.PageSize);

        var totalCount = await dbQuery.CountAsync(ct);

        var customers = await dbQuery
            .OrderBy(e => e.CustomerName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = await MapToResponsesAsync(customers, ct);

        return Result.Success(new PaginatedResult<AdminErpCustomerResponse>(items, totalCount, page, pageSize));
    }

    public async Task<Result<List<AdminErpCustomerResponse>>> ExportErpCustomersAsync(
        AdminErpCustomerListQuery query, CancellationToken ct = default)
    {
        var dbQuery = BuildFilteredQuery(query);

        var totalCount = await dbQuery.CountAsync(ct);
        if (totalCount > ExcelExportHelper.MaxExportRows)
            return Result.Failure<List<AdminErpCustomerResponse>>(ExportErrors.TooManyRows);

        var customers = await dbQuery
            .OrderBy(e => e.CustomerName)
            .ToListAsync(ct);

        var items = await MapToResponsesAsync(customers, ct);
        return Result.Success(items);
    }

    public async Task<Result<AdminErpCustomerResponse>> GetErpCustomerAsync(
        string erpCustomerId, CancellationToken ct = default)
    {
        var customer = await _context.ErpCustomers
            .FirstOrDefaultAsync(e => e.Id == erpCustomerId, ct);
        if (customer is null)
            return Result.Failure<AdminErpCustomerResponse>(AdminErpCustomerErrors.ErpCustomerNotFound);

        var stats = await GetDependentStatsAsync(customer.CustomerCode, ct);
        return Result.Success(MapToResponse(customer, stats.HasShopData, stats.LinkedUsers));
    }

    public async Task<Result<ErpCustomerImportResultResponse>> ImportErpCustomersAsync(
        Stream xlsxStream, string adminUserId, CancellationToken ct = default)
    {
        IReadOnlyList<ErpCustomerImportRow> rows;
        try
        {
            rows = _importReader.Read(xlsxStream, MaxImportRows);
        }
        catch (ErpCustomerImportHeaderException ex)
        {
            _logger.LogWarning(
                "ERP customer import: unrecognized columns, missing [{Missing}] (admin {AdminId})",
                string.Join(", ", ex.MissingColumns), adminUserId);
            return Result.Failure<ErpCustomerImportResultResponse>(AdminErpCustomerErrors.ImportMissingColumns);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ERP customer import: could not parse the uploaded file (admin {AdminId})", adminUserId);
            return Result.Failure<ErpCustomerImportResultResponse>(AdminErpCustomerErrors.ImportInvalidFile);
        }

        if (rows.Count == 0)
            return Result.Failure<ErpCustomerImportResultResponse>(AdminErpCustomerErrors.ImportEmptyFile);

        if (rows.Count > MaxImportRows)
            return Result.Failure<ErpCustomerImportResultResponse>(AdminErpCustomerErrors.ImportTooManyRows);

        // Load every customer whose code appears in the file so each valid row
        // becomes a case-insensitive upsert. Query filters are ignored so a code
        // belonging to a soft-deleted customer revives that row rather than
        // creating a duplicate (and a stale ghost) alongside it.
        var fileCodes = rows
            .Select(r => r.CustomerCode?.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .Select(c => c!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matched = await _context.ErpCustomers
            .IgnoreQueryFilters()
            .Where(e => fileCodes.Contains(e.CustomerCode))
            .ToListAsync(ct);

        // GroupBy guards against the (collation-dependent) chance of two codes
        // differing only by case mapping to the same case-insensitive key.
        var existing = matched
            .Where(e => !e.IsDeleted)
            .GroupBy(e => e.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var softDeleted = matched
            .Where(e => e.IsDeleted)
            .GroupBy(e => e.CustomerCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var errors = new List<ErpCustomerImportRowError>();
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var created = 0;
        var updated = 0;

        foreach (var row in rows)
        {
            var parsed = ValidateRow(row, errors);
            if (parsed is null)
                continue;

            var (code, name) = parsed.Value;

            // A code repeated within the same file is reported rather than
            // applied twice — the first occurrence already won the upsert.
            if (!seenCodes.Add(code))
            {
                errors.Add(new ErpCustomerImportRowError(row.RowNumber, code,
                    _localizer["ErpCustomerImport.Row.DuplicateInFile"]));
                continue;
            }

            if (existing.TryGetValue(code, out var customer))
            {
                customer.CustomerName = name;
                updated++;
            }
            else if (softDeleted.TryGetValue(code, out var revived))
            {
                // Re-importing a code revives the previously deleted customer.
                revived.IsDeleted = false;
                revived.DeletedAt = null;
                revived.DeletedBy = null;
                revived.CustomerName = name;
                updated++;
            }
            else
            {
                await _context.ErpCustomers.AddAsync(new ErpCustomer
                {
                    CustomerCode = code,
                    CustomerName = name
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
            _logger.LogError(ex,
                "ERP customer import by admin {AdminId}: persisting {Total} rows failed",
                adminUserId, rows.Count);
            return Result.Failure<ErpCustomerImportResultResponse>(AdminErpCustomerErrors.ImportFailed);
        }

        _logger.LogInformation(
            "ERP customer import by admin {AdminId}: {Created} created, {Updated} updated, {Failed} failed of {Total} rows",
            adminUserId, created, updated, errors.Count, rows.Count);

        return Result.Success(new ErpCustomerImportResultResponse(
            rows.Count, created, updated, errors.Count, errors));
    }

    // Validates one raw import row. On the first problem it appends a localized
    // ErpCustomerImportRowError and returns null; otherwise returns trimmed values.
    private (string Code, string Name)? ValidateRow(
        ErpCustomerImportRow row, List<ErpCustomerImportRowError> errors)
    {
        var code = row.CustomerCode?.Trim() ?? string.Empty;
        var name = row.CustomerName?.Trim() ?? string.Empty;
        var codeForError = code.Length > 0 ? code : null;

        if (code.Length is 0 or > 50)
        {
            errors.Add(new ErpCustomerImportRowError(row.RowNumber, codeForError,
                _localizer["ErpCustomerImport.Row.CustomerCodeInvalid"]));
            return null;
        }

        if (name.Length is 0 or > 200)
        {
            errors.Add(new ErpCustomerImportRowError(row.RowNumber, code,
                _localizer["ErpCustomerImport.Row.CustomerNameInvalid"]));
            return null;
        }

        return (code, name);
    }

    private IQueryable<ErpCustomer> BuildFilteredQuery(AdminErpCustomerListQuery query)
    {
        var dbQuery = _context.ErpCustomers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            dbQuery = dbQuery.Where(e =>
                e.CustomerCode.Contains(search) || e.CustomerName.Contains(search));
        }

        return dbQuery;
    }

    // Batches dependent lookups for a page of customers so the list endpoint
    // issues a fixed number of queries regardless of page size.
    private async Task<List<AdminErpCustomerResponse>> MapToResponsesAsync(
        List<ErpCustomer> customers, CancellationToken ct)
    {
        if (customers.Count == 0)
            return [];

        var codes = customers.Select(c => c.CustomerCode).ToList();

        var shopDataCodes = (await _context.ShopData
            .Where(s => codes.Contains(s.CustomerCode))
            .Select(s => s.CustomerCode)
            .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var ownerCounts = await _context.ShopOwnerProfiles
            .Where(p => codes.Contains(p.CustomerCode))
            .GroupBy(p => p.CustomerCode)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var sellerCounts = await _context.SellerProfiles
            .Where(p => codes.Contains(p.CustomerCode))
            .GroupBy(p => p.CustomerCode)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var linkedByCode = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var o in ownerCounts)
            linkedByCode[o.Code] = o.Count;
        foreach (var s in sellerCounts)
            linkedByCode[s.Code] = linkedByCode.GetValueOrDefault(s.Code) + s.Count;

        return customers.Select(c => MapToResponse(
            c,
            shopDataCodes.Contains(c.CustomerCode),
            linkedByCode.GetValueOrDefault(c.CustomerCode))).ToList();
    }

    private async Task<(bool HasShopData, int LinkedUsers)> GetDependentStatsAsync(
        string customerCode, CancellationToken ct)
    {
        var hasShopData = await _context.ShopData
            .AnyAsync(s => s.CustomerCode == customerCode, ct);
        var owners = await _context.ShopOwnerProfiles
            .CountAsync(p => p.CustomerCode == customerCode, ct);
        var sellers = await _context.SellerProfiles
            .CountAsync(p => p.CustomerCode == customerCode, ct);

        return (hasShopData, owners + sellers);
    }

    private async Task<bool> HasDependentsAsync(string customerCode, CancellationToken ct)
    {
        if (await _context.ShopData.AnyAsync(s => s.CustomerCode == customerCode, ct))
            return true;
        if (await _context.ShopOwnerProfiles.AnyAsync(p => p.CustomerCode == customerCode, ct))
            return true;
        if (await _context.SellerProfiles.AnyAsync(p => p.CustomerCode == customerCode, ct))
            return true;
        return false;
    }

    // Treats blank/whitespace ShortAddress as null so the value stays out of the
    // unique filtered index (which covers IS NOT NULL rows) and clears cleanly.
    private static string? NormalizeShortAddress(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

    private static AdminErpCustomerResponse MapToResponse(
        ErpCustomer customer, bool hasShopData, int linkedUserCount) =>
        new(
            customer.Id,
            customer.CustomerCode,
            customer.CustomerName,
            customer.ShortAddress,
            hasShopData,
            linkedUserCount
        );
}
