using EmergencyVault.Application.Interfaces;
using EmergencyVault.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EmergencyVault.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IVaultService, VaultService>();
        return services;
    }
}
