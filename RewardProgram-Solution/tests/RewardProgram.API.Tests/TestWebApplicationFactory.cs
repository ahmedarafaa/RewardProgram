using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Domain.Enums.UserEnums;
using RewardProgram.Infrastructure.Persistance;

namespace RewardProgram.API.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    // Accounts the tests mint tokens for. JWT validation rejects tokens whose
    // account is missing, disabled or deleted, so they must exist in the database.
    public const string AdminId = "admin-1";
    public const string SellerId = "seller-1";
    public const string DisabledSellerId = "seller-disabled";
    public const string DeletedSellerId = "seller-deleted";

    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", JwtTokenHelper.TestKey);

        builder.ConfigureServices(services =>
        {
            // Remove ALL DbContext-related registrations (SqlServer provider, options, etc.)
            var dbContextDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("DbContext") == true
                         || d.ServiceType.FullName?.Contains("SqlServer") == true
                         || d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true
                         || d.ImplementationType?.FullName?.Contains("SqlServer") == true)
                .ToList();

            foreach (var d in dbContextDescriptors)
                services.Remove(d);

            // Re-add InMemory database
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            services.AddScoped<Application.Interfaces.IApplicationDbContext>(provider =>
                provider.GetRequiredService<ApplicationDbContext>());

            // Remove hosted services (background workers) to avoid interference
            var hostedServiceDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var d in hostedServiceDescriptors)
                services.Remove(d);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!db.Users.Any(u => u.Id == AdminId))
        {
            db.Users.AddRange(
                TestUser(AdminId, "+966500000001", UserType.SystemAdmin),
                TestUser(SellerId, "+966500000002", UserType.Seller),
                TestUser(DisabledSellerId, "+966500000003", UserType.Seller, disabled: true),
                TestUser(DeletedSellerId, "+966500000004", UserType.Seller, deleted: true));
            db.SaveChanges();
        }

        return host;
    }

    private static ApplicationUser TestUser(string id, string mobile, UserType type,
        bool disabled = false, bool deleted = false) => new()
    {
        Id = id,
        UserName = mobile,
        NormalizedUserName = mobile,
        MobileNumber = mobile,
        PhoneNumber = mobile,
        Name = "Test " + id,
        UserType = type,
        RegistrationStatus = RegistrationStatus.Approved,
        IsDisabled = disabled || deleted,
        IsAccountDeleted = deleted
    };
}
