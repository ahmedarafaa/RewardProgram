using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Products;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.API.Authorization;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = UserRoles.AdminDashboard)]
public class AdminProductController : ControllerBase
{
    private readonly IAdminProductService _productService;
    private readonly IExcelExporter _excelExporter;
    private readonly IStringLocalizer<ErrorMessages> _l;

    public AdminProductController(
        IAdminProductService productService,
        IExcelExporter excelExporter,
        IStringLocalizer<ErrorMessages> localizer)
    {
        _productService = productService;
        _excelExporter = excelExporter;
        _l = localizer;
    }

    [HttpPost]
    [HasPermission(AdminPermissions.ProductsManage)]
    [ProducesResponseType(typeof(AdminProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddProduct([FromBody] AdminAddProductRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _productService.AddProductAsync(request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    // 10 MB upper bound on the uploaded workbook — well above any realistic
    // product catalogue, while still rejecting accidental large files early.
    private const long MaxImportFileBytes = 10 * 1024 * 1024;

    [HttpPost("import")]
    [HasPermission(AdminPermissions.ProductsManage)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProductImportResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ImportProducts(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return Result.Failure(ProductErrors.ProductImportInvalidFile).ToProblem();

        if (file.Length > MaxImportFileBytes)
            return Result.Failure(ProductErrors.ProductImportFileTooLarge).ToProblem();

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Result.Failure(ProductErrors.ProductImportInvalidFile).ToProblem();

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await using var stream = file.OpenReadStream();
        var result = await _productService.ImportProductsAsync(stream, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet]
    [HasPermission(AdminPermissions.ProductsView)]
    [Produces("application/json", ExcelExportHelper.ContentType)]
    [ProducesResponseType(typeof(PaginatedResult<AdminProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListProducts(
        [FromQuery] AdminProductListQuery query,
        [FromQuery] string? export,
        CancellationToken ct)
    {
        if (ExcelExportHelper.IsExportRequested(export))
        {
            var exportResult = await _productService.ExportProductsAsync(query, ct);
            return await this.ExportXlsxAsync(_excelExporter, exportResult,
                _l["Export.Sheet.Products"].Value, "products", BuildProductExportColumns(), ct);
        }

        var result = await _productService.ListProductsAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    private IReadOnlyList<ExcelColumn<AdminProductResponse>> BuildProductExportColumns() =>
    [
        new(_l["Export.Header.Name"].Value, p => p.Name),
        new(_l["Export.Header.ProductCode"].Value, p => p.ProductCode),
        new(_l["Export.Header.Category"].Value, p => p.Category),
        new(_l["Export.Header.PointValue"].Value, p => p.PointValue),
        new(_l["Export.Header.PriceSar"].Value, p => p.Price),
        new(_l["Export.Header.TotalBarcodes"].Value, p => p.TotalBarcodes),
        new(_l["Export.Header.AvailableBarcodes"].Value, p => p.AvailableBarcodes),
    ];

    [HttpGet("categories")]
    [HasPermission(AdminPermissions.ProductsView)]
    [ProducesResponseType(typeof(PaginatedResult<CategoryItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCategories([FromQuery] AdminCategoryListQuery query, CancellationToken ct)
    {
        var result = await _productService.ListCategoriesAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("{id}")]
    [HasPermission(AdminPermissions.ProductsView)]
    [ProducesResponseType(typeof(AdminProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(string id, CancellationToken ct)
    {
        var result = await _productService.GetProductAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("{id}")]
    [HasPermission(AdminPermissions.ProductsManage)]
    [ProducesResponseType(typeof(AdminProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EditProduct(string id, [FromBody] AdminEditProductRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _productService.EditProductAsync(id, request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpDelete("{id}")]
    [HasPermission(AdminPermissions.ProductsManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(string id, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _productService.DeleteProductAsync(id, adminId, ct);
        return result.IsSuccess ? NoContent() : result.ToProblem();
    }
}
