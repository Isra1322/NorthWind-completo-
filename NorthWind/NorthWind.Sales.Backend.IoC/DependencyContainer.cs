namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyContainer
{
    public static IServiceCollection AddNorthWindSalesServices(
        this IServiceCollection services,
        Action<DBOptions> configureDBOptions,
        Action<SmtpOptions> configureSmtpOptions,
        Action<MembershipDBOptions> configureMembershipDBOptions,
        Action<JwtOptions> configureJwtOptions,
        bool useFakeUser = false)
    {
        services.AddUseCasesServices()
            .AddRepositories()
            .AddDataContexts(configureDBOptions)
            .AddPresenters()
            .AddValidationService()
            .AddValidators()
            .AddValidationExceptionHandler()
            .AddUpdateExceptionHandler()
            .AddUnauthorizedAccessExceptionHandler()
            .AddUnhandledExceptionHandler()
            .AddEventServices()
            .AddMailServices(configureSmtpOptions)
            .AddDomainLogsServices()
            .AddTransactionServices()
            .AddUserServices(useFakeUser)
            .AddMembershipCoreServices(configureJwtOptions)
            .AddMembershipIdentityServices(configureMembershipDBOptions);
        services.AddScoped<Microsoft.AspNetCore.Identity.IdentityErrorDescriber, NorthWind.Sales.Backend.IoC.SpanishIdentityErrorDescriber>();
        var membership = services.Last(s => s.ServiceType == typeof(NorthWind.Membership.Backend.Core.Interfaces.Common.IMembershipService));
        services.Remove(membership);
        services.AddScoped<NorthWind.Membership.Backend.Core.Interfaces.Common.IMembershipService>(provider =>
            new NorthWind.Sales.Backend.IoC.MembershipValidationService(
                (NorthWind.Membership.Backend.Core.Interfaces.Common.IMembershipService)
                ActivatorUtilities.CreateInstance(provider, membership.ImplementationType!)));
        return services;
    }
}
