using NorthWind.Membership.Entities.Validators.UserRegistration;
namespace Microsoft.Extensions.DependencyInjection;
public static class DependencyContainer
{
    public static IServiceCollection AddMembershipValidators(this IServiceCollection services)
    {
        services.AddModelValidator<UserRegistrationDto, UserRegistrationDtoValidator>();
        services.AddModelValidator<NorthWind.Membership.Entities.Dtos.UserLogin.UserCredentialsDto,
            NorthWind.Membership.Entities.Validators.UserLogin.UserCredentialsDtoValidator>();
        return services;
    }
}
