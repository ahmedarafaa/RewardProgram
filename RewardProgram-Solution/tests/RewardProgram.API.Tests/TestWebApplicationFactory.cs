using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RewardProgram.Infrastructure.Persistance;

namespace RewardProgram.API.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

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
}
