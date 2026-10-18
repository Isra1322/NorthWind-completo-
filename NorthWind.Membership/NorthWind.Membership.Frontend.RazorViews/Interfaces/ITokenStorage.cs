namespace NorthWind.Membership.Frontend.RazorViews.Interfaces;
public interface ITokenStorage
{
    Task<TokensDto?> GetTokensAsync();
    Task StoreTokensAsync(TokensDto tokens);
    Task RemoveTokensAsync();
}
