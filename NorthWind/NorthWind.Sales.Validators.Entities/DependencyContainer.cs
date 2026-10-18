namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyContainer
{
    public static IServiceCollection AddValidators(this IServiceCollection services)
    {
        services.AddModelValidator<CreateOrderDto, CreateOrderDtoValidator>();
        services.AddModelValidator<CreateOrderDetailDto, CreateOrderDetailDtoValidator>();
        services.AddModelValidator<NorthWind.Membership.Entities.Dtos.UserRegistration.UserRegistrationDto,
            NorthWind.Sales.Validators.Entities.UserRegistrationNameValidator>();
        services.AddScoped<NorthWind.Validation.Entities.Interfaces.IModelValidatorHub<NorthWind.Membership.Entities.Dtos.UserRegistration.UserRegistrationDto>,
            NorthWind.Sales.Validators.Entities.RegistrationEmailValidatorHub>();
        return services;
    }
}
