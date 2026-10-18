using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NorthWind.Sales.BlazorClient;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddNorthWindSalesServices(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["WebApiAddress"]!);
}, membershipHttpClient =>
{
    membershipHttpClient.BaseAddress = new Uri(builder.Configuration["WebApiAddress"]!);
}, httpClientBuilder => httpClientBuilder.AddMembershipBearerTokenHandler());

await builder.Build().RunAsync();