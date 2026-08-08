using AuthSystem.Application.Interfaces;
using AuthSystem.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AuthSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
