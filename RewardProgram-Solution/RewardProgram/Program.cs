using RewardProgram.API;
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
            
            builder.Services.AddOpenApi();
            
            var app = builder.Build();


            // Seed data
            //await DataSeeder.SeedAsync(app.Services);

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }
            app.UseSerilogRequestLogging();
            app.UseHttpsRedirection();
            app.UseCors();
            app.UseAuthorization();

            app.MapControllers();
            app.UseExceptionHandler();

            app.Run();
        }
    }
}
