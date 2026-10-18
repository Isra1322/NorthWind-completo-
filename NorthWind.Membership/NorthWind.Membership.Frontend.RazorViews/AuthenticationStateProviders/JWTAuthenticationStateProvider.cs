using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using NorthWind.Membership.Frontend.RazorViews.Interfaces;
namespace NorthWind.Membership.Frontend.RazorViews.AuthenticationStateProviders;
public class JWTAuthenticationStateProvider(ITokenStorage storage) : AuthenticationStateProvider
{
    internal static bool TryGetIdentity(TokensDto? tokens, out ClaimsIdentity identity)
    {
        identity = new ClaimsIdentity();
        if (string.IsNullOrWhiteSpace(tokens?.AccessToken)) return false;
        try
        {
            var handler = new JsonWebTokenHandler();
            if (!handler.CanReadToken(tokens.AccessToken)) return false;
            var token = handler.ReadJsonWebToken(tokens.AccessToken);
            if (token.ValidTo <= DateTime.UtcNow || token.ValidFrom > DateTime.UtcNow) return false;
            var nameType = token.Claims.Any(c => c.Type == ClaimTypes.Name) ? ClaimTypes.Name :
                token.Claims.Any(c => c.Type == "unique_name") ? "unique_name" : "name";
            identity = new ClaimsIdentity(token.Claims, nameof(JWTAuthenticationStateProvider), nameType, ClaimTypes.Role);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or Microsoft.IdentityModel.Tokens.SecurityTokenException) { return false; }
    }
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var tokens = await storage.GetTokensAsync();
        bool valid = TryGetIdentity(tokens, out var identity);
        if (!valid && tokens is not null) await storage.RemoveTokensAsync();
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
    public async ValueTask LoginAsync(TokensDto tokens)
    {
        await storage.StoreTokensAsync(tokens);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
    public async ValueTask LogoutAsync()
    {
        await storage.RemoveTokensAsync();
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
