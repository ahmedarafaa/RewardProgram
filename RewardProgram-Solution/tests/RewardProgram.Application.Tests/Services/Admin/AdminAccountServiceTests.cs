using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Contracts.Admin.Accounts;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Services.Admin;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Constants;
using RewardProgram.Domain.Entities.Users;

namespace RewardProgram.Application.Tests.Services.Admin;

public class AdminAccountServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly IUserRepository _userRepo;
    private readonly AdminAccountService _sut;

    public AdminAccountServiceTests()
    {
        _context = TestDbContext.Create();
        _userRepo = Substitute.For<IUserRepository>();
        _sut = new AdminAccountService(
            _userRepo,
            _context,
            new StubLocalizer<ErrorMessages>(),
            Substitute.For<ILogger<AdminAccountService>>());
    }

    public void Dispose() => _context.Dispose();

    private ApplicationUser MockUser(string id, params string[] roles)
    {
        var user = new ApplicationUser { Id = id, Name = "Admin User", UserName = "admin.user" };
        _userRepo.FindByIdAsync(id, Arg.Any<CancellationToken>()).Returns(user);
        _userRepo.GetRolesAsync(user).Returns(roles.ToList());
        return user;
    }

    [Fact]
    public void GetPermissionCatalog_ShouldReturnAllPermissionsGroupedByModule()
    {
        var result = _sut.GetPermissionCatalog();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(10); // 10 modules (incl. ErpCustomers)
        result.Value.SelectMany(m => m.Permissions).Should().HaveCount(AdminPermissions.All.Count);
    }

    [Fact]
    public async Task SetPermissions_UnknownPermission_ShouldFail()
    {
        MockUser("a1", UserRoles.Admin);

        var result = await _sut.SetPermissionsAsync("a1",
            new SetAdminPermissionsRequest(["Bogus.Permission"]));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminAccountErrors.InvalidPermission);
    }

    [Fact]
    public async Task SetPermissions_Valid_ShouldReplaceTheWholeSet()
    {
        MockUser("a1", UserRoles.Admin);

        var first = await _sut.SetPermissionsAsync("a1",
            new SetAdminPermissionsRequest([AdminPermissions.UsersView, AdminPermissions.ProductsManage]));
        first.IsSuccess.Should().BeTrue();
        first.Value.Permissions.Should().BeEquivalentTo(
            [AdminPermissions.UsersView, AdminPermissions.ProductsManage]);

        // A second call replaces — does not merge.
        var second = await _sut.SetPermissionsAsync("a1",
            new SetAdminPermissionsRequest([AdminPermissions.ScansView]));
        second.IsSuccess.Should().BeTrue();
        second.Value.Permissions.Should().ContainSingle()
            .Which.Should().Be(AdminPermissions.ScansView);
    }

    [Fact]
    public async Task SetPermissions_TargetIsSystemAdmin_ShouldFail()
    {
        MockUser("sa", UserRoles.SystemAdmin);

        var result = await _sut.SetPermissionsAsync("sa",
            new SetAdminPermissionsRequest([AdminPermissions.UsersView]));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminAccountErrors.CannotModifySystemAdmin);
    }

    [Fact]
    public async Task Delete_TargetIsSystemAdmin_ShouldFail()
    {
        MockUser("sa", UserRoles.SystemAdmin);

        var result = await _sut.DeleteAsync("sa");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminAccountErrors.CannotModifySystemAdmin);
    }

    [Fact]
    public async Task GetById_NonAdminUser_ShouldReturnNotFound()
    {
        MockUser("u1", UserRoles.Seller);

        var result = await _sut.GetByIdAsync("u1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminAccountErrors.NotFound);
    }

    [Fact]
    public async Task Create_DuplicateUsername_ShouldFail()
    {
        _userRepo.FindByUsernameAsync("taken")
            .Returns(new ApplicationUser { Id = "existing" });

        var result = await _sut.CreateAsync(new CreateAdminAccountRequest("taken", "New Admin", "Password1"));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AdminAccountErrors.UsernameAlreadyExists);
    }
}
