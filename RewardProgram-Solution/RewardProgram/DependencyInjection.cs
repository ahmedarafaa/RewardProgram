namespace RewardProgram.API;

public static class DependencyInjection
{
    public static IServiceCollection AddDependencies(this IServiceCollection services,
       IConfiguration configuration)
    {
        services.AddControllers();

        var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>();

        services.AddCors(options =>
            options.AddDefaultPolicy(builder =>
                builder.WithOrigins(allowedOrigins!)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
            )
        );

        //services.AddAuthConfig(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection") ??
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        //services.AddDbContext<ApplicationDbContext>(options =>
        //options.UseSqlServer(connectionString));

        //services
        //    .AddSwaggerServices()
        //    .AddMapsterConfig()
        //    .AddFluentValidationConfig();


        // Register Services



        return services;
    }

    private static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        object value = services.AddSwaggerGen();
        
        return services;
    }

}
