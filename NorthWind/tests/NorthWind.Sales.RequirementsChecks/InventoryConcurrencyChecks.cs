using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NorthWind.Entities.Interfaces;
using NorthWind.Sales.Backend.BusinessObjects.Aggregates;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces.CreateOrder;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces.Repositories;
using NorthWind.Sales.Backend.Repositories.Interfaces;
using NorthWind.Sales.Entities.Dtos.CreateOrder;
using NorthWind.Exceptions.Entities.Exceptions;

internal static class InventoryConcurrencyChecks
{
    private static void ValidateName(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^NorthWindInventoryCheck_[0-9a-f]{32}$"))
            throw new ArgumentException("Only isolated inventory test databases are allowed.");
    }
    private static string Connection(string name) => $"Server=(localdb)\\mssqllocaldb;Database={name};Integrated Security=true;TrustServerCertificate=true";
    private static ServiceProvider BuildProvider(string name, MailProbe? mail = null)
    {
        ValidateName(name);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["SalesTax:Enabled"] = "true", ["SalesTax:Rate"] = "0.15" }).Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddLogging();
        services.AddNorthWindSalesServices(o =>
        {
            o.ConnectionString = Connection(name);
            o.DomainLogsConnectionString = Connection(name + "_Logs");
        }, _ => { }, o => o.ConnectionString = Connection(name), o =>
        {
            o.SecurityKey = new string('x', 96);
            o.ValidIssuer = "NorthWindSales";
            o.ValidAudience = "NorthWindSalesClient";
        }, true);
        services.AddSingleton<IMailService>(mail ?? new MailProbe());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
    public static async Task CreateFixture(string name)
    {
        using var provider = BuildProvider(name);
        using var scope = provider.CreateScope();
        await ((DbContext)scope.ServiceProvider.GetRequiredService<INorthWindSalesCommandsDataContext>()).Database.EnsureCreatedAsync();
        await ((DbContext)scope.ServiceProvider.GetRequiredService<INorthWindDomainLogsDataContext>()).Database.EnsureCreatedAsync();
        await Execute(name, "UPDATE Products SET UnitPrice=10, UnitsInStock=20; UPDATE Customers SET Name='Cliente de concurrencia';");
    }
    public static async Task DeleteFixture(string name)
    {
        using var provider = BuildProvider(name);
        using var scope = provider.CreateScope();
        SqlConnection.ClearAllPools();
        await ((DbContext)scope.ServiceProvider.GetRequiredService<INorthWindSalesCommandsDataContext>()).Database.EnsureDeletedAsync();
        await ((DbContext)scope.ServiceProvider.GetRequiredService<INorthWindDomainLogsDataContext>()).Database.EnsureDeletedAsync();
    }
    private static async Task<decimal> Execute(string name, string sql)
    {
        ValidateName(name);
        await using var connection = new SqlConnection(Connection(name));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        Console.WriteLine("PASS " + description);
    }
    private static CreateOrderDto Order(short quantity, params CreateOrderDetailDto[] extra) =>
        new("ALFKI", "Calle de prueba", "Quito", "Ecuador", "170101", new[] { new CreateOrderDetailDto(1, 10, quantity) }.Concat(extra));
    private static async Task<int> Sell(ServiceProvider provider, CreateOrderDto order)
    {
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>().Handle(order);
        return scope.ServiceProvider.GetRequiredService<ICreateOrderOutputPort>().OrderId;
    }
    public static async Task Run()
    {
        string name = "NorthWindInventoryCheck_" + Guid.NewGuid().ToString("N");
        var mail = new MailProbe();
        try
        {
            await CreateFixture(name);
            using var provider = BuildProvider(name, mail);
            await Sell(provider, Order(5));
            Check(await Execute(name, "SELECT UnitsInStock FROM Products WHERE Id=1") == 15 &&
                await Execute(name, "SELECT COUNT(*) FROM Orders") == 1, "Normal sale: 20 stock minus 5 leaves 15 and one complete order");
            await Execute(name, "UPDATE Products SET UnitsInStock=0 WHERE Id=1");
            bool rejected = false;
            try { await Sell(provider, Order(20)); }
            catch (InventoryConflictException ex)
            {
                rejected = ex.Errors.Any(e => e.Message.Contains("Stock disponible: 0") && e.Message.Contains("Cantidad solicitada: 20"));
            }
            Check(rejected && await Execute(name, "SELECT COUNT(*) FROM Orders") == 1,
                "Direct SQL stock change to zero rejects sale with real available/requested quantities");
            await Execute(name, "UPDATE Products SET UnitsInStock=20 WHERE Id=1");
            int recoveredId = await Sell(provider, Order(20));
            Check(await Execute(name, "SELECT UnitsInStock FROM Products WHERE Id=1") == 0 &&
                await Execute(name, "SELECT COUNT(*) FROM Orders") == 2, "Retry after restoring SQL stock succeeds exactly once and leaves zero stock");
            using (var scope = provider.CreateScope())
            {
                var invoice = await scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>().GetInvoice(recoveredId);
                Check(invoice is { Subtotal: 200m, DiscountAmount: 0m, TaxAmount: 30m, Total: 230m },
                    "Recovered sale applies 15% IVA without discount");
            }
            await Execute(name, "UPDATE Products SET UnitsInStock=10 WHERE Id=1");
            using (var first = provider.CreateScope())
            using (var second = provider.CreateScope())
            {
                var left = first.ServiceProvider.GetRequiredService<ICommandsRepository>();
                var right = second.ServiceProvider.GetRequiredService<ICommandsRepository>();
                await left.CreateOrder(OrderAggregate.From(Order(8)));
                await right.CreateOrder(OrderAggregate.From(Order(8)));
                async Task<bool> Save(ICommandsRepository repository)
                {
                    try { await repository.SaveChanges(); return true; }
                    catch (InventoryConflictException) { return false; }
                }
                var outcomes = await Task.WhenAll(Save(left), Save(right));
                Check(outcomes.Count(success => success) == 1 &&
                    await Execute(name, "SELECT UnitsInStock FROM Products WHERE Id=1") == 2 &&
                    await Execute(name, "SELECT COUNT(*) FROM Orders") == 3,
                    "Simultaneous sales of 8 from 10: only one succeeds, loser inserts nothing, stock remains 2");
            }
            await Execute(name, "UPDATE Products SET UnitsInStock=20");
            decimal details = await Execute(name, "SELECT COUNT(*) FROM OrderDetails");
            mail.Fail = true;
            bool rolledBack = false;
            try { await Sell(provider, Order(1, new(3, 10, 1), new(4, 10, 1), new(5, 10, 1))); }
            catch (InvalidOperationException ex) when (ex.Message == "Simulated SMTP failure") { rolledBack = true; }
            Check(rolledBack && await Execute(name, "SELECT COUNT(*) FROM Orders") == 3 &&
                await Execute(name, "SELECT COUNT(*) FROM OrderDetails") == details &&
                await Execute(name, "SELECT COUNT(*) FROM Products WHERE UnitsInStock<>20") == 0,
                "Failure after saving rolls back the entire order, all details and every stock deduction");
            mail.Fail = false;
            await Sell(provider, Order(1));
            Check(await Execute(name, "SELECT COUNT(*) FROM Orders") == 4 &&
                await Execute(name, "SELECT UnitsInStock FROM Products WHERE Id=1") == 19,
                "A fresh request succeeds after rollback without lingering locks or transaction state");
            Check(await Execute(name, "SELECT COUNT(*) FROM Products WHERE UnitsInStock<0") == 0, "No negative inventory");
        }
        finally { await DeleteFixture(name); }
        Console.WriteLine("All inventory checks passed; isolated databases deleted, real data untouched.");
    }
    private sealed class MailProbe : IMailService
    {
        public bool Fail { get; set; }
        public Task SendMailToAdministrator(string subject, string body) =>
            Fail ? throw new InvalidOperationException("Simulated SMTP failure") : Task.CompletedTask;
    }
}
