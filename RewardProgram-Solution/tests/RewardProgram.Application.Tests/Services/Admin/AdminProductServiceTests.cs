using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Contracts.Admin.Products;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Services.Admin;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Enums;

namespace RewardProgram.Application.Tests.Services.Admin;

public class AdminProductServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly IProductImportReader _importReader;
    private readonly AdminProductService _sut;
    private const string AdminId = "admin-1";

    public AdminProductServiceTests()
    {
        _context = TestDbContext.Create();
        _importReader = Substitute.For<IProductImportReader>();
        _sut = new AdminProductService(
            _context,
            _importReader,
            new StubLocalizer<ErrorMessages>(),
            Substitute.For<ILogger<AdminProductService>>());
    }

    public void Dispose() => _context.Dispose();

    private async Task<Product> SeedProduct(string code = "P001", string name = "Product 1", int pointValue = 100)
    {
        var product = new Product { Name = name, ProductCode = code, PointValue = pointValue, Price = 50 };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    // ── AddProduct ──

    [Fact]
    public async Task AddProduct_ValidRequest_ShouldSucceed()
    {
        var request = new AdminAddProductRequest("New Product", "NP001", 100, 50, "Electronics");

        var result = await _sut.AddProductAsync(request, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("New Product");
        result.Value.ProductCode.Should().Be("NP001");
        result.Value.TotalBarcodes.Should().Be(0);
    }

    [Fact]
    public async Task AddProduct_DuplicateCode_ShouldFail()
    {
        await SeedProduct("P001");

        var request = new AdminAddProductRequest("Another", "P001", 50, 25, null);
        var result = await _sut.AddProductAsync(request, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.ProductCodeAlreadyExists);
    }

    // ── EditProduct ──

    [Fact]
    public async Task EditProduct_ValidRequest_ShouldUpdateAllFields()
    {
        var product = await SeedProduct();

        var request = new AdminEditProductRequest("Updated", "P002", 200, 100, "New Cat");
        var result = await _sut.EditProductAsync(product.Id, request, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Updated");
        result.Value.ProductCode.Should().Be("P002");
        result.Value.PointValue.Should().Be(200);
    }

    [Fact]
    public async Task EditProduct_NotFound_ShouldFail()
    {
        var request = new AdminEditProductRequest("X", "X", 1, 1, null);
        var result = await _sut.EditProductAsync("non-existent", request, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.ProductNotFound);
    }

    [Fact]
    public async Task EditProduct_DuplicateCodeWithOtherProduct_ShouldFail()
    {
        var p1 = await SeedProduct("P001");
        await SeedProduct("P002", "Product 2");

        var request = new AdminEditProductRequest("Updated", "P002", 100, 50, null);
        var result = await _sut.EditProductAsync(p1.Id, request, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.ProductCodeAlreadyExists);
    }

    [Fact]
    public async Task EditProduct_SameCode_ShouldSucceed()
    {
        var product = await SeedProduct("P001");

        var request = new AdminEditProductRequest("Updated", "P001", 200, 100, null);
        var result = await _sut.EditProductAsync(product.Id, request, AdminId);

        result.IsSuccess.Should().BeTrue();
    }

    // ── DeleteProduct ──

    [Fact]
    public async Task DeleteProduct_NoBarcodes_ShouldSucceed()
    {
        var product = await SeedProduct();

        var result = await _sut.DeleteProductAsync(product.Id, AdminId);

        result.IsSuccess.Should().BeTrue();
        _context.Products.Any(p => p.Id == product.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteProduct_HasBarcodes_ShouldFail()
    {
        var product = await SeedProduct();
        _context.ProductBarcodes.Add(new ProductBarcode
        {
            Code = "BC001", ProductId = product.Id, Status = BarcodeStatus.Available
        });
        await _context.SaveChangesAsync();

        var result = await _sut.DeleteProductAsync(product.Id, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.ProductHasBarcodes);
    }

    [Fact]
    public async Task DeleteProduct_NotFound_ShouldFail()
    {
        var result = await _sut.DeleteProductAsync("bad-id", AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.ProductNotFound);
    }

    // ── GetProduct ──

    [Fact]
    public async Task GetProduct_Exists_ShouldReturnWithBarcodeStats()
    {
        var product = await SeedProduct();
        _context.ProductBarcodes.Add(new ProductBarcode
        {
            Code = "BC001", ProductId = product.Id, Status = BarcodeStatus.Available
        });
        _context.ProductBarcodes.Add(new ProductBarcode
        {
            Code = "BC002", ProductId = product.Id, Status = BarcodeStatus.Consumed
        });
        await _context.SaveChangesAsync();

        var result = await _sut.GetProductAsync(product.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalBarcodes.Should().Be(2);
        result.Value.AvailableBarcodes.Should().Be(1);
    }

    // ── ImportProducts ──

    [Fact]
    public async Task ImportProducts_NewAndExistingCodes_ShouldUpsert()
    {
        await SeedProduct(code: "P001", name: "Old Name", pointValue: 50);
        _importReader.Read(Arg.Any<Stream>()).Returns(new List<ProductImportRow>
        {
            new(2, "Updated Name", "P001", "Cat", "120", "30"),
            new(3, "Brand New", "P999", "Cat", "200", "75")
        });

        var result = await _sut.ImportProductsAsync(Stream.Null, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().Be(1);
        result.Value.Updated.Should().Be(1);
        result.Value.Failed.Should().Be(0);

        var updated = await _context.Products.FirstAsync(p => p.ProductCode == "P001");
        updated.Name.Should().Be("Updated Name");
        updated.PointValue.Should().Be(120);
        _context.Products.Any(p => p.ProductCode == "P999").Should().BeTrue();
    }

    [Fact]
    public async Task ImportProducts_InvalidRow_ShouldReportError()
    {
        _importReader.Read(Arg.Any<Stream>()).Returns(new List<ProductImportRow>
        {
            new(2, "Good Product", "P010", null, "100", "25"),
            new(3, "Bad Points", "P011", null, "notanumber", "25")
        });

        var result = await _sut.ImportProductsAsync(Stream.Null, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().Be(1);
        result.Value.Failed.Should().Be(1);
        result.Value.Errors.Should().ContainSingle()
            .Which.RowNumber.Should().Be(3);
    }

    [Fact]
    public async Task ImportProducts_DuplicateCodeInFile_ShouldReportSecondOccurrence()
    {
        _importReader.Read(Arg.Any<Stream>()).Returns(new List<ProductImportRow>
        {
            new(2, "First", "DUP1", null, "100", "25"),
            new(3, "Second", "DUP1", null, "100", "25")
        });

        var result = await _sut.ImportProductsAsync(Stream.Null, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().Be(1);
        result.Value.Failed.Should().Be(1);
    }

    [Fact]
    public async Task ImportProducts_EmptyFile_ShouldFail()
    {
        _importReader.Read(Arg.Any<Stream>()).Returns(new List<ProductImportRow>());

        var result = await _sut.ImportProductsAsync(Stream.Null, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.ProductImportEmptyFile);
    }

    [Fact]
    public async Task ImportProducts_UnreadableFile_ShouldFail()
    {
        _importReader.Read(Arg.Any<Stream>())
            .Returns(_ => throw new InvalidOperationException("bad file"));

        var result = await _sut.ImportProductsAsync(Stream.Null, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.ProductImportInvalidFile);
    }

    // ── ListProducts ──

    [Fact]
    public async Task ListProducts_ShouldReturnPaginated()
    {
        for (int i = 0; i < 5; i++)
            await SeedProduct($"P{i:D3}", $"Product {i}");

        var query = new AdminProductListQuery(null, null, Page: 1, PageSize: 3);
        var result = await _sut.ListProductsAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task ListProducts_SearchByName_ShouldFilter()
    {
        await SeedProduct("P001", "Apple");
        await SeedProduct("P002", "Banana");

        var query = new AdminProductListQuery("Apple", null);
        var result = await _sut.ListProductsAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.Items[0].Name.Should().Be("Apple");
    }

    [Fact]
    public async Task ListProducts_FilterByCategory_ShouldFilter()
    {
        await SeedProduct("P001", "Product A");
        var p2 = await SeedProduct("P002", "Product B");
        p2.Category = "Electronics";
        await _context.SaveChangesAsync();

        var query = new AdminProductListQuery(null, "Electronics");
        var result = await _sut.ListProductsAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
    }
}
