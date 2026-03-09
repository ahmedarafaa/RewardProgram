using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Products;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Domain.Constants;
using System.Security.Claims;

namespace RewardProgram.API.Controllers.Admin;

[ApiController]
[Route("api/admin/products")]
[Authorize(Roles = UserRoles.SystemAdmin)]
public class AdminProductController : ControllerBase
{
    private readonly IAdminProductService _productService;

    public AdminProductController(IAdminProductService productService)
    {
        _productService = productService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(AdminProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddProduct([FromBody] AdminAddProductRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _productService.AddProductAsync(request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<AdminProductResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProducts([FromQuery] AdminProductListQuery query, CancellationToken ct)
    {
        var result = await _productService.ListProductsAsync(query, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AdminProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(string id, CancellationToken ct)
    {
        var result = await _productService.GetProductAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AdminProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditProduct(string id, [FromBody] AdminEditProductRequest request, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _productService.EditProductAsync(id, request, adminId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem();
    }

    [HttpDelete("{id}")]
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
