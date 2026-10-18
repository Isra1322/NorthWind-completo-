using System.Text;
using System.Transactions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NorthWind.Sales.Backend.BusinessObjects.Aggregates;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces.CreateOrder;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces.Repositories;
using NorthWind.Sales.Backend.BusinessObjects.Specifications;
using NorthWind.Sales.Entities.Dtos.CreateOrder;

internal static class SpecialOrderChecks
{
    public static async Task Run(ServiceProvider provider, string connectionString, MailProbe mail)
    {
        async Task<decimal> Scalar(string sql)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            return Convert.ToDecimal(await command.ExecuteScalarAsync());
        }
        void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        var ids = new[] { 1, 3, 4, 10, 5 };
        string customerId;
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand("SELECT TOP(1) Id FROM Customers WHERE CurrentBalance=0 ORDER BY Id", connection);
            customerId = (string)(await command.ExecuteScalarAsync() ?? throw new Exception("No customer without debt available"));
        }
        var stock = new Dictionary<int, decimal>();
        var prices = new Dictionary<int, decimal>();
        foreach (int id in ids)
        {
            stock[id] = await Scalar($"SELECT UnitsInStock FROM Products WHERE Id={id}");
            prices[id] = await Scalar($"SELECT UnitPrice FROM Products WHERE Id={id}");
            Check(stock[id] > 0, $"Product {id} available for rollback-only tests");
        }
        var orders = await Scalar("SELECT COUNT(*) FROM Orders");
        var details = await Scalar("SELECT COUNT(*) FROM OrderDetails");
        foreach (bool smtpFails in new[] { false, true })
        for (int count = 1; count <= 5; count++)
        {
            using (var rollback = new TransactionScope(TransactionScopeOption.RequiresNew,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
            using (var scope = provider.CreateScope())
            {
                mail.Fail = smtpFails;
                int calls = mail.Calls;
                var dto = new CreateOrderDto(customerId, "Calle 123", "Quito", "Ecuador", "170101",
                    ids.Take(count).Select(id => new CreateOrderDetailDto(id, prices[id], 1)).ToArray())
                    { RequestId = Guid.NewGuid() };
                Check(new SpecialOrderSpecification().IsSatisfiedBy(OrderAggregate.From(dto)) == (count > 3),
                    $"Classification preserved for {count} products");
                var input = scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>();
                var output = scope.ServiceProvider.GetRequiredService<ICreateOrderOutputPort>();
                int id = await Microsoft.AspNetCore.Builder.CreateOrderController.CreateOrder(dto, input, output);
                Check(id > 0, $"Controller returns order number: {count} products, SMTP failure={smtpFails}");
                var invoice = await scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>().GetInvoice(id);
                Check(invoice is not null && invoice.Lines.Count == count && invoice.Lines.All(l => l.Quantity == 1),
                    $"Invoice contains exactly {count} complete details");
                Check(await Scalar($"SELECT COUNT(*) FROM OrderDetails WHERE OrderId={id}") == count,
                    "Every detail persisted in SQL Server");
                Check(invoice!.Subtotal == ids.Take(count).Sum(p => prices[p]) && invoice.TaxRate == .15m &&
                    invoice.TaxAmount == decimal.Round(invoice.Subtotal * .15m, 2, MidpointRounding.AwayFromZero) &&
                    invoice.Total == invoice.Subtotal + invoice.TaxAmount, "Subtotal, IVA 15% and total preserved");
                if (count == 4) Check(invoice.Subtotal == 210m && invoice.TaxAmount == 31.50m && invoice.Total == 241.50m,
                    "Reported case: 210.00 + 31.50 = 241.50");
                string pdf = Encoding.Latin1.GetString(scope.ServiceProvider.GetRequiredService<IInvoicePdfPresenter>().Render(invoice));
                Check(pdf.StartsWith("%PDF") && pdf.Contains("%%EOF") && invoice.Lines.All(l =>
                    pdf.Contains(l.ProductName.Replace("(", "\\(").Replace(")", "\\)"))),
                    $"PDF includes all {count} product names");
                Check(mail.Calls - calls == (count > 3 ? 1 : 0), "Only special orders raise email notification");
                using var retryScope = provider.CreateScope();
                int retryId = await Microsoft.AspNetCore.Builder.CreateOrderController.CreateOrder(dto,
                    retryScope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>(),
                    retryScope.ServiceProvider.GetRequiredService<ICreateOrderOutputPort>());
                Check(retryId == id && await Scalar("SELECT COUNT(*) FROM Orders") == orders + 1 &&
                    mail.Calls - calls == (count > 3 ? 1 : 0), "Repeated request recovers the same order without duplicate sale or email");
                Check(await retryScope.ServiceProvider.GetRequiredService<ICommandsRepository>().FindOrderId(dto.RequestId!.Value) == id,
                    "Lost HTTP response can be recovered from the persisted request reference");
                foreach (int product in ids)
                    Check(await Scalar($"SELECT UnitsInStock FROM Products WHERE Id={product}") ==
                        stock[product] - (ids.Take(count).Contains(product) ? 1 : 0), $"Product {product}: inventory decremented exactly once");
            }
        }
        // Fail after SQL writes to prove the real transaction rolls back the entire sale.
        using (var scope = provider.CreateScope())
        {
            var original = scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>();
            var input = (ICreateOrderInputPort)ActivatorUtilities.CreateInstance(scope.ServiceProvider, original.GetType(),
                new FailAfterSave(scope.ServiceProvider.GetRequiredService<ICommandsRepository>()));
            bool failed = false;
            try
            {
                await input.Handle(new(customerId, "Calle 123", "Quito", "Ecuador", "170101",
                    ids.Select(id => new CreateOrderDetailDto(id, prices[id], 1)).ToArray()));
            }
            catch (InvalidOperationException e) when (e.Message == "Failure after SQL save") { failed = true; }
            Check(failed, "Injected failure after SQL SaveChanges exercises transaction rollback");
        }
        Check(await Scalar("SELECT COUNT(*) FROM Orders") == orders && await Scalar("SELECT COUNT(*) FROM OrderDetails") == details,
            "All test sales rolled back; existing orders and details unchanged");
        foreach (int product in ids)
            Check(await Scalar($"SELECT UnitsInStock FROM Products WHERE Id={product}") == stock[product],
                $"Existing inventory unchanged for product {product}");
        mail.Fail = false;
    }

    private sealed class FailAfterSave(ICommandsRepository inner) : ICommandsRepository
    {
        public Task CreateOrder(OrderAggregate order) => inner.CreateOrder(order);
        public Task<int?> FindOrderId(Guid requestId, bool lockRequest = false) => inner.FindOrderId(requestId, lockRequest);
        public async Task SaveChanges()
        {
            await inner.SaveChanges();
            throw new InvalidOperationException("Failure after SQL save");
        }
    }
}
