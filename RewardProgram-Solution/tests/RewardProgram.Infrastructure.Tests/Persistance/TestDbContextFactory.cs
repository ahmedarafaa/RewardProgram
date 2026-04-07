using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RewardProgram.Infrastructure.Persistance;
using System.Security.Claims;

namespace RewardProgram.Infrastructure.Tests.Persistance;

/// <summary>
/// Creates ApplicationDbContext instances backed by EF InMemory provider for testing.
/// Tests SaveChangesAsync audit logic, soft-delete conversion, and query filters.
/// </summary>
public sealed class TestDbContextFactory : IDisposable
{
    private readonly string _dbName;
    private readonly string? _userId;

    public TestDbContextFactory(string? userId = null)
    {
        _userId = userId;
        _dbName = Guid.NewGuid().ToString(); // Unique per test class instance
    }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        var httpContextAccessor = new HttpContextAccessor();

        if (_userId is not null)
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, _userId) };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            httpContextAccessor.HttpContext = new DefaultHttpContext { User = principal };
        }
        else
        {
            httpContextAccessor.HttpContext = new DefaultHttpContext();
        }

        return new ApplicationDbContext(options, httpContextAccessor);
    }

    public void Dispose() { }
}
