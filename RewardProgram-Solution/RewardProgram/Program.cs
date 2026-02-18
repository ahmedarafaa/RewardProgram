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

            // Auto-migrate in Development
            if (app.Environment.IsDevelopment())
            {
                using var migrationScope = app.Services.CreateScope();
                var db = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.MigrateAsync();
            }

            // Seed data (roles, users, regions, cities)
            await DataSeeder.SeedAsync(app.Services);

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
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
