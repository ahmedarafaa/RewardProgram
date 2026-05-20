using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Redemptions;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Services.Admin;

public class AdminRedemptionService : IAdminRedemptionService
{
    private readonly IApplicationDbContext _context;

    public AdminRedemptionService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedResult<AdminRedemptionListItemResponse>>> GetAllAsync(
        AdminRedemptionListQuery query, CancellationToken ct = default)
    {
        var baseQuery = BuildRedemptionsFilteredQuery(query);

        var (page, pageSize) = PaginationHelper.Normalize(query.Page, query.PageSize);

        var totalCount = await baseQuery.CountAsync(ct);

        // EF Core 10 OrderBy-after-Select translation bug — see RedemptionListItemSelector.
        var items = await baseQuery
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(RedemptionListItemSelector)
            .ToListAsync(ct);

        return Result.Success(new PaginatedResult<AdminRedemptionListItemResponse>(items, totalCount, page, pageSize));
    }

    public async Task<Result<List<AdminRedemptionListItemResponse>>> ExportAllAsync(
        AdminRedemptionListQuery query, CancellationToken ct = default)
    {
        var baseQuery = BuildRedemptionsFilteredQuery(query);

        var totalCount = await baseQuery.CountAsync(ct);
        if (totalCount > ExcelExportHelper.MaxExportRows)
            return Result.Failure<List<AdminRedemptionListItemResponse>>(ExportErrors.TooManyRows);

        var items = await baseQuery
            .OrderByDescending(r => r.CreatedAt)
            .Select(RedemptionListItemSelector)
            .ToListAsync(ct);

        return Result.Success(items);
    }

    private IQueryable<RedemptionRequest> BuildRedemptionsFilteredQuery(AdminRedemptionListQuery query)
    {
        var baseQuery = _context.RedemptionRequests
            .Include(r => r.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.UserId))
            baseQuery = baseQuery.Where(r => r.UserId == query.UserId);

        if (query.Method.HasValue)
            baseQuery = baseQuery.Where(r => r.Method == query.Method.Value);

        if (query.Status.HasValue)
            baseQuery = baseQuery.Where(r => r.Status == query.Status.Value);

        if (!string.IsNullOrEmpty(query.RegionId))
        {
            // NationalAddress is an owned entity holding only CityId — resolve the
            // region's cities via a subquery, then match the user's city against them.
            var regionCityIds = _context.Cities
                .Where(c => c.RegionId == query.RegionId)
                .Select(c => c.Id);
            baseQuery = baseQuery.Where(r =>
                r.User.NationalAddress != null
                && regionCityIds.Contains(r.User.NationalAddress.CityId));
        }

        if (query.FromDate.HasValue)
            baseQuery = baseQuery.Where(r => r.CreatedAt >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            baseQuery = baseQuery.Where(r => r.CreatedAt <= query.ToDate.Value);

        return baseQuery;
    }

    // Expression so callers can compose it as the FINAL `.Select(...)` step after
    // OrderBy/Skip/Take — see EF Core 10 comment in GetAllAsync.
    private static readonly Expression<Func<RedemptionRequest, AdminRedemptionListItemResponse>> RedemptionListItemSelector =
        r => new AdminRedemptionListItemResponse(
            r.Id,
            r.User.Name,
            r.User.MobileNumber,
            r.Method,
            r.Status,
            r.PointsAmount,
            r.SarAmount,
            r.CreatedAt);

    public async Task<Result<AdminRedemptionResponse>> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var request = await _context.RedemptionRequests
            .Include(r => r.User)
            .Include(r => r.CashHandoverBy)
            .Include(r => r.RejectedBy)
            .Include(r => r.Approvals)
                .ThenInclude(a => a.Approver)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (request is null)
            return Result.Failure<AdminRedemptionResponse>(RedemptionErrors.RequestNotFound);

        var response = new AdminRedemptionResponse(
            request.Id,
            request.UserId,
            request.User.Name,
            request.User.MobileNumber,
            request.Method,
            request.Status,
            request.PointsAmount,
            request.SarRate,
            request.SarAmount,
            request.Iban,
            request.AccountNumber,
            request.Address,
            request.SwiftCode,
            request.AccountName,
            request.CashOtpExpiresAt,
            request.CashHandoverBy is not null
                ? request.CashHandoverBy.Name
                : null,
            request.CashHandoverAt,
            request.RejectionReason,
            request.RejectedBy is not null
                ? request.RejectedBy.Name
                : null,
            request.CreatedAt,
            request.Approvals.Select(a => new AdminRedemptionApprovalResponse(
                a.Id,
                a.Approver.Name,
                a.Action,
                a.RejectionReason,
                a.FromStatus,
                a.ToStatus,
                a.CreatedAt
            )).OrderBy(a => a.CreatedAt).ToList()
        );

        return Result.Success(response);
    }
}
