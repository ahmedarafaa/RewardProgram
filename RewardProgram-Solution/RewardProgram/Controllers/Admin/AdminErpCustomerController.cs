using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.ErpCustomers;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.API.Authorization;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/erp-customers")]
[Authorize(Roles = UserRoles.AdminDashboard)]
public class AdminErpCustomerController : ControllerBase
{
    // 10 MB upper bound on the uploaded workbook — well above any realistic
    // customer list, while still rejecting accidental large files early.
    private const long MaxImportFileBytes = 10 * 1024 * 1024;

    private readonly IAdminErpCustomerService _service;
    private readonly IExcelExporter _excelExporter;
    private readonly IStringLocalizer<ErrorMessages> _l;

    public AdminErpCustomerController(
        IAdminErpCustomerService service,
        IExcelExporter excelExporter,
        IStringLocalizer<ErrorMessages> localizer)
    {
        _service = service;
        _excelExporter = excelExporter;
        _l = localizer;
    }

    [HttpGet]
    [HasPermission(AdminPermissions.ErpCustomersView)]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(PaginatedResult<AdminErpCustomerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> List(
        [FromQuery] AdminErpCustomerListQuery query,
        [FromQuery] string? export,
        CancellationToken ct)
    {
        if (ExcelExportHelper.IsExportRequested(export))
        {
            var exportResult = await _service.ExportErpCustomersAsync(query, ct);
            return await this.ExportXlsxAsync(_excelExporter, exportResult,
                _l["Export.Sheet.ErpCustomers"].Value, "erp-customers", BuildExportColumns(), ct);
        }

        var result = await _service.ListErpCustomersAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    private IReadOnlyList<ExcelColumn<AdminErpCustomerResponse>> BuildExportColumns() =>
    [
        new(_l["Export.Header.CustomerCode"].Value, c => c.CustomerCode),
        new(_l["Export.Header.Name"].Value, c => c.CustomerName),
        new(_l["Export.Header.ShortAddress"].Value, c => c.ShortAddress),
    ];

    [HttpGet("{id}")]
    [HasPermission(AdminPermissions.ErpCustomersView)]
    [ProducesResponseType(typeof(AdminErpCustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var result = await _service.GetErpCustomerAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPost]
    [HasPermission(AdminPermissions.ErpCustomersManage)]
    [ProducesResponseType(typeof(AdminErpCustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add([FromBody] AdminAddErpCustomerRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _service.AddErpCustomerAsync(request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("{id}")]
    [HasPermission(AdminPermissions.ErpCustomersManage)]
    [ProducesResponseType(typeof(AdminErpCustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Edit(string id, [FromBody] AdminEditErpCustomerRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _service.EditErpCustomerAsync(id, request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpDelete("{id}")]
    [HasPermission(AdminPermissions.ErpCustomersManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _service.DeleteErpCustomerAsync(id, adminId, ct);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }

    [HttpPost("import")]
    [HasPermission(AdminPermissions.ErpCustomersManage)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ErpCustomerImportResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return Result.Failure(AdminErpCustomerErrors.ImportInvalidFile).ToProblem();

        if (file.Length > MaxImportFileBytes)
            return Result.Failure(AdminErpCustomerErrors.ImportFileTooLarge).ToProblem();

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Result.Failure(AdminErpCustomerErrors.ImportInvalidFile).ToProblem();

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await using var stream = file.OpenReadStream();
        var result = await _service.ImportErpCustomersAsync(stream, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }
}
