using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Analytics;
using RewardProgram.Application.Contracts.Admin.Dashboard;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.API.Authorization;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Enums;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = UserRoles.AdminDashboard)]
[HasPermission(AdminPermissions.AnalyticsView)]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _service;
    private readonly IExcelExporter _excelExporter;
    private readonly IStringLocalizer<ErrorMessages> _l;

    public AdminDashboardController(
        IAdminDashboardService service,
        IExcelExporter excelExporter,
        IStringLocalizer<ErrorMessages> localizer)
    {
        _service = service;
        _excelExporter = excelExporter;
        _l = localizer;
    }

    #region Dashboard

    [HttpGet("dashboard")]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(AdminDashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard([FromQuery] string? export, CancellationToken ct)
    {
        var result = await _service.GetDashboardAsync(ct);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            return await this.ExportMultiSheetXlsxAsync(_excelExporter, result, "dashboard",
                (wb, data) => wb.AddSheet(_l["Export.Sheet.Summary"].Value, BuildDashboardKpis(data), BuildKpiColumns()),
                ct);
        }

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    private IEnumerable<KpiRow> BuildDashboardKpis(AdminDashboardResponse d) =>
    [
        new(_l["Export.Kpi.TotalShopOwners"].Value, d.TotalShopOwners),
        new(_l["Export.Kpi.TotalSellers"].Value, d.TotalSellers),
        new(_l["Export.Kpi.TotalTechnicians"].Value, d.TotalTechnicians),
        new(_l["Export.Kpi.TotalPendingApprovals"].Value, d.TotalPendingApprovals),
        new(_l["Export.Kpi.TotalPointsEarned"].Value, d.TotalPointsEarned),
        new(_l["Export.Kpi.TotalPointsRedeemed"].Value, d.TotalPointsRedeemed),
        new(_l["Export.Kpi.TotalSarRedeemed"].Value, d.TotalSarRedeemed),
        new(_l["Export.Kpi.TotalActiveBarcodes"].Value, d.TotalActiveBarcodes),
        new(_l["Export.Kpi.TotalScans"].Value, d.TotalScans),
        new(_l["Export.Kpi.PendingRedemptions"].Value, d.PendingRedemptions),
        new(_l["Export.Kpi.TotalInvitations"].Value, d.TotalInvitations),
        new(_l["Export.Kpi.TotalNotificationsSent"].Value, d.TotalNotificationsSent),
        new(_l["Export.Kpi.TotalDeletedAccounts"].Value, d.TotalDeletedAccounts),
    ];

    #endregion

    #region Users Analytics

    [HttpGet("analytics/users")]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(AdminUserAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserAnalytics([FromQuery] string? export, CancellationToken ct)
    {
        var result = await _service.GetUserAnalyticsAsync(ct);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            return await this.ExportMultiSheetXlsxAsync(_excelExporter, result, "user-analytics",
                (wb, data) => wb
                    .AddSheet(_l["Export.Sheet.UsersByType"].Value, data.CountByUserType,
                        [
                            new(_l["Export.Header.Type"].Value, x => LocalizedEnum.Display(x.UserType, _l)),
                            new(_l["Export.Header.Count"].Value, x => x.Count),
                        ])
                    .AddSheet(_l["Export.Sheet.UsersByRegStatus"].Value, data.CountByRegistrationStatus,
                        [
                            new(_l["Export.Header.RegistrationStatus"].Value, x => LocalizedEnum.Display(x.Status, _l)),
                            new(_l["Export.Header.Count"].Value, x => x.Count),
                        ])
                    .AddSheet(_l["Export.Sheet.UsersByRegion"].Value, data.CountByRegion,
                        BuildRegionCountColumns<RegionUserCount>(x => x.RegionNameAr, x => x.RegionNameEn, x => x.Count))
                    .AddSheet(_l["Export.Sheet.UsersTrend"].Value, data.RegistrationTrend, BuildMonthlyCountColumns()),
                ct);
        }

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    #endregion

    #region Regions Analytics

    [HttpGet("analytics/regions")]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(AdminRegionAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegionAnalytics([FromQuery] string? export, CancellationToken ct)
    {
        var result = await _service.GetRegionAnalyticsAsync(ct);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            return await this.ExportMultiSheetXlsxAsync(_excelExporter, result, "region-analytics",
                (wb, data) =>
                {
                    wb.AddSheet(_l["Export.Sheet.Regions"].Value, data.Regions,
                    [
                        new(_l["Export.Header.RegionAr"].Value, r => r.RegionNameAr),
                        new(_l["Export.Header.RegionEn"].Value, r => r.RegionNameEn),
                        new(_l["Export.Header.ZoneManager"].Value, r => r.ZoneManagerName),
                        new(_l["Export.Header.CityCount"].Value, r => r.CityCount),
                        new(_l["Export.Header.ShopOwnerCount"].Value, r => r.ShopOwnerCount),
                        new(_l["Export.Header.SellerCount"].Value, r => r.SellerCount),
                        new(_l["Export.Header.TechnicianCount"].Value, r => r.TechnicianCount),
                    ]);

                    // Flatten the nested City lists into a single sheet so admins
                    // can pivot/filter by region+city without expanding rows.
                    var flatCities = data.Regions
                        .SelectMany(r => r.Cities.Select(c => new
                        {
                            r.RegionNameAr,
                            r.RegionNameEn,
                            c.CityNameAr,
                            c.CityNameEn,
                            c.ApprovalSalesManName,
                            c.UserCount,
                        }))
                        .ToList();

                    wb.AddSheet(_l["Export.Sheet.Cities"].Value, flatCities,
                    [
                        new(_l["Export.Header.RegionAr"].Value, x => x.RegionNameAr),
                        new(_l["Export.Header.RegionEn"].Value, x => x.RegionNameEn),
                        new(_l["Export.Header.CityAr"].Value, x => x.CityNameAr),
                        new(_l["Export.Header.CityEn"].Value, x => x.CityNameEn),
                        new(_l["Export.Header.ApprovalSalesMan"].Value, x => x.ApprovalSalesManName),
                        new(_l["Export.Header.UserCount"].Value, x => x.UserCount),
                    ]);
                },
                ct);
        }

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    #endregion

    #region Points Analytics

    [HttpGet("analytics/points")]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(AdminPointsAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPointsAnalytics([FromQuery] string? export, CancellationToken ct)
    {
        var result = await _service.GetPointsAnalyticsAsync(ct);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            return await this.ExportMultiSheetXlsxAsync(_excelExporter, result, "points-analytics",
                (wb, data) => wb
                    .AddSheet(_l["Export.Sheet.Summary"].Value,
                        new[]
                        {
                            new KpiRow(_l["Export.Kpi.TotalEarned"].Value, data.TotalEarned),
                            new KpiRow(_l["Export.Kpi.TotalRedeemed"].Value, data.TotalRedeemed),
                            new KpiRow(_l["Export.Kpi.TotalBalance"].Value, data.TotalBalance),
                        },
                        BuildKpiColumns())
                    .AddSheet(_l["Export.Sheet.PointsByRegion"].Value, data.PointsByRegion,
                    [
                        new(_l["Export.Header.RegionAr"].Value, r => r.RegionNameAr),
                        new(_l["Export.Header.RegionEn"].Value, r => r.RegionNameEn),
                        new(_l["Export.Header.TotalEarned"].Value, r => r.TotalEarned),
                    ])
                    .AddSheet(_l["Export.Sheet.PointsByRep"].Value, data.PointsByRepresentative,
                    [
                        new(_l["Export.Header.SalesMan"].Value, r => r.SalesManName),
                        new(_l["Export.Header.TotalEarned"].Value, r => r.TotalEarned),
                        new(_l["Export.Header.UserCount"].Value, r => r.UserCount),
                    ])
                    .AddSheet(_l["Export.Sheet.PointsTrend"].Value, data.PointsTrend, BuildMonthlyDecimalColumns()),
                ct);
        }

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("analytics/points/details")]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(PaginatedResult<AdminPointsDetailItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetPointsDetails(
        [FromQuery] string? userId,
        [FromQuery] string? regionId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] WalletTransactionType? type,
        [FromQuery] string? export = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new AdminPointsDetailQuery(userId, regionId, dateFrom, dateTo, type, page, pageSize);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            var exportResult = await _service.ExportPointsDetailsAsync(query, ct);
            return await this.ExportXlsxAsync(_excelExporter, exportResult,
                _l["Export.Sheet.PointsDetails"].Value, "points-details",
                BuildPointsDetailColumns(), ct);
        }

        var result = await _service.GetPointsDetailsAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    private IReadOnlyList<ExcelColumn<AdminPointsDetailItemResponse>> BuildPointsDetailColumns() =>
    [
        new(_l["Export.Header.CreatedAt"].Value, p => p.CreatedAt),
        new(_l["Export.Header.User"].Value, p => p.UserName),
        new(_l["Export.Header.Mobile"].Value, p => p.UserMobile),
        new(_l["Export.Header.Type"].Value, p => LocalizedEnum.Display(p.Type, _l)),
        new(_l["Export.Header.PointsAwarded"].Value, p => p.Amount),
        new(_l["Export.Header.Sar"].Value, p => p.SarAmount),
        new(_l["Export.Header.Description"].Value, p => p.Description),
    ];

    #endregion

    #region Top Performers

    [HttpGet("analytics/top-performers")]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(TopPerformersResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopPerformers(
        [FromQuery] int top = 10,
        [FromQuery] string? export = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetTopPerformersAsync(top, ct);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            return await this.ExportMultiSheetXlsxAsync(_excelExporter, result, "top-performers",
                (wb, data) => wb
                    .AddSheet(_l["Export.Sheet.TopSellers"].Value, data.TopSellers, BuildTopPerformerColumns())
                    .AddSheet(_l["Export.Sheet.TopTechnicians"].Value, data.TopTechnicians, BuildTopPerformerColumns()),
                ct);
        }

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    private IReadOnlyList<ExcelColumn<TopPerformerItem>> BuildTopPerformerColumns() =>
    [
        new(_l["Export.Header.Name"].Value, p => p.UserName),
        new(_l["Export.Header.Mobile"].Value, p => p.MobileNumber),
        new(_l["Export.Header.Region"].Value, p => p.RegionNameAr),
        new(_l["Export.Header.TotalEarned"].Value, p => p.TotalPointsEarned),
        new(_l["Export.Header.TotalScans"].Value, p => p.TotalScans),
    ];

    #endregion

    #region Inactive Users

    [HttpGet("analytics/inactive-users")]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(PaginatedResult<InactiveUserItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetInactiveUsers(
        [FromQuery] int inactiveDays = 30,
        [FromQuery] string? export = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new InactiveUsersQuery(inactiveDays, page, pageSize);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            var exportResult = await _service.ExportInactiveUsersAsync(query, ct);
            return await this.ExportXlsxAsync(_excelExporter, exportResult,
                _l["Export.Sheet.InactiveUsers"].Value, "inactive-users",
                BuildInactiveUserColumns(), ct);
        }

        var result = await _service.GetInactiveUsersAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    private IReadOnlyList<ExcelColumn<InactiveUserItem>> BuildInactiveUserColumns() =>
    [
        new(_l["Export.Header.Name"].Value, u => u.UserName),
        new(_l["Export.Header.Mobile"].Value, u => u.MobileNumber),
        new(_l["Export.Header.Type"].Value, u => LocalizedEnum.Display(u.UserType, _l)),
        new(_l["Export.Header.LastScanDate"].Value, u => u.LastScanDate),
        new(_l["Export.Header.DaysSinceLastScan"].Value, u => u.DaysSinceLastScan),
    ];

    #endregion

    #region Barcodes Analytics

    [HttpGet("analytics/barcodes")]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(BarcodeAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBarcodeAnalytics([FromQuery] string? export, CancellationToken ct)
    {
        var result = await _service.GetBarcodeAnalyticsAsync(ct);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            return await this.ExportMultiSheetXlsxAsync(_excelExporter, result, "barcode-analytics",
                (wb, data) => wb
                    .AddSheet(_l["Export.Sheet.Summary"].Value,
                        new[]
                        {
                            new KpiRow(_l["Export.Kpi.TotalGenerated"].Value, data.TotalGenerated),
                            new KpiRow(_l["Export.Kpi.TotalAvailable"].Value, data.TotalAvailable),
                            new KpiRow(_l["Export.Kpi.TotalSellerScanned"].Value, data.TotalSellerScanned),
                            new KpiRow(_l["Export.Kpi.TotalTechnicianScanned"].Value, data.TotalTechnicianScanned),
                            new KpiRow(_l["Export.Kpi.TotalConsumed"].Value, data.TotalConsumed),
                            new KpiRow(_l["Export.Kpi.ScanRate"].Value, data.ScanRate),
                        },
                        BuildKpiColumns())
                    .AddSheet(_l["Export.Sheet.TopProductsByBarcodes"].Value, data.TopProductsByBarcodes,
                    [
                        new(_l["Export.Header.Product"].Value, p => p.ProductName),
                        new(_l["Export.Header.ProductCode"].Value, p => p.ProductCode),
                        new(_l["Export.Header.TotalBarcodes"].Value, p => p.TotalBarcodes),
                        new(_l["Export.Header.ScannedCount"].Value, p => p.ScannedCount),
                        new(_l["Export.Header.ConsumedCount"].Value, p => p.ConsumedCount),
                    ]),
                ct);
        }

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    #endregion

    #region Redemptions Analytics

    [HttpGet("analytics/redemptions")]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(RedemptionAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRedemptionAnalytics([FromQuery] string? export, CancellationToken ct)
    {
        var result = await _service.GetRedemptionAnalyticsAsync(ct);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            return await this.ExportMultiSheetXlsxAsync(_excelExporter, result, "redemption-analytics",
                (wb, data) => wb
                    .AddSheet(_l["Export.Sheet.Summary"].Value,
                        new[]
                        {
                            new KpiRow(_l["Export.Kpi.TotalSarRedeemed"].Value, data.TotalSarRedeemed),
                            new KpiRow(_l["Export.Kpi.AverageProcessingDays"].Value, data.AverageProcessingDays),
                            new KpiRow(_l["Export.Kpi.PendingRedemptions"].Value, data.PendingCount),
                        },
                        BuildKpiColumns())
                    .AddSheet(_l["Export.Sheet.RedemptionsByStatus"].Value, data.CountByStatus,
                    [
                        new(_l["Export.Header.Status"].Value, x => LocalizedEnum.Display(x.Status, _l)),
                        new(_l["Export.Header.Count"].Value, x => x.Count),
                        new(_l["Export.Header.Sar"].Value, x => x.TotalSar),
                    ])
                    .AddSheet(_l["Export.Sheet.RedemptionsByMethod"].Value, data.CountByMethod,
                    [
                        new(_l["Export.Header.Method"].Value, x => LocalizedEnum.Display(x.Method, _l)),
                        new(_l["Export.Header.Count"].Value, x => x.Count),
                        new(_l["Export.Header.Sar"].Value, x => x.TotalSar),
                    ])
                    .AddSheet(_l["Export.Sheet.RedemptionsTrend"].Value, data.RedemptionTrend, BuildMonthlyDecimalColumns()),
                ct);
        }

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    #endregion

    #region Salesman Performance

    [HttpGet("analytics/salesman-performance")]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(SalesManPerformanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalesManPerformance([FromQuery] string? export, CancellationToken ct)
    {
        var result = await _service.GetSalesManPerformanceAsync(ct);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            return await this.ExportMultiSheetXlsxAsync(_excelExporter, result, "salesman-performance",
                (wb, data) => wb.AddSheet(_l["Export.Sheet.SalesManPerformance"].Value, data.SalesMen,
                [
                    new(_l["Export.Header.Name"].Value, s => s.SalesManName),
                    new(_l["Export.Header.Mobile"].Value, s => s.MobileNumber),
                    new(_l["Export.Header.AssignedUsers"].Value, s => s.AssignedUserCount),
                    new(_l["Export.Header.ApprovedUsers"].Value, s => s.ApprovedUserCount),
                    new(_l["Export.Header.PendingApprovals"].Value, s => s.PendingApprovalCount),
                    new(_l["Export.Header.TotalEarned"].Value, s => s.TotalPointsEarned),
                    new(_l["Export.Header.CityCount"].Value, s => s.CityCount),
                ]),
                ct);
        }

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    #endregion

    #region Revenue Analytics

    [HttpGet("analytics/revenue")]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(RevenueAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevenueAnalytics([FromQuery] string? export, CancellationToken ct)
    {
        var result = await _service.GetRevenueAnalyticsAsync(ct);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            return await this.ExportMultiSheetXlsxAsync(_excelExporter, result, "revenue-analytics",
                (wb, data) => wb
                    .AddSheet(_l["Export.Sheet.Summary"].Value,
                        new[]
                        {
                            new KpiRow(_l["Export.Kpi.TotalSarLiability"].Value, data.TotalSarLiability),
                            new KpiRow(_l["Export.Kpi.TotalSarHeld"].Value, data.TotalSarHeld),
                            new KpiRow(_l["Export.Kpi.TotalSarPaidOut"].Value, data.TotalSarPaidOut),
                            new KpiRow(_l["Export.Kpi.TotalPointsOutstanding"].Value, data.TotalPointsOutstanding),
                        },
                        BuildKpiColumns())
                    .AddSheet(_l["Export.Sheet.RevenueByType"].Value, data.VolumeByType,
                    [
                        new(_l["Export.Header.TransactionType"].Value, v => v.Type),
                        new(_l["Export.Header.Count"].Value, v => v.Count),
                        new(_l["Export.Header.TotalPoints"].Value, v => v.TotalPoints),
                        new(_l["Export.Header.Sar"].Value, v => v.TotalSar),
                    ])
                    .AddSheet(_l["Export.Sheet.PayoutTrend"].Value, data.PayoutTrend, BuildMonthlyDecimalColumns()),
                ct);
        }

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    #endregion

    #region Invitations Analytics

    [HttpGet("analytics/invitations")]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(InvitationAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvitationAnalytics(
        [FromQuery] int top = 10,
        [FromQuery] string? export = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetInvitationAnalyticsAsync(top, ct);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            return await this.ExportMultiSheetXlsxAsync(_excelExporter, result, "invitation-analytics",
                (wb, data) => wb
                    .AddSheet(_l["Export.Sheet.Summary"].Value,
                        new[]
                        {
                            new KpiRow(_l["Export.Kpi.TotalInvitationsSent"].Value, data.TotalInvitationsSent),
                            new KpiRow(_l["Export.Kpi.TotalAccepted"].Value, data.TotalAccepted),
                            new KpiRow(_l["Export.Kpi.TotalPending"].Value, data.TotalPending),
                            new KpiRow(_l["Export.Kpi.ConversionRate"].Value, data.ConversionRate),
                            new KpiRow(_l["Export.Kpi.TotalRewardPointsSpent"].Value, data.TotalRewardPointsSpent),
                            new KpiRow(_l["Export.Kpi.TotalRewardSarSpent"].Value, data.TotalRewardSarSpent),
                        },
                        BuildKpiColumns())
                    .AddSheet(_l["Export.Sheet.TopInviters"].Value, data.TopInviters,
                    [
                        new(_l["Export.Header.Name"].Value, i => i.UserName),
                        new(_l["Export.Header.Mobile"].Value, i => i.MobileNumber),
                        new(_l["Export.Header.TotalInvited"].Value, i => i.TotalInvited),
                        new(_l["Export.Header.ApprovedCount"].Value, i => i.ApprovedCount),
                        new(_l["Export.Header.PointsEarned"].Value, i => i.PointsEarned),
                    ])
                    .AddSheet(_l["Export.Sheet.InvitationsTrend"].Value, data.InvitationTrend, BuildMonthlyCountColumns()),
                ct);
        }

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    #endregion

    #region Notifications Analytics

    [HttpGet("analytics/notifications")]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(NotificationAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotificationAnalytics([FromQuery] string? export, CancellationToken ct)
    {
        var result = await _service.GetNotificationAnalyticsAsync(ct);

        if (ExcelExportHelper.IsExportRequested(export))
        {
            return await this.ExportMultiSheetXlsxAsync(_excelExporter, result, "notification-analytics",
                (wb, data) => wb
                    .AddSheet(_l["Export.Sheet.Summary"].Value,
                        new[]
                        {
                            new KpiRow(_l["Export.Kpi.TotalAllTime"].Value, data.TotalAllTime),
                            new KpiRow(_l["Export.Kpi.TotalThisMonth"].Value, data.TotalThisMonth),
                            new KpiRow(_l["Export.Kpi.TotalToday"].Value, data.TotalToday),
                            new KpiRow(_l["Export.Kpi.TotalRead"].Value, data.TotalRead),
                            new KpiRow(_l["Export.Kpi.TotalUnread"].Value, data.TotalUnread),
                            new KpiRow(_l["Export.Kpi.ReadRate"].Value, data.ReadRate),
                            new KpiRow(_l["Export.Kpi.AdminSentCount"].Value, data.AdminSentCount),
                            new KpiRow(_l["Export.Kpi.SystemTriggeredCount"].Value, data.SystemTriggeredCount),
                        },
                        BuildKpiColumns())
                    .AddSheet(_l["Export.Sheet.NotificationsByType"].Value, data.CountByType,
                    [
                        new(_l["Export.Header.Type"].Value, x => LocalizedEnum.Display(x.Type, _l)),
                        new(_l["Export.Header.Count"].Value, x => x.Count),
                    ])
                    .AddSheet(_l["Export.Sheet.NotificationsTrend"].Value, data.NotificationTrend, BuildMonthlyCountColumns()),
                ct);
        }

        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    #endregion

    #region Shared Column Builders

    // Shared (Label, Value) shape used by every analytics "Summary" sheet that
    // flattens scalar KPIs from a composite response.
    private IReadOnlyList<ExcelColumn<KpiRow>> BuildKpiColumns() =>
    [
        new(_l["Export.Kpi.Label"].Value, k => k.Label),
        new(_l["Export.Kpi.Value"].Value, k => k.Value),
    ];

    private IReadOnlyList<ExcelColumn<MonthlyCount>> BuildMonthlyCountColumns() =>
    [
        new(_l["Export.Header.Year"].Value, m => m.Year),
        new(_l["Export.Header.Month"].Value, m => m.Month),
        new(_l["Export.Header.Count"].Value, m => m.Count),
    ];

    private IReadOnlyList<ExcelColumn<MonthlyDecimalCount>> BuildMonthlyDecimalColumns() =>
    [
        new(_l["Export.Header.Year"].Value, m => m.Year),
        new(_l["Export.Header.Month"].Value, m => m.Month),
        new(_l["Export.Header.Total"].Value, m => m.Total),
    ];

    private IReadOnlyList<ExcelColumn<T>> BuildRegionCountColumns<T>(
        Func<T, string> nameArSelector,
        Func<T, string> nameEnSelector,
        Func<T, int> countSelector) =>
    [
        new(_l["Export.Header.RegionAr"].Value, x => nameArSelector(x)),
        new(_l["Export.Header.RegionEn"].Value, x => nameEnSelector(x)),
        new(_l["Export.Header.Count"].Value, x => countSelector(x)),
    ];

    #endregion
}
