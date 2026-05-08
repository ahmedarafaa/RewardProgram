using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
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
            QuestPDF.Settings.License = LicenseType.Community;

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDependencies(builder.Configuration);
           
            builder.Host.UseSerilog((context, configuration) =>
                configuration.ReadFrom.Configuration(context.Configuration)
            );
                       
            var app = builder.Build();

            var isNonProd = app.Environment.IsDevelopment() || app.Environment.IsStaging() || app.Environment.IsEnvironment("UAT");

            // Auto-migrate in Development, Staging & UAT
            if (isNonProd)
            {
                using var migrationScope = app.Services.CreateScope();
                var db = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.MigrateAsync();

                // Seed data (roles, users, regions, cities, products, ERP customers)
                await DataSeeder.SeedAsync(app.Services);
            }

            // Swagger in Development, Staging & UAT
            if (isNonProd)
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

            if (!app.Environment.IsDevelopment())
                app.UseHsts();

            app.UseHttpsRedirection();

            app.Use(async (ctx, next) =>
            {
                var headers = ctx.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                await next();
            });

            app.UseStaticFiles();
            app.UseCors();

            var supportedCultures = new[] { "ar", "en" };
            app.UseRequestLocalization(options =>
            {
                options.SetDefaultCulture("ar")
                       .AddSupportedCultures(supportedCultures)
                       .AddSupportedUICultures(supportedCultures);
            });

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
