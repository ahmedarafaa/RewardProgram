using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RewardProgram.Application.Contracts.Validators;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Application.Services.Auth;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Infrastructure.Authentication;
using RewardProgram.Infrastructure.Persistance;
using System.Text;

namespace RewardProgram.API;

public static class DependencyInjection
{
    public static IServiceCollection AddDependencies(this IServiceCollection services,
       IConfiguration configuration)
    {
        services.AddControllers();
        services.AddHttpContextAccessor();  

        // CORS
        var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];

        if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException("AllowedOrigins configuration is missing or empty.");
        }

        services.AddCors(options =>
            options.AddDefaultPolicy(builder =>
                builder.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
            )
        );

        services.AddAuthConfig(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection") ??
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        services
            .AddSwaggerServices()
            .AddFluentValidationConfig();


        // Register Services
        services.AddScoped<ISmsService, SmsService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }

    private static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }

    private static IServiceCollection AddFluentValidationConfig(this IServiceCollection services)
    {
        services
            .AddFluentValidationAutoValidation()
            // Scan Application assembly where validators are defined
            .AddValidatorsFromAssemblyContaining<NationalAddressDtoValidator>();

        return services;
    }

    private static IServiceCollection AddAuthConfig(this IServiceCollection services,
        IConfiguration configuration)
    {
        
        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IJwtProvider, JwtProvider>();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var jwtSettings = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(o =>
        {
            o.SaveToken = true;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.Key!)),
                ValidIssuer = jwtSettings?.Issuer,
                ValidAudience = jwtSettings?.Audience
            };
        });

        return services;
    }
}
