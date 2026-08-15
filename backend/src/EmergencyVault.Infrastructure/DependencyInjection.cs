using System.Net;
using EmergencyVault.Application.Interfaces;
using EmergencyVault.Application.Options;
using EmergencyVault.Domain.Constant;
using EmergencyVault.Domain.Entities;
using EmergencyVault.Infrastructure.Billing;
using EmergencyVault.Infrastructure.Caching;
using EmergencyVault.Infrastructure.Email;
using EmergencyVault.Infrastructure.Persistence;
using EmergencyVault.Infrastructure.Repositories;
using EmergencyVault.Infrastructure.Security;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.Azure.StackExchangeRedis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace EmergencyVault.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(Environment.GetEnvironmentVariable(EnvironmentVariableConst.SqlServerConnectionString) 
                ?? throw new InvalidOperationException("Missing ConnectionStrings:Default configuration.")));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariableConst.RedisConnectionString)
                ?? throw new InvalidOperationException("Missing ConnectionStrings:Redis configuration.");

            var redisOptions = ConfigurationOptions.Parse(connectionString);

            // Azure Managed Redis has no access key (accessKeysAuthentication is disabled on the
            // cache) - authenticate as the container's own system-assigned managed identity instead.
            // Local/Docker Redis has no such hostname and keeps using the plain connection string.
            if (redisOptions.EndPoints.Any(endpoint => endpoint is DnsEndPoint dns
                && dns.Host.EndsWith(".redis.azure.net", StringComparison.OrdinalIgnoreCase)))
            {
                redisOptions.Protocol = RedisProtocol.Resp3;
                redisOptions.ConfigureForAzureWithSystemAssignedManagedIdentityAsync().GetAwaiter().GetResult();
            }

            return ConnectionMultiplexer.Connect(redisOptions);
        });

        services.AddHttpContextAccessor();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;

                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager<SignInManager<ApplicationUser>>()
            .AddDefaultTokenProviders();

        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserFailedLoginRepository, UserFailedLoginRepository>();
        services.AddScoped<IUserLoginHistoryRepository, UserLoginHistoryRepository>();
        services.AddScoped<IVaultRepository, VaultRepository>();
        services.AddScoped<IVaultAttachmentBlobStore, SqlVaultAttachmentBlobStore>();
        services.AddScoped<IBillingRepository, BillingRepository>();
        services.AddScoped<IStripeGateway, StripeGateway>();
        services.AddSingleton<IRefreshTokenCache, RedisRefreshTokenCache>();
        services.AddScoped<IEmailSender, ResendEmailSender>();
        services.AddScoped<EmailJobs>();

        // Job queue lives in the same SQL Server database as everything else (Hangfire creates
        // its own schema on first run). A single worker on the dedicated "emails" queue processes
        // sends one at a time so we stay under Resend's rate limit.
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(Environment.GetEnvironmentVariable(EnvironmentVariableConst.SqlServerConnectionString)
                ?? throw new InvalidOperationException("Missing ConnectionStrings:Default configuration.")));

        services.AddHangfireServer(options =>
        {
            options.Queues = new[] { "emails" };
            options.WorkerCount = 1;
        });

        // ITokenService depends on the ECDsa signing key singleton, registered by the Api
        // project in Program.cs (loaded once there and shared with JWT Bearer validation).
        services.AddSingleton<ITokenService, TokenService>();

        services.AddSingleton<IHotpService, HotpService>();
        services.AddSingleton<ITwoFactorChallengeCache, RedisTwoFactorChallengeCache>();
        services.AddSingleton<ISharedVaultAccessChallengeCache, RedisSharedVaultAccessChallengeCache>();
        services.AddSingleton<ISharedVaultAccessGrantCache, RedisSharedVaultAccessGrantCache>();

        return services;
    }
}
