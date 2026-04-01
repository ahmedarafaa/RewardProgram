using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Analytics;
using RewardProgram.Application.Contracts.Admin.Dashboard;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Enums;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = UserRoles.SystemAdmin)]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _service;

    public AdminDashboardController(IAdminDashboardService service)
    {
        _service = service;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(AdminDashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var result = await _service.GetDashboardAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("analytics/users")]
    [ProducesResponseType(typeof(AdminUserAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserAnalytics(CancellationToken ct)
    {
        var result = await _service.GetUserAnalyticsAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("analytics/regions")]
    [ProducesResponseType(typeof(AdminRegionAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegionAnalytics(CancellationToken ct)
    {
        var result = await _service.GetRegionAnalyticsAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("analytics/points")]
    [ProducesResponseType(typeof(AdminPointsAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPointsAnalytics(CancellationToken ct)
    {
        var result = await _service.GetPointsAnalyticsAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("analytics/points/details")]
    [ProducesResponseType(typeof(PaginatedResult<AdminPointsDetailItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPointsDetails(
        [FromQuery] string? userId,
        [FromQuery] string? regionId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] WalletTransactionType? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new AdminPointsDetailQuery(userId, regionId, dateFrom, dateTo, type, page, pageSize);
        var result = await _service.GetPointsDetailsAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("analytics/top-performers")]
    [ProducesResponseType(typeof(TopPerformersResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopPerformers(
        [FromQuery] int top = 10,
        CancellationToken ct = default)
    {
        var result = await _service.GetTopPerformersAsync(top, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("analytics/inactive-users")]
    [ProducesResponseType(typeof(PaginatedResult<InactiveUserItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInactiveUsers(
        [FromQuery] int inactiveDays = 30,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new InactiveUsersQuery(inactiveDays, page, pageSize);
        var result = await _service.GetInactiveUsersAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("analytics/barcodes")]
    [ProducesResponseType(typeof(BarcodeAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBarcodeAnalytics(CancellationToken ct)
    {
        var result = await _service.GetBarcodeAnalyticsAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("analytics/redemptions")]
    [ProducesResponseType(typeof(RedemptionAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRedemptionAnalytics(CancellationToken ct)
    {
        var result = await _service.GetRedemptionAnalyticsAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("analytics/salesman-performance")]
    [ProducesResponseType(typeof(SalesManPerformanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalesManPerformance(CancellationToken ct)
    {
        var result = await _service.GetSalesManPerformanceAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("analytics/revenue")]
    [ProducesResponseType(typeof(RevenueAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevenueAnalytics(CancellationToken ct)
    {
        var result = await _service.GetRevenueAnalyticsAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
