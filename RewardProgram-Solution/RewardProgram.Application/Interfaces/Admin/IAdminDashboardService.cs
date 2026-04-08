using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Analytics;
using RewardProgram.Application.Contracts.Admin.Dashboard;

namespace RewardProgram.Application.Interfaces.Admin;

public interface IAdminDashboardService
{
    Task<Result<AdminDashboardResponse>> GetDashboardAsync(CancellationToken ct = default);
    Task<Result<AdminUserAnalyticsResponse>> GetUserAnalyticsAsync(CancellationToken ct = default);
    Task<Result<AdminRegionAnalyticsResponse>> GetRegionAnalyticsAsync(CancellationToken ct = default);
    Task<Result<AdminPointsAnalyticsResponse>> GetPointsAnalyticsAsync(CancellationToken ct = default);
    Task<Result<PaginatedResult<AdminPointsDetailItemResponse>>> GetPointsDetailsAsync(
        AdminPointsDetailQuery query, CancellationToken ct = default);
    Task<Result<TopPerformersResponse>> GetTopPerformersAsync(int top = 10, CancellationToken ct = default);
    Task<Result<PaginatedResult<InactiveUserItem>>> GetInactiveUsersAsync(
        InactiveUsersQuery query, CancellationToken ct = default);
    Task<Result<BarcodeAnalyticsResponse>> GetBarcodeAnalyticsAsync(CancellationToken ct = default);
    Task<Result<RedemptionAnalyticsResponse>> GetRedemptionAnalyticsAsync(CancellationToken ct = default);
    Task<Result<SalesManPerformanceResponse>> GetSalesManPerformanceAsync(CancellationToken ct = default);
    Task<Result<RevenueAnalyticsResponse>> GetRevenueAnalyticsAsync(CancellationToken ct = default);
    Task<Result<InvitationAnalyticsResponse>> GetInvitationAnalyticsAsync(int topInviters = 10, CancellationToken ct = default);
    Task<Result<NotificationAnalyticsResponse>> GetNotificationAnalyticsAsync(CancellationToken ct = default);
}
