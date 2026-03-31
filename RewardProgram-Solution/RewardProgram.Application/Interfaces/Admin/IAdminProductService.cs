using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Contracts;
using RewardProgram.Application.Contracts.Admin.Products;

namespace RewardProgram.Application.Interfaces.Admin;

public interface IAdminProductService
{
    Task<Result<AdminProductResponse>> AddProductAsync(AdminAddProductRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result<AdminProductResponse>> EditProductAsync(string productId, AdminEditProductRequest request, string adminUserId, CancellationToken ct = default);
    Task<Result> DeleteProductAsync(string productId, string adminUserId, CancellationToken ct = default);
    Task<Result<PaginatedResult<AdminProductResponse>>> ListProductsAsync(AdminProductListQuery query, CancellationToken ct = default);
    Task<Result<AdminProductResponse>> GetProductAsync(string productId, CancellationToken ct = default);
    Task<Result<PaginatedResult<CategoryItem>>> ListCategoriesAsync(AdminCategoryListQuery query, CancellationToken ct = default);
}
