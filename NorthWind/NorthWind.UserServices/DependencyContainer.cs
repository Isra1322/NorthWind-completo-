using Microsoft.Extensions.DependencyInjection;
using NorthWind.Entities.Interfaces;

namespace NorthWind.UserServices;

public static class DependencyContainer
{
    public static IServiceCollection AddUserServices(
        this IServiceCollection services, bool useFakeUser = false)
    {
        services.AddHttpContextAccessor();
        if (useFakeUser)
            services.AddSingleton<IUserService, UserServiceFake>();
        else
            services.AddSingleton<IUserService, UserService>();
        return services;
    }
}
