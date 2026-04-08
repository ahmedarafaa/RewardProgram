using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums.UserEnums;
using RewardProgram.Infrastructure.Persistance;
using System.Security.Claims;

namespace RewardProgram.Infrastructure.Tests.Persistance;

/// <summary>
/// Creates ApplicationDbContext instances backed by SQLite in-memory for tests that
/// need real constraint enforcement (unique indexes, FK constraints, etc.).
/// </summary>
public sealed class SqliteTestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string? _userId;
    private readonly HashSet<string> _seededUserIds = new();
    private int _mobileCounter;

    public SqliteTestDbContextFactory(string? userId = null)
    {
        _userId = userId;
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var ctx = CreateContext();
        // RelationalDatabaseCreator.CreateTables uses the model directly
        // but nvarchar(max) is not valid SQLite. We generate SQL and fix it.
        var creator = ctx.GetService<Microsoft.EntityFrameworkCore.Storage.IDatabaseCreator>()
            as Microsoft.EntityFrameworkCore.Storage.RelationalDatabaseCreator;

        // Generate the SQL, replace SQL Server types that SQLite doesn't understand
        var sql = creator!.GenerateCreateScript();
        sql = sql.Replace("nvarchar(max)", "TEXT")
                 .Replace("varbinary(max)", "BLOB")
                 .Replace("rowversion", "BLOB");

        // SQLite doesn't auto-generate rowversion — add a default so NOT NULL is satisfied
        sql = sql.Replace("\"RowVersion\" BLOB NOT NULL", "\"RowVersion\" BLOB NOT NULL DEFAULT X'00000001'");

        // Execute the fixed SQL directly
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Seeds minimal ApplicationUser rows so FK constraints to AspNetUsers are satisfied.
    /// Skips users already seeded in this factory instance.
    /// </summary>
    public async Task SeedUsersAsync(ApplicationDbContext ctx, params string[] userIds)
    {
        foreach (var id in userIds)
        {
            if (!_seededUserIds.Add(id)) continue;
            ctx.Users.Add(new ApplicationUser
            {
                Id = id,
                UserName = id,
                NormalizedUserName = id.ToUpperInvariant(),
                Name = "Test " + id,
                MobileNumber = $"+96650{++_mobileCounter:D6}",
                UserType = UserType.Seller,
                RegistrationStatus = RegistrationStatus.Approved,
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            });
        }
        await ctx.SaveChangesAsync();
    }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
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

    public void Dispose()
    {
        _connection.Dispose();
    }
}
