using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Contracts.Admin.ErpCustomers;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Services.Admin;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Application.Tests.Services.Admin;

public class AdminErpCustomerServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly IErpCustomerImportReader _importReader;
    private readonly AdminErpCustomerService _sut;
    private const string AdminId = "admin-1";

    public AdminErpCustomerServiceTests()
    {
        _context = TestDbContext.Create();
        _importReader = Substitute.For<IErpCustomerImportReader>();
        _sut = new AdminErpCustomerService(
            _context,
            _importReader,
            new StubLocalizer<ErrorMessages>(),
            Substitute.For<ILogger<AdminErpCustomerService>>());
    }

    public void Dispose() => _context.Dispose();

    private async Task<ErpCustomer> SeedCustomer(string code = "C001", string name = "Customer 1")
    {
        var customer = new ErpCustomer { CustomerCode = code, CustomerName = name };
        _context.ErpCustomers.Add(customer);
        await _context.SaveChangesAsync();
        return customer;
    }

    // ── Add ──

    [Fact]
    public async Task AddErpCustomer_Valid_ShouldSucceed()
    {
        var request = new AdminAddErpCustomerRequest("NEW-001", "New Customer");

        var result = await _sut.AddErpCustomerAsync(request, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerCode.Should().Be("NEW-001");
        result.Value.CustomerName.Should().Be("New Customer");
        result.Value.ShortAddress.Should().BeNull(); // optional, not supplied
        result.Value.HasShopData.Should().BeFalse();
        result.Value.LinkedUserCount.Should().Be(0);
    }

    [Fact]
    public async Task AddErpCustomer_WithShortAddress_ShouldSucceed()
    {
        var request = new AdminAddErpCustomerRequest("NEW-002", "Addr Customer", "RRRA2929");

        var result = await _sut.AddErpCustomerAsync(request, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShortAddress.Should().Be("RRRA2929");
    }

    [Fact]
    public async Task AddErpCustomer_BlankShortAddress_ShouldStoreNull()
    {
        var request = new AdminAddErpCustomerRequest("NEW-003", "Blank Addr", "   ");

        var result = await _sut.AddErpCustomerAsync(request, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShortAddress.Should().BeNull();
    }

    [Fact]
    public async Task AddErpCustomer_DuplicateShortAddress_ShouldFail()
    {
        _context.ErpCustomers.Add(new ErpCustomer
        {
            CustomerCode = "C001", CustomerName = "First", ShortAddress = "RRRA2929"
        });
        await _context.SaveChangesAsync();

        var request = new AdminAddErpCustomerRequest("C002", "Second", "RRRA2929");
        var result = await _sut.AddErpCustomerAsync(request, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErpCustomerErrors.ShortAddressAlreadyExists);
    }

    [Fact]
    public async Task AddErpCustomer_DuplicateCode_ShouldFail()
    {
        await SeedCustomer("C001");

        var request = new AdminAddErpCustomerRequest("C001", "Another");
        var result = await _sut.AddErpCustomerAsync(request, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErpCustomerErrors.CustomerCodeAlreadyExists);
    }

    [Fact]
    public async Task AddErpCustomer_SoftDeletedCode_ShouldReviveSameRecord()
    {
        var customer = await SeedCustomer("C500", "Deleted Customer");
        customer.IsDeleted = true;
        await _context.SaveChangesAsync();

        var request = new AdminAddErpCustomerRequest("C500", "Revived Customer");
        var result = await _sut.AddErpCustomerAsync(request, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(customer.Id);
        result.Value.CustomerName.Should().Be("Revived Customer");
        _context.ErpCustomers.Count(e => e.CustomerCode == "C500").Should().Be(1);
    }

    // ── Edit ──

    [Fact]
    public async Task EditErpCustomer_Valid_ShouldUpdateName()
    {
        var customer = await SeedCustomer("C001", "Old Name");

        var request = new AdminEditErpCustomerRequest("Updated Name");
        var result = await _sut.EditErpCustomerAsync(customer.Id, request, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerName.Should().Be("Updated Name");
        result.Value.CustomerCode.Should().Be("C001"); // unchanged
    }

    [Fact]
    public async Task EditErpCustomer_NotFound_ShouldFail()
    {
        var request = new AdminEditErpCustomerRequest("X");
        var result = await _sut.EditErpCustomerAsync("non-existent", request, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErpCustomerErrors.ErpCustomerNotFound);
    }

    [Fact]
    public async Task EditErpCustomer_SetShortAddress_ShouldSucceed()
    {
        var customer = await SeedCustomer("C001");

        var request = new AdminEditErpCustomerRequest("Updated", "ABCD1234");
        var result = await _sut.EditErpCustomerAsync(customer.Id, request, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShortAddress.Should().Be("ABCD1234");
    }

    [Fact]
    public async Task EditErpCustomer_KeepingOwnShortAddress_ShouldSucceed()
    {
        var customer = new ErpCustomer
        {
            CustomerCode = "C001", CustomerName = "X", ShortAddress = "ABCD1234"
        };
        _context.ErpCustomers.Add(customer);
        await _context.SaveChangesAsync();

        var request = new AdminEditErpCustomerRequest("X Updated", "ABCD1234");
        var result = await _sut.EditErpCustomerAsync(customer.Id, request, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShortAddress.Should().Be("ABCD1234");
    }

    [Fact]
    public async Task EditErpCustomer_DuplicateShortAddress_ShouldFail()
    {
        _context.ErpCustomers.Add(new ErpCustomer
        {
            CustomerCode = "C001", CustomerName = "First", ShortAddress = "RRRA2929"
        });
        await _context.SaveChangesAsync();
        var second = await SeedCustomer("C002", "Second");

        var request = new AdminEditErpCustomerRequest("Second", "RRRA2929");
        var result = await _sut.EditErpCustomerAsync(second.Id, request, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErpCustomerErrors.ShortAddressAlreadyExists);
    }

    [Fact]
    public async Task EditErpCustomer_ClearShortAddress_ShouldSetNull()
    {
        var customer = new ErpCustomer
        {
            CustomerCode = "C001", CustomerName = "X", ShortAddress = "ABCD1234"
        };
        _context.ErpCustomers.Add(customer);
        await _context.SaveChangesAsync();

        var request = new AdminEditErpCustomerRequest("X", null);
        var result = await _sut.EditErpCustomerAsync(customer.Id, request, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.ShortAddress.Should().BeNull();
    }

    // ── Delete ──

    [Fact]
    public async Task DeleteErpCustomer_NoDependents_ShouldSucceed()
    {
        var customer = await SeedCustomer();

        var result = await _sut.DeleteErpCustomerAsync(customer.Id, AdminId);

        result.IsSuccess.Should().BeTrue();
        _context.ErpCustomers.Any(e => e.Id == customer.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteErpCustomer_HasShopData_ShouldFail()
    {
        var customer = await SeedCustomer("C001");
        _context.ShopData.Add(new ShopData { CustomerCode = "C001" });
        await _context.SaveChangesAsync();

        var result = await _sut.DeleteErpCustomerAsync(customer.Id, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErpCustomerErrors.ErpCustomerInUse);
    }

    [Fact]
    public async Task DeleteErpCustomer_HasShopOwner_ShouldFail()
    {
        var customer = await SeedCustomer("C001");
        _context.ShopOwnerProfiles.Add(new ShopOwnerProfile { UserId = "u1", CustomerCode = "C001" });
        await _context.SaveChangesAsync();

        var result = await _sut.DeleteErpCustomerAsync(customer.Id, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErpCustomerErrors.ErpCustomerInUse);
    }

    [Fact]
    public async Task DeleteErpCustomer_NotFound_ShouldFail()
    {
        var result = await _sut.DeleteErpCustomerAsync("bad-id", AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErpCustomerErrors.ErpCustomerNotFound);
    }

    // ── Get ──

    [Fact]
    public async Task GetErpCustomer_Exists_ShouldReturnWithDependentStats()
    {
        var customer = await SeedCustomer("C001");
        _context.ShopData.Add(new ShopData { CustomerCode = "C001" });
        _context.ShopOwnerProfiles.Add(new ShopOwnerProfile { UserId = "u1", CustomerCode = "C001" });
        _context.SellerProfiles.Add(new SellerProfile { UserId = "u2", CustomerCode = "C001" });
        await _context.SaveChangesAsync();

        var result = await _sut.GetErpCustomerAsync(customer.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasShopData.Should().BeTrue();
        result.Value.LinkedUserCount.Should().Be(2);
    }

    // ── List ──

    [Fact]
    public async Task ListErpCustomers_ShouldReturnPaginated()
    {
        for (int i = 0; i < 5; i++)
            await SeedCustomer($"C{i:D3}", $"Customer {i}");

        var query = new AdminErpCustomerListQuery(null, Page: 1, PageSize: 3);
        var result = await _sut.ListErpCustomersAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task ListErpCustomers_SearchByCode_ShouldFilter()
    {
        await SeedCustomer("ALPHA-1", "Apple");
        await SeedCustomer("BETA-2", "Banana");

        var query = new AdminErpCustomerListQuery("ALPHA");
        var result = await _sut.ListErpCustomersAsync(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle()
            .Which.CustomerCode.Should().Be("ALPHA-1");
    }

    // ── Import ──

    [Fact]
    public async Task ImportErpCustomers_NewAndExistingCodes_ShouldUpsert()
    {
        await SeedCustomer("C001", "Old Name");
        _importReader.Read(Arg.Any<Stream>(), Arg.Any<int>()).Returns(new List<ErpCustomerImportRow>
        {
            new(2, "C001", "Updated Name"),
            new(3, "C999", "Brand New")
        });

        var result = await _sut.ImportErpCustomersAsync(Stream.Null, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().Be(1);
        result.Value.Updated.Should().Be(1);
        result.Value.Failed.Should().Be(0);

        var updated = await _context.ErpCustomers.FirstAsync(e => e.CustomerCode == "C001");
        updated.CustomerName.Should().Be("Updated Name");
        _context.ErpCustomers.Any(e => e.CustomerCode == "C999").Should().BeTrue();
    }

    [Fact]
    public async Task ImportErpCustomers_SoftDeletedCode_ShouldRevive()
    {
        var customer = await SeedCustomer("C700", "Deleted");
        customer.IsDeleted = true;
        await _context.SaveChangesAsync();

        _importReader.Read(Arg.Any<Stream>(), Arg.Any<int>()).Returns(new List<ErpCustomerImportRow>
        {
            new(2, "C700", "Revived")
        });

        var result = await _sut.ImportErpCustomersAsync(Stream.Null, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().Be(0);
        result.Value.Updated.Should().Be(1);

        var revived = await _context.ErpCustomers.FirstAsync(e => e.CustomerCode == "C700");
        revived.Id.Should().Be(customer.Id);
        revived.CustomerName.Should().Be("Revived");
    }

    [Fact]
    public async Task ImportErpCustomers_InvalidRow_ShouldReportError()
    {
        _importReader.Read(Arg.Any<Stream>(), Arg.Any<int>()).Returns(new List<ErpCustomerImportRow>
        {
            new(2, "C010", "Good Customer"),
            new(3, "", "Missing Code")
        });

        var result = await _sut.ImportErpCustomersAsync(Stream.Null, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().Be(1);
        result.Value.Failed.Should().Be(1);
        result.Value.Errors.Should().ContainSingle()
            .Which.RowNumber.Should().Be(3);
    }

    [Fact]
    public async Task ImportErpCustomers_DuplicateCodeInFile_ShouldReportSecondOccurrence()
    {
        _importReader.Read(Arg.Any<Stream>(), Arg.Any<int>()).Returns(new List<ErpCustomerImportRow>
        {
            new(2, "DUP1", "First"),
            new(3, "DUP1", "Second")
        });

        var result = await _sut.ImportErpCustomersAsync(Stream.Null, AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Created.Should().Be(1);
        result.Value.Failed.Should().Be(1);
    }

    [Fact]
    public async Task ImportErpCustomers_EmptyFile_ShouldFail()
    {
        _importReader.Read(Arg.Any<Stream>(), Arg.Any<int>()).Returns(new List<ErpCustomerImportRow>());

        var result = await _sut.ImportErpCustomersAsync(Stream.Null, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErpCustomerErrors.ImportEmptyFile);
    }

    [Fact]
    public async Task ImportErpCustomers_UnreadableFile_ShouldFail()
    {
        _importReader.Read(Arg.Any<Stream>(), Arg.Any<int>())
            .Returns(_ => throw new InvalidOperationException("bad file"));

        var result = await _sut.ImportErpCustomersAsync(Stream.Null, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErpCustomerErrors.ImportInvalidFile);
    }

    [Fact]
    public async Task ImportErpCustomers_UnrecognizedColumns_ShouldFail()
    {
        _importReader.Read(Arg.Any<Stream>(), Arg.Any<int>())
            .Returns(_ => throw new ErpCustomerImportHeaderException(["Customer Code / كود العميل"]));

        var result = await _sut.ImportErpCustomersAsync(Stream.Null, AdminId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminErpCustomerErrors.ImportMissingColumns);
    }
}
