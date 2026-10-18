using NorthWind.HttpDelegatingHandlers;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthWind.Membership.Frontend.RazorViews.AuthenticationStateProviders;
using NorthWind.Membership.Frontend.RazorViews.Interfaces;
using NorthWind.Membership.Frontend.RazorViews.Services;
using NorthWind.Membership.Frontend.RazorViews.HttpMessageHandlers;
namespace Microsoft.Extensions.DependencyInjection;
public static class DependencyContainer
{
    public static IHttpClientBuilder AddMembershipBearerTokenHandler(this IHttpClientBuilder builder)
    {
        builder.Services.TryAddTransient<MembershipBearerTokenHandler>();
        return builder.AddHttpMessageHandler<MembershipBearerTokenHandler>();
    }

    public static IServiceCollection AddMembershipServices(this IServiceCollection services, Action<HttpClient> configureHttpClient)
    {
        services.AddAuthorizationCore();
        services.AddScoped<ITokenStorage, TokenLocalStorage>();
        services.AddScoped<JWTAuthenticationStateProvider>();
        services.AddScoped<AuthenticationStateProvider>(provider => provider.GetRequiredService<JWTAuthenticationStateProvider>());
        services.Configure<MembershipBearerTokenOptions>(options =>
        {
            using var client = new HttpClient();
            configureHttpClient(client);
            options.BackendAddress = client.BaseAddress;
        });
        services.AddExceptionDelegatingHandler();
        services.AddHttpClient<MembershipGateway>(configureHttpClient).AddHttpMessageHandler<ExceptionDelegatingHandler>();
        services.AddValidationService().AddMembershipValidators();
        services.AddScoped<UserRegistrationViewModel>();
        services.AddModelValidator<UserRegistrationViewModel, UserRegistrationViewModelDtoValidator>();
        services.AddModelValidator<UserRegistrationViewModel, UserRegistrationViewModelValidator>();
        services.AddScoped<UserLoginViewModel>();
        services.AddModelValidator<UserLoginViewModel, UserLoginViewModelDtoValidator>();
        return services;
    }
}
