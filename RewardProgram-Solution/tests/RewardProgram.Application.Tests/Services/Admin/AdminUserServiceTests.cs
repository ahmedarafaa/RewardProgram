using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Contracts.Admin.Users;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Files;
using RewardProgram.Application.Services.Admin;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums.UserEnums;
using static RewardProgram.Application.Tests.TestHelpers.AsyncQueryableExtensions;

namespace RewardProgram.Application.Tests.Services.Admin;

public class AdminUserServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly IUserRepository _userRepo;
    private readonly IFileStorageService _fileStorage;
    private readonly AdminUserService _sut;

    public AdminUserServiceTests()
    {
        _context = TestDbContext.Create();
        _userRepo = Substitute.For<IUserRepository>();
        _fileStorage = Substitute.For<IFileStorageService>();
        _sut = new AdminUserService(_context, _userRepo, _fileStorage,
            new MemoryCache(new MemoryCacheOptions()),
            Substitute.For<ILogger<AdminUserService>>());
    }

    public void Dispose() => _context.Dispose();

    // ── ToggleStatus ──

    [Fact]
    public async Task ToggleStatus_UserNotFound_ShouldFail()
    {
        _userRepo.FindByIdAsync("bad", Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.ToggleStatusAsync("bad", "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.UserNotFound);
    }

    [Fact]
    public async Task ToggleStatus_SystemAdmin_ShouldFail()
    {
        var user = new ApplicationUser
        {
            Id = "admin", Name = "Admin", MobileNumber = "+966500000001",
            UserType = UserType.SystemAdmin
        };
        _userRepo.FindByIdAsync("admin", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.ToggleStatusAsync("admin", "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.UserIsSystemAdmin);
    }

    [Fact]
    public async Task ToggleStatus_EnabledUser_ShouldDisable()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "+966500000001",
            UserType = UserType.Seller, IsDisabled = false
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);
        _userRepo.UpdateAsync(user).Returns(IdentityResult.Success);

        var result = await _sut.ToggleStatusAsync("u1", "admin1");

        result.IsSuccess.Should().BeTrue();
        result.Value.IsDisabled.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleStatus_DisabledUser_ShouldEnable()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "+966500000001",
            UserType = UserType.Seller, IsDisabled = true
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);
        _userRepo.UpdateAsync(user).Returns(IdentityResult.Success);

        var result = await _sut.ToggleStatusAsync("u1", "admin1");

        result.IsSuccess.Should().BeTrue();
        result.Value.IsDisabled.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleStatus_UpdateFails_ShouldReturnError()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "+966500000001",
            UserType = UserType.Seller, IsDisabled = false
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);
        _userRepo.UpdateAsync(user).Returns(IdentityResult.Failed(new IdentityError { Description = "DB error" }));

        var result = await _sut.ToggleStatusAsync("u1", "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.UpdateUserFailed);
    }

    // ── ListUsers ──

    [Fact]
    public async Task ListUsers_ShouldExcludeSystemAdmin()
    {
        var users = new List<ApplicationUser>
        {
            new() { Id = "1", Name = "Seller", MobileNumber = "1", UserType = UserType.Seller, CreatedAt = DateTime.UtcNow },
            new() { Id = "2", Name = "Admin", MobileNumber = "2", UserType = UserType.SystemAdmin, CreatedAt = DateTime.UtcNow }
        };
        _userRepo.Query().Returns(users.AsAsyncQueryable());

        var result = await _sut.ListUsersAsync(new AdminUserListQuery(null, null, null, null, null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task ListUsers_FilterByUserType_ShouldReturnOnlyMatching()
    {
        var users = new List<ApplicationUser>
        {
            new() { Id = "1", Name = "Seller", MobileNumber = "1", UserType = UserType.Seller, CreatedAt = DateTime.UtcNow },
            new() { Id = "2", Name = "Tech", MobileNumber = "2", UserType = UserType.Technician, CreatedAt = DateTime.UtcNow }
        };
        _userRepo.Query().Returns(users.AsAsyncQueryable());

        var result = await _sut.ListUsersAsync(
            new AdminUserListQuery(null, UserType.Seller, null, null, null, null));

        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.First().UserType.Should().Be(UserType.Seller);
    }

    [Fact]
    public async Task ListUsers_SearchByName_ShouldFilter()
    {
        var users = new List<ApplicationUser>
        {
            new() { Id = "1", Name = "Ahmed Mohamed", MobileNumber = "+966500000001", UserType = UserType.Seller, CreatedAt = DateTime.UtcNow },
            new() { Id = "2", Name = "Khalid Ali", MobileNumber = "+966500000002", UserType = UserType.Seller, CreatedAt = DateTime.UtcNow }
        };
        _userRepo.Query().Returns(users.AsAsyncQueryable());

        var result = await _sut.ListUsersAsync(
            new AdminUserListQuery("Ahmed", null, null, null, null, null));

        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.First().Name.Should().Be("Ahmed Mohamed");
    }

    [Fact]
    public async Task ListUsers_FilterByDisabled_ShouldReturnOnlyMatching()
    {
        var users = new List<ApplicationUser>
        {
            new() { Id = "1", Name = "Active", MobileNumber = "1", UserType = UserType.Seller, IsDisabled = false, CreatedAt = DateTime.UtcNow },
            new() { Id = "2", Name = "Disabled", MobileNumber = "2", UserType = UserType.Seller, IsDisabled = true, CreatedAt = DateTime.UtcNow }
        };
        _userRepo.Query().Returns(users.AsAsyncQueryable());

        var result = await _sut.ListUsersAsync(
            new AdminUserListQuery(null, null, null, null, true, null));

        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.First().IsDisabled.Should().BeTrue();
    }

    [Fact]
    public async Task ListUsers_ShouldPaginate()
    {
        var users = Enumerable.Range(1, 10).Select(i => new ApplicationUser
        {
            Id = $"u{i}", Name = $"User{i}", MobileNumber = $"+96650000000{i}",
            UserType = UserType.Seller, CreatedAt = DateTime.UtcNow.AddMinutes(-i)
        }).ToList();
        _userRepo.Query().Returns(users.AsAsyncQueryable());

        var result = await _sut.ListUsersAsync(
            new AdminUserListQuery(null, null, null, null, null, null, Page: 2, PageSize: 3));

        result.Value.Items.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(10);
    }

    // ── AddSalesMan ──

    [Fact]
    public async Task AddSalesMan_DuplicateMobile_ShouldFail()
    {
        _userRepo.MobileExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.AddSalesManAsync(
            new AdminAddSalesManRequest("Test", "0500000001", ["city1"]), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.MobileAlreadyExists);
    }

    [Fact]
    public async Task AddSalesMan_InvalidCities_ShouldFail()
    {
        _userRepo.MobileExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.AddSalesManAsync(
            new AdminAddSalesManRequest("Test", "0500000001", ["nonexistent"]), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.SomeCitiesNotFound);
    }

    // ── AddZoneManager ──

    [Fact]
    public async Task AddZoneManager_DuplicateMobile_ShouldFail()
    {
        _userRepo.MobileExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.AddZoneManagerAsync(
            new AdminAddZoneManagerRequest("Test", "0500000001", "region1"), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.MobileAlreadyExists);
    }

    [Fact]
    public async Task AddZoneManager_RegionNotFound_ShouldFail()
    {
        _userRepo.MobileExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.AddZoneManagerAsync(
            new AdminAddZoneManagerRequest("Test", "0500000001", "bad-region"), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.RegionNotFound);
    }

    [Fact]
    public async Task AddZoneManager_RegionAlreadyHasManager_ShouldFail()
    {
        _userRepo.MobileExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var region = new Region
        {
            NameAr = "الرياض", NameEn = "Riyadh", IsActive = true,
            ZoneManagerId = "existing-zm"
        };
        _context.Regions.Add(region);
        await _context.SaveChangesAsync();

        var result = await _sut.AddZoneManagerAsync(
            new AdminAddZoneManagerRequest("Test", "0500000001", region.Id), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.RegionAlreadyHasZoneManager);
    }

    // ── AddTechnician ──

    [Fact]
    public async Task AddTechnician_DuplicateMobile_ShouldFail()
    {
        _userRepo.MobileExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.AddTechnicianAsync(
            new AdminAddTechnicianRequest("Test", "0500000001", "city1", "12345", "Olaya"), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.MobileAlreadyExists);
    }

    [Fact]
    public async Task AddTechnician_CityNotFound_ShouldFail()
    {
        _userRepo.MobileExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.AddTechnicianAsync(
            new AdminAddTechnicianRequest("Test", "0500000001", "bad-city", "12345", "Olaya"), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.CityNotFound);
    }

    [Fact]
    public async Task AddTechnician_CityNoSalesMan_ShouldFail()
    {
        _userRepo.MobileExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var region = new Region { NameAr = "R", NameEn = "R", IsActive = true };
        _context.Regions.Add(region);
        await _context.SaveChangesAsync();

        var city = new City
        {
            NameAr = "C", NameEn = "C", RegionId = region.Id, IsActive = true,
            ApprovalSalesManId = null // no salesman
        };
        _context.Cities.Add(city);
        await _context.SaveChangesAsync();

        var result = await _sut.AddTechnicianAsync(
            new AdminAddTechnicianRequest("Test", "0500000001", city.Id, "12345", "Olaya"), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.NoApprovalSalesMan);
    }
}
