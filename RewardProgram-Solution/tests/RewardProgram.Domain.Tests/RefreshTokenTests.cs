using FluentAssertions;
using RewardProgram.Domain.Entities;

namespace RewardProgram.Domain.Tests;

public class RefreshTokenTests
{
    [Fact]
    public void IsExpired_WhenExpiresOnInPast_ShouldReturnTrue()
    {
        var token = new RefreshToken
        {
            Token = "test-token",
            ExpiresOn = DateTime.UtcNow.AddMinutes(-1)
        };

        token.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenExpiresOnInFuture_ShouldReturnFalse()
    {
        var token = new RefreshToken
        {
            Token = "test-token",
            ExpiresOn = DateTime.UtcNow.AddDays(1)
        };

        token.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsActive_WhenNotRevokedAndNotExpired_ShouldReturnTrue()
    {
        var token = new RefreshToken
        {
            Token = "test-token",
            ExpiresOn = DateTime.UtcNow.AddDays(1),
            RevokedOn = null
        };

        token.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenRevoked_ShouldReturnFalse()
    {
        var token = new RefreshToken
        {
            Token = "test-token",
            ExpiresOn = DateTime.UtcNow.AddDays(1),
            RevokedOn = DateTime.UtcNow
        };

        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_WhenExpired_ShouldReturnFalse()
    {
        var token = new RefreshToken
        {
            Token = "test-token",
            ExpiresOn = DateTime.UtcNow.AddMinutes(-1),
            RevokedOn = null
        };

        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsActive_WhenBothRevokedAndExpired_ShouldReturnFalse()
    {
        var token = new RefreshToken
        {
            Token = "test-token",
            ExpiresOn = DateTime.UtcNow.AddMinutes(-1),
            RevokedOn = DateTime.UtcNow.AddMinutes(-5)
        };

        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void NewToken_ShouldHave_CreatedOnSetToUtcNow()
    {
        var before = DateTime.UtcNow;
        var token = new RefreshToken();
        var after = DateTime.UtcNow;

        token.CreatedOn.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void NewToken_ShouldHave_EmptyTokenString()
    {
        var token = new RefreshToken();

        token.Token.Should().BeEmpty();
    }
}
