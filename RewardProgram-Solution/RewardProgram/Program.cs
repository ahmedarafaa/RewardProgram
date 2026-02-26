using Microsoft.EntityFrameworkCore;
using RewardProgram.API;
using RewardProgram.Infrastructure.Persistance;
using RewardProgram.Infrastructure.Persistance.Data;
using Serilog;

namespace RewardProgram
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDependencies(builder.Configuration);
           
            builder.Host.UseSerilog((context, configuration) =>
                configuration.ReadFrom.Configuration(context.Configuration)
            );
                       
            var app = builder.Build();

            // Auto-migrate in Development & Staging
            if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
            {
                using var migrationScope = app.Services.CreateScope();
                var db = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.MigrateAsync();

                // Seed data (roles, users, regions, cities)
                await DataSeeder.SeedAsync(app.Services);
            }

            // Swagger in Development & Staging
            if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
            {
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/public/swagger.json", "Public API");
                    options.SwaggerEndpoint("/swagger/admin/swagger.json", "Admin API");
                });
            }

            app.UseExceptionHandler();
            app.UseSerilogRequestLogging();
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCors();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
