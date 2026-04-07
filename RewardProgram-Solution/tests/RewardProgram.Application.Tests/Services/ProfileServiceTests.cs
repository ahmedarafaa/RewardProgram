using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RewardProgram.Application.Abstractions;
using RewardProgram.Application.Errors;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Files;
using RewardProgram.Application.Services;
using RewardProgram.Application.Tests.TestHelpers;
using RewardProgram.Domain.Entities;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums;
using RewardProgram.Domain.Enums.UserEnums;

namespace RewardProgram.Application.Tests.Services;

public class ProfileServiceTests : IDisposable
{
    private readonly TestDbContext _context;
    private readonly IUserRepository _userRepo;
    private readonly IFileStorageService _fileStorage;
    private readonly ProfileService _sut;

    public ProfileServiceTests()
    {
        _context = TestDbContext.Create();
        _userRepo = Substitute.For<IUserRepository>();
        _fileStorage = Substitute.For<IFileStorageService>();
        _sut = new ProfileService(_context, _userRepo, _fileStorage,
            Substitute.For<ILogger<ProfileService>>());
    }

    public void Dispose() => _context.Dispose();

    // ── GetProfile ──

    [Fact]
    public async Task GetProfile_UserNotFound_ShouldFail()
    {
        _userRepo.FindByIdAsync("bad", Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.GetProfileAsync("bad");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
    }

    [Fact]
    public async Task GetProfile_ValidUser_ShouldReturnProfile()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test User", MobileNumber = "+966500000001",
            UserType = UserType.Seller, ProfileImageUrl = "/img/test.jpg"
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var wallet = new Wallet { UserId = "u1", Balance = 500, SarBalance = 50 };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        var result = await _sut.GetProfileAsync("u1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test User");
        result.Value.Points.Should().Be(500);
        result.Value.ProfileImageUrl.Should().Be("/img/test.jpg");
    }

    [Fact]
    public async Task GetProfile_NoWallet_ShouldReturnZeroPoints()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "+966500000001",
            UserType = UserType.Technician
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.GetProfileAsync("u1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Points.Should().Be(0);
    }

    [Fact]
    public async Task GetProfile_WithNationalAddress_ShouldReturnCityName()
    {
        // Seed city
        var region = new Region { NameAr = "الرياض", NameEn = "Riyadh", IsActive = true };
        _context.Regions.Add(region);
        await _context.SaveChangesAsync();

        var city = new City { NameAr = "الرياض", NameEn = "Riyadh", RegionId = region.Id, IsActive = true };
        _context.Cities.Add(city);
        await _context.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "+966500000001",
            UserType = UserType.Seller,
            NationalAddress = new NationalAddress { CityId = city.Id, Street = "Main St", District = "Olaya" }
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.GetProfileAsync("u1");

        result.IsSuccess.Should().BeTrue();
        result.Value.CityName.Should().Be("الرياض");
        result.Value.District.Should().Be("Olaya");
    }

    // ── UpdateProfilePhoto ──

    [Fact]
    public async Task UpdatePhoto_InvalidExtension_ShouldFail()
    {
        var photo = Substitute.For<IFormFile>();
        photo.FileName.Returns("test.gif");
        photo.Length.Returns(1024);

        var result = await _sut.UpdateProfilePhotoAsync("u1", photo);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProfileErrors.InvalidImageType);
    }

    [Fact]
    public async Task UpdatePhoto_TooLarge_ShouldFail()
    {
        var photo = Substitute.For<IFormFile>();
        photo.FileName.Returns("test.jpg");
        photo.Length.Returns(6 * 1024 * 1024); // 6MB

        var result = await _sut.UpdateProfilePhotoAsync("u1", photo);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProfileErrors.ImageTooLarge);
    }

    [Fact]
    public async Task UpdatePhoto_UserNotFound_ShouldFail()
    {
        var photo = Substitute.For<IFormFile>();
        photo.FileName.Returns("test.png");
        photo.Length.Returns(1024);

        _userRepo.FindByIdAsync("bad", Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.UpdateProfilePhotoAsync("bad", photo);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
    }

    [Fact]
    public async Task UpdatePhoto_Valid_ShouldUploadAndUpdateUser()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "+966500000001",
            ProfileImageUrl = "/old/photo.jpg"
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var photo = Substitute.For<IFormFile>();
        photo.FileName.Returns("new.jpg");
        photo.Length.Returns(1024);
        photo.OpenReadStream().Returns(new MemoryStream([0x01]));

        _fileStorage.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), "profiles", Arg.Any<CancellationToken>())
            .Returns(Result.Success("/profiles/new-guid.jpg"));

        var result = await _sut.UpdateProfilePhotoAsync("u1", photo);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("/profiles/new-guid.jpg");
        await _fileStorage.Received(1).DeleteAsync("/old/photo.jpg");
        user.ProfileImageUrl.Should().Be("/profiles/new-guid.jpg");
    }

    // ── DeleteAccount ──

    [Fact]
    public async Task DeleteAccount_UserNotFound_ShouldFail()
    {
        _userRepo.FindByIdAsync("bad", Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.DeleteAccountAsync("bad");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AuthErrors.UserNotFound);
    }

    [Fact]
    public async Task DeleteAccount_HasPendingRedemptions_ShouldFail()
    {
        var user = new ApplicationUser { Id = "u1", Name = "Test", MobileNumber = "+966500000001" };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        _context.RedemptionRequests.Add(new RedemptionRequest
        {
            UserId = "u1",
            Status = RedemptionRequestStatus.PendingSalesMan,
            PointsAmount = 100, SarRate = 10, SarAmount = 10,
            Method = RedemptionMethod.Cash
        });
        await _context.SaveChangesAsync();

        var result = await _sut.DeleteAccountAsync("u1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProfileErrors.HasPendingRedemptions);
    }

    [Fact]
    public async Task DeleteAccount_Valid_ShouldDisableAndRevokeTokens()
    {
        var user = new ApplicationUser
        {
            Id = "u1", Name = "Test", MobileNumber = "+966500000001",
            RefreshTokens = new List<RefreshToken>
            {
                new() { Token = "token1", ExpiresOn = DateTime.UtcNow.AddDays(1) },
                new() { Token = "token2", ExpiresOn = DateTime.UtcNow.AddDays(1) }
            }
        };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.DeleteAccountAsync("u1");

        result.IsSuccess.Should().BeTrue();
        user.IsDisabled.Should().BeTrue();
        user.RefreshTokens.Should().AllSatisfy(t => t.RevokedOn.Should().NotBeNull());
        await _userRepo.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task DeleteAccount_CompletedRedemptions_ShouldSucceed()
    {
        var user = new ApplicationUser { Id = "u1", Name = "Test", MobileNumber = "+966500000001" };
        _userRepo.FindByIdAsync("u1", Arg.Any<CancellationToken>()).Returns(user);

        _context.RedemptionRequests.Add(new RedemptionRequest
        {
            UserId = "u1",
            Status = RedemptionRequestStatus.Completed,
            PointsAmount = 100, SarRate = 10, SarAmount = 10,
            Method = RedemptionMethod.Cash
        });
        await _context.SaveChangesAsync();

        var result = await _sut.DeleteAccountAsync("u1");

        result.IsSuccess.Should().BeTrue();
    }
}
