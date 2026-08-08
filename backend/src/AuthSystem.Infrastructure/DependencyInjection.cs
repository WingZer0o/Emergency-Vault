using AuthSystem.Application.Interfaces;
using AuthSystem.Application.Options;
using AuthSystem.Domain.Entities;
using AuthSystem.Infrastructure.Caching;
using AuthSystem.Infrastructure.Email;
using AuthSystem.Infrastructure.Persistence;
using AuthSystem.Infrastructure.Repositories;
using AuthSystem.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace AuthSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("Missing ConnectionStrings:Redis configuration.")));

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
        services.AddSingleton<IRefreshTokenCache, RedisRefreshTokenCache>();
        services.AddScoped<IEmailSender, ConsoleEmailSender>();

        // ITokenService depends on the ECDsa signing key singleton, registered by the Api
        // project in Program.cs (loaded once there and shared with JWT Bearer validation).
        services.AddSingleton<ITokenService, TokenService>();

        return services;
    }
}
