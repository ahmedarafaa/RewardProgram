using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Contracts.Admin.Users;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Files;
using RewardProgram.Application.Services.Admin;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Constants;
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
            Substitute.For<ILogger<AdminUserService>>(),
            new StubLocalizer<ErrorMessages>());
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
        _userRepo.FindByMobileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationUser { Id = "existing", Name = "Existing", MobileNumber = "+966500000099" });

        var result = await _sut.AddSalesManAsync(
            new AdminAddSalesManRequest("Test", "0500000001", ["city1"]), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.MobileAlreadyExists);
    }

    [Fact]
    public async Task AddSalesMan_InvalidCities_ShouldFail()
    {
        _userRepo.FindByMobileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.AddSalesManAsync(
            new AdminAddSalesManRequest("Test", "0500000001", ["nonexistent"]), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.SomeCitiesNotFound);
    }

    // ── AddZoneManager ──

    [Fact]
    public async Task AddZoneManager_DuplicateMobile_ShouldFail()
    {
        _userRepo.FindByMobileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ApplicationUser { Id = "existing", Name = "Existing", MobileNumber = "+966500000099" });

        var result = await _sut.AddZoneManagerAsync(
            new AdminAddZoneManagerRequest("Test", "0500000001", "region1"), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.MobileAlreadyExists);
    }

    [Fact]
    public async Task AddZoneManager_RegionNotFound_ShouldFail()
    {
        _userRepo.FindByMobileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.AddZoneManagerAsync(
            new AdminAddZoneManagerRequest("Test", "0500000001", "bad-region"), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.RegionNotFound);
    }

    [Fact]
    public async Task AddZoneManager_RegionAlreadyHasManager_ShouldFail()
    {
        _userRepo.FindByMobileAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

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

    // ── RestoreUser ──

    [Fact]
    public async Task RestoreUser_UserNotFound_ShouldFail()
    {
        _userRepo.FindByIdAsync("bad", Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.RestoreUserAsync("bad", "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.UserNotFound);
    }

    [Fact]
    public async Task RestoreUser_SystemAdmin_ShouldFail()
    {
        var user = new ApplicationUser
        {
            Id = "admin", Name = "Admin", MobileNumber = "+966500000001",
            UserType = UserType.SystemAdmin, IsAccountDeleted = true
        };
        _userRepo.FindByIdAsync("admin", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.RestoreUserAsync("admin", "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.UserIsSystemAdmin);
    }

    [Fact]
    public async Task RestoreUser_NotDeleted_ShouldFail()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "+966500000001",
            UserType = UserType.SalesMan, IsAccountDeleted = false
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.RestoreUserAsync("u1", "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.AccountNotDeleted);
    }

    [Fact]
    public async Task RestoreUser_Valid_ShouldClearDeletionAndStampRestoreFields()
    {
        var deletedAt = DateTime.UtcNow.AddDays(-3);
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "+966500000001",
            UserType = UserType.SalesMan,
            IsDisabled = true,
            IsAccountDeleted = true,
            AccountDeletedAt = deletedAt,
            DeletedByAdminId = "previous-admin"
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);
        _userRepo.UpdateAsync(user).Returns(IdentityResult.Success);

        var result = await _sut.RestoreUserAsync("u1", "admin1");

        result.IsSuccess.Should().BeTrue();
        user.IsAccountDeleted.Should().BeFalse();
        user.AccountDeletedAt.Should().BeNull();
        user.DeletedByAdminId.Should().BeNull();
        user.IsDisabled.Should().BeFalse();
        user.RestoredByAdminId.Should().Be("admin1");
        user.RestoredAt.Should().NotBeNull();
        user.RestoredAt!.Value.Should().BeAfter(deletedAt);
    }

    [Fact]
    public async Task RestoreUser_ArchivedRejectedRegistration_ShouldFail()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "DEL_638000000000000000_+966500000001",
            UserType = UserType.Seller, RegistrationStatus = RegistrationStatus.Rejected,
            IsDisabled = true, IsAccountDeleted = true
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.RestoreUserAsync("u1", "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.CannotRestoreArchivedRegistration);
        user.IsAccountDeleted.Should().BeTrue();
    }

    // ── Territory / inactive-account guards ──

    private ApplicationUser SalesMan(string id, bool disabled = false, bool deleted = false) => new()
    {
        Id = id, Name = "SM " + id, MobileNumber = "+96650000000" + id.Length,
        UserType = UserType.SalesMan, IsDisabled = disabled, IsAccountDeleted = deleted
    };

    private async Task<City> SeedCityAsync(string? ownerId)
    {
        var city = new City { NameAr = "مدينة", NameEn = "City", RegionId = "r1", IsActive = true, ApprovalSalesManId = ownerId };
        _context.Cities.Add(city);
        await _context.SaveChangesAsync();
        return city;
    }

    [Fact]
    public async Task ToggleStatus_DeletedUser_ShouldFail()
    {
        var user = SalesMan("sm1", disabled: true, deleted: true);
        _userRepo.FindByIdAsync("sm1", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.ToggleStatusAsync("sm1", "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.CannotToggleDeletedUser);
        user.IsDisabled.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleStatus_DisableSalesManOwningCities_ShouldFail()
    {
        var user = SalesMan("sm1");
        _userRepo.FindByIdAsync("sm1", Arg.Any<CancellationToken>()).Returns(user);
        await SeedCityAsync("sm1");

        var result = await _sut.ToggleStatusAsync("sm1", "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.ReassignTerritoryBeforeDisable);
        user.IsDisabled.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleStatus_DisableZoneManagerOwningRegion_ShouldFail()
    {
        var user = new ApplicationUser
        {
            Id = "zm1", Name = "ZM", MobileNumber = "+966500000009", UserType = UserType.ZoneManager
        };
        _userRepo.FindByIdAsync("zm1", Arg.Any<CancellationToken>()).Returns(user);
        _context.Regions.Add(new Region { NameAr = "الرياض", NameEn = "Riyadh", IsActive = true, ZoneManagerId = "zm1" });
        await _context.SaveChangesAsync();

        var result = await _sut.ToggleStatusAsync("zm1", "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.ReassignTerritoryBeforeDisable);
    }

    [Fact]
    public async Task ToggleStatus_EnableDisabledSalesManOwningCities_ShouldSucceed()
    {
        // Re-enabling is always allowed — only the disable direction strands territory.
        var user = SalesMan("sm1", disabled: true);
        _userRepo.FindByIdAsync("sm1", Arg.Any<CancellationToken>()).Returns(user);
        _userRepo.UpdateAsync(user).Returns(IdentityResult.Success);
        await SeedCityAsync("sm1");

        var result = await _sut.ToggleStatusAsync("sm1", "admin1");

        result.IsSuccess.Should().BeTrue();
        result.Value.IsDisabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ReassignCities_ToInactiveSalesMan_ShouldFail(bool disabled, bool deleted)
    {
        _userRepo.FindByIdAsync("sm2", Arg.Any<CancellationToken>()).Returns(SalesMan("sm2", disabled, deleted));
        var city = await SeedCityAsync("sm1");

        var result = await _sut.ReassignCitiesAsync(new AdminReassignCitiesRequest([city.Id], "sm2"), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.ReassignmentTargetInactive);
        (await _context.Cities.FindAsync(city.Id))!.ApprovalSalesManId.Should().Be("sm1");
    }

    [Fact]
    public async Task ReassignCities_DuplicateCityIds_ShouldNotFailAsNotFound()
    {
        _userRepo.FindByIdAsync("sm2", Arg.Any<CancellationToken>()).Returns(SalesMan("sm2"));
        _userRepo.Query().Returns(new List<ApplicationUser>().AsAsyncQueryable());
        var city = await SeedCityAsync("sm1");

        var result = await _sut.ReassignCitiesAsync(new AdminReassignCitiesRequest([city.Id, city.Id], "sm2"), "admin1");

        result.Error.Should().NotBe(AdminUserErrors.SomeCitiesNotFound);
    }

    [Fact]
    public async Task ReassignRegion_ToDeletedZoneManager_ShouldFail()
    {
        _userRepo.FindByIdAsync("zm2", Arg.Any<CancellationToken>()).Returns(new ApplicationUser
        {
            Id = "zm2", Name = "ZM", MobileNumber = "+966500000008",
            UserType = UserType.ZoneManager, IsAccountDeleted = true, IsDisabled = true
        });
        var region = new Region { NameAr = "الرياض", NameEn = "Riyadh", IsActive = true, ZoneManagerId = "zm1" };
        _context.Regions.Add(region);
        await _context.SaveChangesAsync();

        var result = await _sut.ReassignRegionAsync(new AdminReassignRegionRequest(region.Id, "zm2"), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.ReassignmentTargetInactive);
    }

    [Fact]
    public async Task DeleteSalesMan_HandingCityToDisabledSalesMan_ShouldFail()
    {
        _userRepo.FindByIdAsync("sm1", Arg.Any<CancellationToken>()).Returns(SalesMan("sm1"));
        _userRepo.Query().Returns(new List<ApplicationUser> { SalesMan("sm2", disabled: true) }.AsAsyncQueryable());
        var city = await SeedCityAsync("sm1");

        var result = await _sut.DeleteSalesManAsync("sm1",
            new AdminDeleteSalesManRequest([new AdminCityReassignment(city.Id, "sm2")]), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.ReassignmentTargetInactive);
    }

    [Fact]
    public async Task DeleteSalesMan_DuplicateCityInReassignments_ShouldFailCleanly()
    {
        _userRepo.FindByIdAsync("sm1", Arg.Any<CancellationToken>()).Returns(SalesMan("sm1"));
        var city = await SeedCityAsync("sm1");

        var result = await _sut.DeleteSalesManAsync("sm1",
            new AdminDeleteSalesManRequest(
            [
                new AdminCityReassignment(city.Id, "sm2"),
                new AdminCityReassignment(city.Id, "sm3")
            ]), "admin1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminUserErrors.DuplicateCityReassignment);
    }

    [Fact]
    public async Task ListUsers_SearchByLocalMobileFormat_ShouldMatchStoredInternationalForm()
    {
        var users = new List<ApplicationUser>
        {
            new() { Id = "1", Name = "Ali", MobileNumber = "+966597261921", UserType = UserType.SalesMan, CreatedAt = DateTime.UtcNow },
            new() { Id = "2", Name = "Omar", MobileNumber = "+966511111111", UserType = UserType.SalesMan, CreatedAt = DateTime.UtcNow }
        };
        _userRepo.Query().Returns(users.AsAsyncQueryable());

        var result = await _sut.ListUsersAsync(new AdminUserListQuery("0597261921", null, null, null, null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(u => u.Id).Should().Equal("1");
    }
    // ── Staff roles (SM ⇄ ZM ⇄ dual) ──

    private ApplicationUser Staff(string id, UserType type, params string[] roles)
    {
        var user = new ApplicationUser
        {
            Id = id, Name = "Staff " + id, MobileNumber = "+96650000" + id.GetHashCode().ToString("D5")[^5..],
            UserType = type, RegistrationStatus = RegistrationStatus.Approved
        };
        _userRepo.FindByIdAsync(id, Arg.Any<CancellationToken>()).Returns(user);
        _userRepo.GetRolesAsync(user).Returns(roles.ToList());
        return user;
    }

    private void IdentityWritesSucceed()
    {
        _userRepo.UpdateAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Success);
        _userRepo.UpdateSecurityStampAsync(Arg.Any<ApplicationUser>()).Returns(IdentityResult.Success);
        _userRepo.AddToRoleAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>()).Returns(IdentityResult.Success);
        _userRepo.RemoveFromRolesAsync(Arg.Any<ApplicationUser>(), Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);
    }

    private async Task<Region> SeedRegionAsync(string? zmId, string? id = null)
    {
        var region = new Region { NameAr = "منطقة", NameEn = "Region", IsActive = true, ZoneManagerId = zmId };
        if (id is not null) region.Id = id;
        _context.Regions.Add(region);
        await _context.SaveChangesAsync();
        return region;
    }

    private static AdminSetStaffRolesRequest Roles(
        string[] roles,
        List<AdminCityReassignment>? cityReassignments = null,
        string? newZoneManagerId = null,
        string? regionId = null,
        List<string>? cityIds = null) =>
        new(roles.ToList(), cityReassignments, newZoneManagerId, regionId, cityIds);

    [Fact]
    public async Task SetStaffRoles_UserNotFound_ShouldFail()
    {
        var result = await _sut.SetStaffRolesAsync("missing", Roles([UserRoles.ZoneManager]), "admin1");

        result.Error.Should().Be(AdminUserErrors.UserNotFound);
    }

    [Fact]
    public async Task SetStaffRoles_NonStaffUser_ShouldFail()
    {
        Staff("so1", UserType.ShopOwner, UserRoles.ShopOwner);

        var result = await _sut.SetStaffRolesAsync("so1", Roles([UserRoles.SalesMan]), "admin1");

        result.Error.Should().Be(AdminUserErrors.UserNotStaff);
    }

    [Fact]
    public async Task SetStaffRoles_DisabledUser_ShouldFail()
    {
        var sm = Staff("sm1", UserType.SalesMan, UserRoles.SalesMan);
        sm.IsDisabled = true;

        var result = await _sut.SetStaffRolesAsync("sm1", Roles([UserRoles.ZoneManager]), "admin1");

        result.Error.Should().Be(AdminUserErrors.CannotChangeRolesOfInactiveUser);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("")]
    public async Task SetStaffRoles_InvalidRole_ShouldFail(string role)
    {
        Staff("sm1", UserType.SalesMan, UserRoles.SalesMan);

        var result = await _sut.SetStaffRolesAsync("sm1", Roles([role]), "admin1");

        result.Error.Should().Be(AdminUserErrors.InvalidStaffRole);
    }

    [Fact]
    public async Task SetStaffRoles_EmptySet_ShouldFail()
    {
        Staff("sm1", UserType.SalesMan, UserRoles.SalesMan);

        var result = await _sut.SetStaffRolesAsync("sm1", Roles([]), "admin1");

        result.Error.Should().Be(AdminUserErrors.InvalidStaffRole);
    }

    [Fact]
    public async Task SetStaffRoles_SameSet_ShouldBeNoOp()
    {
        var sm = Staff("sm1", UserType.SalesMan, UserRoles.SalesMan);
        await SeedCityAsync("sm1");

        var result = await _sut.SetStaffRolesAsync("sm1", Roles(["salesman"]), "admin1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Roles.Should().Equal(UserRoles.SalesMan);
        result.Value.IsDualRole.Should().BeFalse();
        sm.UserType.Should().Be(UserType.SalesMan);
        await _userRepo.DidNotReceive().UpdateAsync(Arg.Any<ApplicationUser>());
        await _userRepo.DidNotReceive().RevokeAllRefreshTokensAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetStaffRoles_SmToZm_OwningCitiesWithoutHandoff_ShouldFail()
    {
        Staff("sm1", UserType.SalesMan, UserRoles.SalesMan);
        var city = await SeedCityAsync("sm1");

        var result = await _sut.SetStaffRolesAsync("sm1", Roles([UserRoles.ZoneManager]), "admin1");

        result.Error.Should().Be(AdminUserErrors.AllCitiesMustBeReassigned);
        (await _context.Cities.FindAsync(city.Id))!.ApprovalSalesManId.Should().Be("sm1");
    }

    [Fact]
    public async Task SetStaffRoles_SmToZm_HandoffToNonSalesMan_ShouldFail()
    {
        Staff("sm1", UserType.SalesMan, UserRoles.SalesMan);
        var zmTarget = Staff("zm9", UserType.ZoneManager, UserRoles.ZoneManager);
        _userRepo.Query().Returns(new List<ApplicationUser> { zmTarget }.AsAsyncQueryable());
        var city = await SeedCityAsync("sm1");

        var result = await _sut.SetStaffRolesAsync("sm1",
            Roles([UserRoles.ZoneManager], [new AdminCityReassignment(city.Id, "zm9")]), "admin1");

        result.Error.Should().Be(AdminUserErrors.ReassignmentTargetNotSalesMan);
    }

    [Fact]
    public async Task SetStaffRoles_SmToZm_CityNotOwned_ShouldFail()
    {
        Staff("sm1", UserType.SalesMan, UserRoles.SalesMan);
        var owned = await SeedCityAsync("sm1");
        var foreign = await SeedCityAsync("sm7");

        var result = await _sut.SetStaffRolesAsync("sm1",
            Roles([UserRoles.ZoneManager],
            [
                new AdminCityReassignment(owned.Id, "sm2"),
                new AdminCityReassignment(foreign.Id, "sm2")
            ]), "admin1");

        result.Error.Should().Be(AdminUserErrors.CityNotOwnedBySalesMan);
    }

    [Fact]
    public async Task SetStaffRoles_SmToZm_WithHandoffAndRegion_ShouldConvertInPlace()
    {
        IdentityWritesSucceed();
        var sm = Staff("sm1", UserType.SalesMan, UserRoles.SalesMan);
        var sm2 = Staff("sm2", UserType.SalesMan, UserRoles.SalesMan);
        var pendingShop = new ApplicationUser
        {
            Id = "shop1", Name = "Shop", MobileNumber = "+966500009999", UserType = UserType.ShopOwner,
            RegistrationStatus = RegistrationStatus.PendingSalesman, AssignedSalesManId = "sm1"
        };
        var city = await SeedCityAsync("sm1");
        pendingShop.NationalAddress = new NationalAddress { CityId = city.Id, Street = "s", BuildingNumber = 1, PostalCode = "12345", SubNumber = 1, District = "d" };
        _userRepo.Query().Returns(new List<ApplicationUser> { sm2, pendingShop }.AsAsyncQueryable());
        var region = await SeedRegionAsync(null);

        var result = await _sut.SetStaffRolesAsync("sm1",
            Roles([UserRoles.ZoneManager],
                cityReassignments: [new AdminCityReassignment(city.Id, "sm2")],
                regionId: region.Id), "admin1");

        result.IsSuccess.Should().BeTrue();
        result.Value.UserType.Should().Be(UserType.ZoneManager);
        result.Value.Roles.Should().Equal(UserRoles.ZoneManager);
        result.Value.IsDualRole.Should().BeFalse();
        sm.UserType.Should().Be(UserType.ZoneManager);

        (await _context.Cities.FindAsync(city.Id))!.ApprovalSalesManId.Should().Be("sm2");
        pendingShop.AssignedSalesManId.Should().Be("sm2");
        (await _context.Regions.FindAsync(region.Id))!.ZoneManagerId.Should().Be("sm1");

        await _userRepo.Received(1).RemoveFromRolesAsync(sm, Arg.Is<IEnumerable<string>>(r => r.Single() == UserRoles.SalesMan));
        await _userRepo.Received(1).AddToRoleAsync(sm, UserRoles.ZoneManager);
        await _userRepo.Received(1).UpdateSecurityStampAsync(sm);
        await _userRepo.Received(1).RevokeAllRefreshTokensAsync("sm1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetStaffRoles_SmToZm_RegionAlreadyManaged_ShouldFail()
    {
        Staff("sm1", UserType.SalesMan, UserRoles.SalesMan);
        var region = await SeedRegionAsync("zm-other");

        var result = await _sut.SetStaffRolesAsync("sm1",
            Roles([UserRoles.ZoneManager], regionId: region.Id), "admin1");

        result.Error.Should().Be(AdminUserErrors.RegionAlreadyHasZoneManager);
    }

    [Fact]
    public async Task SetStaffRoles_ZmToSm_ManagingRegionWithoutReplacement_ShouldFail()
    {
        Staff("zm1", UserType.ZoneManager, UserRoles.ZoneManager);
        await SeedRegionAsync("zm1");

        var result = await _sut.SetStaffRolesAsync("zm1", Roles([UserRoles.SalesMan]), "admin1");

        result.Error.Should().Be(AdminUserErrors.ReplacementZoneManagerRequired);
    }

    [Fact]
    public async Task SetStaffRoles_ZmToSm_ReplacementManagesAnotherRegion_ShouldFail()
    {
        Staff("zm1", UserType.ZoneManager, UserRoles.ZoneManager);
        Staff("zm2", UserType.ZoneManager, UserRoles.ZoneManager);
        await SeedRegionAsync("zm1");
        await SeedRegionAsync("zm2");

        var result = await _sut.SetStaffRolesAsync("zm1",
            Roles([UserRoles.SalesMan], newZoneManagerId: "zm2"), "admin1");

        result.Error.Should().Be(AdminUserErrors.ZoneManagerAlreadyAssigned);
    }

    [Fact]
    public async Task SetStaffRoles_ZmToDual_ShouldKeepRegionAndAddCities()
    {
        IdentityWritesSucceed();
        var zm = Staff("zm1", UserType.ZoneManager, UserRoles.ZoneManager);
        _userRepo.Query().Returns(new List<ApplicationUser>().AsAsyncQueryable());
        var region = await SeedRegionAsync("zm1");
        var city = await SeedCityAsync(null);

        var result = await _sut.SetStaffRolesAsync("zm1",
            Roles([UserRoles.ZoneManager, UserRoles.SalesMan], cityIds: [city.Id]), "admin1");

        result.IsSuccess.Should().BeTrue();
        result.Value.IsDualRole.Should().BeTrue();
        result.Value.Roles.Should().Equal(UserRoles.ZoneManager, UserRoles.SalesMan);
        zm.UserType.Should().Be(UserType.ZoneManager);
        (await _context.Regions.FindAsync(region.Id))!.ZoneManagerId.Should().Be("zm1");
        (await _context.Cities.FindAsync(city.Id))!.ApprovalSalesManId.Should().Be("zm1");

        await _userRepo.Received(1).AddToRoleAsync(zm, UserRoles.SalesMan);
        await _userRepo.DidNotReceive().RemoveFromRolesAsync(Arg.Any<ApplicationUser>(), Arg.Any<IEnumerable<string>>());
    }

    [Fact]
    public async Task SetStaffRoles_SmToDual_ShouldKeepSalesManAsPrimaryType()
    {
        // Matches the seeded dual-role accounts (typed SalesMan): adding ZM must not flip UserType.
        IdentityWritesSucceed();
        var sm = Staff("sm1", UserType.SalesMan, UserRoles.SalesMan);
        var city = await SeedCityAsync("sm1");

        var result = await _sut.SetStaffRolesAsync("sm1",
            Roles([UserRoles.SalesMan, UserRoles.ZoneManager]), "admin1");

        result.IsSuccess.Should().BeTrue();
        result.Value.IsDualRole.Should().BeTrue();
        result.Value.UserType.Should().Be(UserType.SalesMan);
        sm.UserType.Should().Be(UserType.SalesMan);
        (await _context.Cities.FindAsync(city.Id))!.ApprovalSalesManId.Should().Be("sm1");
        await _userRepo.Received(1).AddToRoleAsync(sm, UserRoles.ZoneManager);
    }

    [Fact]
    public async Task SetStaffRoles_DualTypedSalesMan_DropSalesMan_ShouldBecomeZoneManager()
    {
        IdentityWritesSucceed();
        var dual = Staff("d1", UserType.SalesMan, UserRoles.SalesMan, UserRoles.ZoneManager);
        _userRepo.Query().Returns(new List<ApplicationUser>().AsAsyncQueryable());

        var result = await _sut.SetStaffRolesAsync("d1", Roles([UserRoles.ZoneManager]), "admin1");

        result.IsSuccess.Should().BeTrue();
        result.Value.UserType.Should().Be(UserType.ZoneManager);
        dual.UserType.Should().Be(UserType.ZoneManager);
    }

    [Fact]
    public async Task SetStaffRoles_AddSalesMan_CityAlreadyOwned_ShouldFail()
    {
        Staff("zm1", UserType.ZoneManager, UserRoles.ZoneManager);
        var city = await SeedCityAsync("sm-other");

        var result = await _sut.SetStaffRolesAsync("zm1",
            Roles([UserRoles.ZoneManager, UserRoles.SalesMan], cityIds: [city.Id]), "admin1");

        result.Error.Should().Be(AdminUserErrors.CityAlreadyHasSalesMan);
    }

    [Fact]
    public async Task SetStaffRoles_DualToSm_ShouldHandOffRegionAndKeepCities()
    {
        IdentityWritesSucceed();
        var dual = Staff("d1", UserType.ZoneManager, UserRoles.ZoneManager, UserRoles.SalesMan);
        Staff("zm2", UserType.ZoneManager, UserRoles.ZoneManager);
        var region = await SeedRegionAsync("d1");
        var city = await SeedCityAsync("d1");

        var result = await _sut.SetStaffRolesAsync("d1",
            Roles([UserRoles.SalesMan], newZoneManagerId: "zm2"), "admin1");

        result.IsSuccess.Should().BeTrue();
        result.Value.IsDualRole.Should().BeFalse();
        dual.UserType.Should().Be(UserType.SalesMan);
        (await _context.Regions.FindAsync(region.Id))!.ZoneManagerId.Should().Be("zm2");
        (await _context.Cities.FindAsync(city.Id))!.ApprovalSalesManId.Should().Be("d1");
        await _userRepo.Received(1).RemoveFromRolesAsync(dual, Arg.Is<IEnumerable<string>>(r => r.Single() == UserRoles.ZoneManager));
    }

    [Fact]
    public async Task SetStaffRoles_LegacyUserWithoutRoleRow_ShouldAddMissingRow()
    {
        // UserType says ZoneManager but Identity has no role row: the fallback treats the
        // user as ZM for territory rules, while the Identity write adds BOTH target rows.
        IdentityWritesSucceed();
        var legacy = Staff("l1", UserType.ZoneManager);
        _userRepo.Query().Returns(new List<ApplicationUser>().AsAsyncQueryable());

        var result = await _sut.SetStaffRolesAsync("l1",
            Roles([UserRoles.ZoneManager, UserRoles.SalesMan]), "admin1");

        result.IsSuccess.Should().BeTrue();
        await _userRepo.Received(1).AddToRoleAsync(legacy, UserRoles.ZoneManager);
        await _userRepo.Received(1).AddToRoleAsync(legacy, UserRoles.SalesMan);
        await _userRepo.DidNotReceive().RemoveFromRolesAsync(Arg.Any<ApplicationUser>(), Arg.Any<IEnumerable<string>>());
    }

    [Fact]
    public async Task SetStaffRoles_IdentityRoleWriteFails_ShouldRollBackTerritory()
    {
        IdentityWritesSucceed();
        _userRepo.RemoveFromRolesAsync(Arg.Any<ApplicationUser>(), Arg.Any<IEnumerable<string>>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "boom" }));
        var sm = Staff("sm1", UserType.SalesMan, UserRoles.SalesMan);
        var sm2 = Staff("sm2", UserType.SalesMan, UserRoles.SalesMan);
        _userRepo.Query().Returns(new List<ApplicationUser> { sm2 }.AsAsyncQueryable());
        var city = await SeedCityAsync("sm1");

        var result = await _sut.SetStaffRolesAsync("sm1",
            Roles([UserRoles.ZoneManager], [new AdminCityReassignment(city.Id, "sm2")]), "admin1");

        result.Error.Should().Be(AdminUserErrors.UpdateUserFailed);
        sm.UserType.Should().Be(UserType.SalesMan);
        await _userRepo.DidNotReceive().RevokeAllRefreshTokensAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Dual-role fixes in existing flows ──

    [Fact]
    public async Task DeleteSalesMan_DualRoleStillManagingRegion_ShouldFail()
    {
        Staff("d1", UserType.SalesMan, UserRoles.SalesMan, UserRoles.ZoneManager);
        await SeedRegionAsync("d1");

        var result = await _sut.DeleteSalesManAsync("d1", new AdminDeleteSalesManRequest([]), "admin1");

        result.Error.Should().Be(AdminUserErrors.OtherRoleTerritoryMustBeHandedOff);
    }

    [Fact]
    public async Task DeleteZoneManager_DualRoleStillOwningCities_ShouldFail()
    {
        Staff("d1", UserType.ZoneManager, UserRoles.ZoneManager, UserRoles.SalesMan);
        await SeedCityAsync("d1");

        var result = await _sut.DeleteZoneManagerAsync("d1", new AdminDeleteZoneManagerRequest(null), "admin1");

        result.Error.Should().Be(AdminUserErrors.OtherRoleTerritoryMustBeHandedOff);
    }

    [Fact]
    public async Task DeleteZoneManager_DualRoleTypedSalesMan_ShouldBeAccepted()
    {
        // Primary type is SalesMan but the account holds the ZM role: the ZM delete
        // endpoint must recognise it (previously rejected with UserTypeMismatch).
        IdentityWritesSucceed();
        Staff("d1", UserType.SalesMan, UserRoles.SalesMan, UserRoles.ZoneManager);
        Staff("zm2", UserType.ZoneManager, UserRoles.ZoneManager);
        var region = await SeedRegionAsync("d1");

        var result = await _sut.DeleteZoneManagerAsync("d1", new AdminDeleteZoneManagerRequest("zm2"), "admin1");

        result.IsSuccess.Should().BeTrue();
        (await _context.Regions.FindAsync(region.Id))!.ZoneManagerId.Should().Be("zm2");
    }

    [Fact]
    public async Task ReassignCities_ToDualRoleUserTypedZoneManager_ShouldSucceed()
    {
        var dual = Staff("d1", UserType.ZoneManager, UserRoles.ZoneManager, UserRoles.SalesMan);
        _userRepo.Query().Returns(new List<ApplicationUser>().AsAsyncQueryable());
        var city = await SeedCityAsync("sm1");

        var result = await _sut.ReassignCitiesAsync(new AdminReassignCitiesRequest([city.Id], dual.Id), "admin1");

        result.IsSuccess.Should().BeTrue();
        (await _context.Cities.FindAsync(city.Id))!.ApprovalSalesManId.Should().Be("d1");
    }

    [Fact]
    public async Task ReassignRegion_ToDualRoleUserTypedSalesMan_ShouldSucceed()
    {
        var dual = Staff("d1", UserType.SalesMan, UserRoles.SalesMan, UserRoles.ZoneManager);
        var region = await SeedRegionAsync("zm1");

        var result = await _sut.ReassignRegionAsync(new AdminReassignRegionRequest(region.Id, dual.Id), "admin1");

        result.IsSuccess.Should().BeTrue();
        (await _context.Regions.FindAsync(region.Id))!.ZoneManagerId.Should().Be("d1");
    }

    [Fact]
    public async Task ToggleStatus_DisableDualRoleTypedZoneManagerOwningCities_ShouldFail()
    {
        var dual = Staff("d1", UserType.ZoneManager, UserRoles.ZoneManager, UserRoles.SalesMan);
        await SeedCityAsync("d1");

        var result = await _sut.ToggleStatusAsync("d1", "admin1");

        result.Error.Should().Be(AdminUserErrors.ReassignTerritoryBeforeDisable);
        dual.IsDisabled.Should().BeFalse();
    }

    [Fact]
    public async Task ListUsers_DualRoleUser_ShouldBeFlagged()
    {
        var dual = new ApplicationUser { Id = "d1", Name = "Dual", MobileNumber = "+966500000001", UserType = UserType.ZoneManager, CreatedAt = DateTime.UtcNow };
        var sm = new ApplicationUser { Id = "sm1", Name = "SM", MobileNumber = "+966500000002", UserType = UserType.SalesMan, CreatedAt = DateTime.UtcNow };
        _userRepo.Query().Returns(new List<ApplicationUser> { dual, sm }.AsAsyncQueryable());
        _userRepo.GetUsersInRoleAsync(UserRoles.ZoneManager).Returns(new List<ApplicationUser> { dual });
        _userRepo.GetUsersInRoleAsync(UserRoles.SalesMan).Returns(new List<ApplicationUser> { dual, sm });

        var result = await _sut.ListUsersAsync(new AdminUserListQuery(null, null, null, null, null, null));

        result.IsSuccess.Should().BeTrue();
        var rows = result.Value.Items.ToDictionary(r => r.Id);
        rows["d1"].IsDualRole.Should().BeTrue();
        rows["d1"].Roles.Should().Equal(UserRoles.ZoneManager, UserRoles.SalesMan);
        rows["sm1"].IsDualRole.Should().BeFalse();
    }

    [Fact]
    public async Task ListUsers_FilterSalesMan_ShouldIncludeDualRoleTypedZoneManager()
    {
        var dual = new ApplicationUser { Id = "d1", Name = "Dual", MobileNumber = "+966500000001", UserType = UserType.ZoneManager, CreatedAt = DateTime.UtcNow };
        _userRepo.Query().Returns(new List<ApplicationUser> { dual }.AsAsyncQueryable());
        _userRepo.GetUsersInRoleAsync(UserRoles.ZoneManager).Returns(new List<ApplicationUser> { dual });
        _userRepo.GetUsersInRoleAsync(UserRoles.SalesMan).Returns(new List<ApplicationUser> { dual });

        var result = await _sut.ListUsersAsync(new AdminUserListQuery(null, UserType.SalesMan, null, null, null, null));

        result.Value.Items.Select(r => r.Id).Should().Equal("d1");
    }
}
