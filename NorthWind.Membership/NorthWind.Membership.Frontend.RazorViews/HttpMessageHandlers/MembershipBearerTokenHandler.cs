using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using NorthWind.Membership.Frontend.RazorViews.Interfaces;
using NorthWind.Membership.Frontend.RazorViews.AuthenticationStateProviders;
namespace NorthWind.Membership.Frontend.RazorViews.HttpMessageHandlers;
internal class MembershipBearerTokenOptions { public Uri? BackendAddress { get; set; } }
internal class MembershipBearerTokenHandler(ITokenStorage storage, IOptions<MembershipBearerTokenOptions> options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var allowed = options.Value.BackendAddress;
        var destination = request.RequestUri;
        if (allowed is not null && destination is not null &&
            string.Equals(allowed.Scheme, destination.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(allowed.Host, destination.Host, StringComparison.OrdinalIgnoreCase) &&
            allowed.Port == destination.Port && destination.AbsolutePath.StartsWith(allowed.AbsolutePath, StringComparison.Ordinal))
        {
            var tokens = await storage.GetTokensAsync();
            if (JWTAuthenticationStateProvider.TryGetIdentity(tokens, out _))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
            else if (tokens is not null) await storage.RemoveTokensAsync();
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
