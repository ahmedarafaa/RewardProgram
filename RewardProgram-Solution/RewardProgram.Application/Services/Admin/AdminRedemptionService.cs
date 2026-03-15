using Microsoft.EntityFrameworkCore;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Redemptions;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
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
        var baseQuery = _context.RedemptionRequests
            .Include(r => r.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(query.UserId))
            baseQuery = baseQuery.Where(r => r.UserId == query.UserId);

        if (query.Method.HasValue)
            baseQuery = baseQuery.Where(r => r.Method == query.Method.Value);

        if (query.Status.HasValue)
            baseQuery = baseQuery.Where(r => r.Status == query.Status.Value);

        if (query.FromDate.HasValue)
            baseQuery = baseQuery.Where(r => r.CreatedAt >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            baseQuery = baseQuery.Where(r => r.CreatedAt <= query.ToDate.Value);

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(r => r.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new AdminRedemptionListItemResponse(
                r.Id,
                r.User.Name,
                r.User.MobileNumber,
                r.Method,
                r.Status,
                r.PointsAmount,
                r.SarAmount,
                r.CreatedAt))
            .ToListAsync(ct);

        return Result.Success(new PaginatedResult<AdminRedemptionListItemResponse>(items, totalCount, query.Page, query.PageSize));
    }

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
            request.BankName,
            request.AccountHolderName,
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
