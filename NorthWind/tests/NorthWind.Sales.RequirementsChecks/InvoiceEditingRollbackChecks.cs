using System.Transactions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NorthWind.Sales.Backend.BusinessObjects.Aggregates;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces.Repositories;
using NorthWind.Sales.Entities.Dtos;

internal static class InvoiceEditingRollbackChecks
{
    public static async Task Run(ServiceProvider provider, string connectionString)
    {
        async Task<string> Snapshot()
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand("""
                SELECT (SELECT * FROM Orders ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES) Orders,
                  (SELECT * FROM OrderDetails ORDER BY OrderId,ProductId FOR JSON PATH, INCLUDE_NULL_VALUES) Details,
                  (SELECT * FROM InvoiceVersions ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES) Versions,
                  (SELECT Id,UnitsInStock,UnitPrice FROM Products ORDER BY Id FOR JSON PATH) Inventory
                FOR JSON PATH
                """, connection);
            using var reader = await command.ExecuteReaderAsync();
            var value = new System.Text.StringBuilder();
            while (await reader.ReadAsync()) value.Append(reader.GetString(0));
            return value.ToString();
        }
        void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        string before = await Snapshot();
        using (var rollback = new TransactionScope(TransactionScopeOption.RequiresNew,
            new TransactionOptions { IsolationLevel = IsolationLevel.Serializable }, TransactionScopeAsyncFlowOption.Enabled))
        using (var scope = provider.CreateScope())
        {
            var read = scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>();
            var product = (await read.SearchProducts("1", null, 1)).Items.Single(x => x.Id == "1");
            Check(product.UnitsInStock >= 2, "Existing product has stock for rollback-only invoice editing check");
            // Seed only within the outer rollback transaction; never commit or delete a fixture.
            var order = OrderAggregate.From(new("ANATR", "Calle Loja 101", "Quito", "Ecuador", "00101", [new(1, product.UnitPrice, 1)]));
            var repository = scope.ServiceProvider.GetRequiredService<ICommandsRepository>();
            await repository.CreateOrder(order);
            await repository.SaveChanges();
            var editor = scope.ServiceProvider.GetRequiredService<IInvoiceEditingService>();
            await editor.UpdateAsync(order.Id, new("Calle Bolívar #123", "Cuenca", "Ecuador", "00102", [new(1, 2)]), "test-user");
            var original = await editor.GetVersionAsync(order.Id, 1);
            var current = await editor.GetVersionAsync(order.Id, 2);
            Check(original?.Invoice.Lines.Single().Quantity == 1 && current?.Invoice.Lines.Single().Quantity == 2,
                "Invoice editing preserves original version and creates the next version with correct details");
            Check((await editor.GetProductForEditAsync(1))!.UnitsInStock == product.UnitsInStock - 2,
                "Invoice edit changes stock only by the quantity difference");
            var originalPdf = original!.PdfContent.ToArray();
            await editor.UpdateAsync(order.Id, new("Calle Loja 101", "Cuenca", "Ecuador", "00102", [new(1, 1)]), "test-user");
            Check((await editor.GetVersionAsync(order.Id, 1))!.PdfContent.SequenceEqual(originalPdf) &&
                (await editor.GetVersionsAsync(order.Id)).Count == 3,
                "Subsequent editing leaves the original PDF unchanged and keeps all versions");
            bool invalid = false;
            try { await editor.UpdateAsync(order.Id, new("+", "Puebla+++", "529455", "01A", [new(1, 1)]), "test-user"); }
            catch (ArgumentException) { invalid = true; }
            Check(invalid && (await editor.GetVersionsAsync(order.Id)).Count == 3,
                "Direct editing API validation rejects bad delivery data before any version is written");
            bool noStock = false;
            try { await editor.UpdateAsync(order.Id, new("Calle Loja 101", "Quito", "Ecuador", "00101", [new(1, short.MaxValue)]), "test-user"); }
            catch (InvalidOperationException) { noStock = true; }
            Check(noStock, "Invoice editing rejects quantities above available stock");
        }
        Check(await Snapshot() == before, "Rollback preserves every existing invoice, detail, version, historical PDF and inventory value");
    }
}
