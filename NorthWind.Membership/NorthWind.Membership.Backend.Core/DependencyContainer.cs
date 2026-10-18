using NorthWind.Membership.Backend.Core.UseCases.UserRegistration;
using NorthWind.Membership.Backend.Core.UseCases.UserLogin;
using NorthWind.Membership.Backend.Core.Presenters.UserLogin;
using NorthWind.Membership.Backend.Core.Services;
using NorthWind.Membership.Backend.Core.Options;
using System.Text;
using NorthWind.Membership.Backend.Core.Presenters.UserRegistration;
namespace Microsoft.Extensions.DependencyInjection;
public static class DependencyContainer
{
    public static IServiceCollection AddMembershipCoreServices(this IServiceCollection services, Action<JwtOptions> configureJwtOptions)
    {
        services.AddValidationService().AddMembershipValidators();
        services.AddScoped<IUserRegistrationInputPort, UserRegistrationInteractor>();
        services.AddScoped<IUserRegistrationOutputPort, UserRegistrationPresenter>();
        services.AddScoped<IUserLoginInputPort, UserLoginInteractor>();
        services.AddScoped<IUserLoginOutputPort, UserLoginPresenter>();
        services.AddSingleton<JwtService>();
        services.AddOptions<JwtOptions>().Configure(configureJwtOptions)
            .Validate(o => Encoding.UTF8.GetByteCount(o.SecurityKey) >= 32, "La clave JWT debe tener al menos 32 bytes.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ValidIssuer) && !string.IsNullOrWhiteSpace(o.ValidAudience), "Emisor y audiencia JWT requeridos.")
            .Validate(o => o.ExpireInMinutes > 0, "La vigencia JWT debe ser positiva.").ValidateOnStart();
        return services;
    }
}
