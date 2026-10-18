using System.Net.Http.Json;
namespace NorthWind.Membership.Frontend.RazorViews.WebApiGateways;
public class MembershipGateway(HttpClient client)
{
    public async Task<TokensDto> LoginAsync(UserCredentialsDto userCredentials)
    {
        using var response = await client.PostAsJsonAsync(NorthWind.Membership.Entities.Endpoints.Login, userCredentials);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TokensDto>()
            ?? throw new InvalidOperationException("No se recibio el token de acceso.");
    }
    public async Task RegisterAsync(UserRegistrationDto userData)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(NorthWind.Membership.Entities.Endpoints.Register, userData);
        response.EnsureSuccessStatusCode();
    }
}
