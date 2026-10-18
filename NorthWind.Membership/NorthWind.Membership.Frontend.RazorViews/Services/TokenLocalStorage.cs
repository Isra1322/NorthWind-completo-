using System.Text.Json;
using Microsoft.JSInterop;
using NorthWind.Membership.Frontend.RazorViews.Interfaces;
namespace NorthWind.Membership.Frontend.RazorViews.Services;
internal class TokenLocalStorage(IJSRuntime jsRuntime) : ITokenStorage
{
    private const string StorageKey = "tokens";
    public async Task StoreTokensAsync(TokensDto tokens) =>
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, JsonSerializer.Serialize(tokens));
    public async Task<TokensDto?> GetTokensAsync()
    {
        string? value = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (value is null) return null;
        try
        {
            var tokens = JsonSerializer.Deserialize<TokensDto>(value);
            if (tokens is null) await RemoveTokensAsync();
            return tokens;
        }
        catch (JsonException) { await RemoveTokensAsync(); return null; }
    }
    public async Task RemoveTokensAsync() => await jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
}
