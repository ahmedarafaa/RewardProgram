using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using RewardProgram.Application.Contracts.Validators;
using RewardProgram.Application.Helpers;
using RewardProgram.Application.Interfaces;
using RewardProgram.Application.Interfaces.Admin;
using RewardProgram.Application.Interfaces.Auth;
using RewardProgram.Application.Interfaces.Files;
using RewardProgram.Application.Services;
using RewardProgram.Application.Services.Admin;
using RewardProgram.Application.Services.Auth;
using RewardProgram.Application.Services.Lookups;
using RewardProgram.Domain.Entities.Users;
using RewardProgram.Infrastructure.Authentication;
using RewardProgram.Infrastructure.Persistance;
using RewardProgram.Infrastructure.Services;
using RewardProgram.Infrastructure.Services.FileStorage;
using RewardProgram.Infrastructure.Services.WhatsAppService;
using System.Text;

namespace RewardProgram.API;

public static class DependencyInjection
{
    public static IServiceCollection AddDependencies(this IServiceCollection services,
       IConfiguration configuration)
    {
        services.AddControllers();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddLocalization();

        // Localize the framework's auto-generated 400 ProblemDetails when DTO model-state
        // validation fails — its default `title` is the hardcoded English string
        // "One or more validation errors occurred." which leaks into ar-locale responses.
        services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var localizer = context.HttpContext.RequestServices
                    .GetService<Microsoft.Extensions.Localization.IStringLocalizer<RewardProgram.Application.Errors.ErrorMessages>>();
                var title = localizer is not null
                    ? localizer["Validation.OneOrMoreErrors"].Value
                    : "One or more validation errors occurred";

                var problem = new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = title,
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                };
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problem)
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        services.AddHsts(options =>
        {
            options.Preload = true;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(365);
        });

        var keysDirectory = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "App_Data", "keys"));
        keysDirectory.Create();
        services.AddDataProtection()
            .PersistKeysToFileSystem(keysDirectory)
            .SetApplicationName("RewardProgram");

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

        // Configure Twilio Options
        services.Configure<TwilioOptions>(configuration.GetSection(TwilioOptions.SectionName));
        services.Configure<InvitationOptions>(configuration.GetSection(InvitationOptions.SectionName));
        services.Configure<TwilioWebhookOptions>(configuration.GetSection(TwilioWebhookOptions.SectionName));

        // Configure Verification Token Options (derived from JWT key to avoid key reuse)
        var jwtKey = configuration.GetSection(JwtOptions.SectionName)["Key"]!;
        services.Configure<VerificationTokenOptions>(o =>
        {
            o.HmacKey = $"VerificationToken:{jwtKey}";
            o.ExpiryMinutes = 300;
        });

        // Register Services
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITwilioService, TwilioService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IOtpFallbackService, OtpFallbackService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAdminProductService, AdminProductService>();
        services.AddScoped<IAdminBarcodeService, AdminBarcodeService>();
        services.AddScoped<IScanService, ScanService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IAdminRewardSettingsService, AdminRewardSettingsService>();
        services.AddScoped<IAdminContentService, AdminContentService>();
        services.AddScoped<IRedemptionService, RedemptionService>();
        services.AddScoped<IRedemptionApprovalService, RedemptionApprovalService>();
        services.AddScoped<IAdminRedemptionService, AdminRedemptionService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IShopService, ShopService>();
        services.Configure<FirebaseOptions>(configuration.GetSection(FirebaseOptions.SectionName));
        ValidateFirebaseConfig(configuration);
        services.AddSingleton<IFirebaseMessagingService, FirebaseMessagingService>();
        services.AddSingleton<IBarcodePdfGenerator, BarcodePdfGenerator>();
        services.AddHostedService<PointsExpiryBackgroundService>();
        services.AddHostedService<OtpCleanupBackgroundService>();

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
            };
        });

        return services;
    }

    private static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("public", new OpenApiInfo
            {
                Title = "RewardProgram — Public API",
                Version = "v1",
                Description = "Auth, Approvals, Lookups"
            });

            options.SwaggerDoc("admin", new OpenApiInfo
            {
                Title = "RewardProgram — Admin API",
                Version = "v1",
                Description = "Admin panel endpoints"
            });

            // Route each controller to the right document based on namespace
            options.DocInclusionPredicate((docName, apiDesc) =>
            {
                var controllerType = (apiDesc.ActionDescriptor as Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)
                    ?.ControllerTypeInfo;

                if (controllerType == null) return false;

                var isAdmin = controllerType.Namespace?.Contains(".Admin") == true;

                return docName switch
                {
                    "admin" => isAdmin,
                    "public" => !isAdmin,
                    _ => false
                };
            });

            options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT Authorization header using the Bearer scheme."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("bearer", document)] = []
            });
        });

        return services;
    }

    private static void ValidateFirebaseConfig(IConfiguration configuration)
    {
        var firebase = configuration.GetSection(FirebaseOptions.SectionName).Get<FirebaseOptions>();
        if (firebase is null || !firebase.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(firebase.ServiceAccountKeyPath))
            throw new InvalidOperationException(
                "Firebase is enabled but 'Firebase:ServiceAccountKeyPath' is not set. Provide the path to the service-account JSON or set 'Firebase:Enabled' to false.");

        var resolved = FirebaseOptions.ResolveKeyPath(firebase.ServiceAccountKeyPath);
        if (!File.Exists(resolved))
            throw new InvalidOperationException(
                $"Firebase is enabled but the service-account JSON was not found at '{resolved}'. Deploy the credentials file or disable Firebase.");
    }

    private static IServiceCollection AddFluentValidationConfig(this IServiceCollection services)
    {
        services
            .AddFluentValidationAutoValidation()
            .AddValidatorsFromAssemblyContaining<NationalAddressDtoValidator>();

        return services;
    }

    private static IServiceCollection AddAuthConfig(this IServiceCollection services,
        IConfiguration configuration)
    {

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var jwtSettings = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration section is missing.");

        if (string.IsNullOrWhiteSpace(jwtSettings.Key))
            throw new InvalidOperationException("JWT signing key must not be empty. Configure 'Jwt:Key' in appsettings.");

        if (jwtSettings.Key.Contains("REPLACE_WITH_SECURE_KEY", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("JWT signing key is still the placeholder value. Set a real 'Jwt:Key' in environment-specific configuration.");

        if (jwtSettings.Key.Length < 32)
            throw new InvalidOperationException("JWT signing key must be at least 32 characters (256 bits) for HS256.");

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
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                ValidIssuer = jwtSettings.Issuer,
                ValidAudiences = new[] { jwtSettings.Audience, jwtSettings.AdminAudience },
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });

        return services;
    }
}
