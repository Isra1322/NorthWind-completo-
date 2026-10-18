using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces;

internal static class HistoricalInvoiceChecks
{
    public static async Task Run(ServiceProvider provider, string connectionString)
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        async Task<string> Snapshot()
        {
            await using var command = new SqlCommand("""
                SELECT
                  (SELECT * FROM Orders ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES) AS Orders,
                  (SELECT * FROM OrderDetails ORDER BY OrderId, ProductId FOR JSON PATH, INCLUDE_NULL_VALUES) AS Details,
                  (SELECT Id, UnitPrice, UnitsInStock FROM Products ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES) AS Inventory
                FOR JSON PATH
                """, connection);
            using var reader = await command.ExecuteReaderAsync();
            var json = new StringBuilder();
            while (await reader.ReadAsync()) json.Append(reader.GetString(0));
            return json.ToString();
        }
        string before = await Snapshot();
        var originals = new List<(int Id, decimal Subtotal, decimal? Rate, decimal? Tax, decimal Total)>();
        await using (var command = new SqlCommand("""
            SELECT o.Id, COALESCE(o.Subtotal, d.Amount, 0), o.TaxRate, o.TaxAmount,
                   COALESCE(o.Total, COALESCE(o.Subtotal, d.Amount, 0) + COALESCE(o.TaxAmount, 0))
            FROM Orders o
            OUTER APPLY (SELECT SUM(UnitPrice * Quantity) Amount FROM OrderDetails WHERE OrderId=o.Id) d
            ORDER BY o.Id
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                originals.Add((reader.GetInt32(0), reader.GetDecimal(1), reader.IsDBNull(2) ? null : reader.GetDecimal(2),
                    reader.IsDBNull(3) ? null : reader.GetDecimal(3), reader.GetDecimal(4)));
        using var scope = provider.CreateScope();
        var read = scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>();
        var presenter = scope.ServiceProvider.GetRequiredService<IInvoicePdfPresenter>();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(1252);
        var spanish = System.Globalization.CultureInfo.GetCultureInfo("es-ES");
        var listed = new Dictionary<int, decimal>();
        int page = 1;
        while (true)
        {
            var result = await read.SearchInvoices(null, page);
            foreach (var item in result.Items) listed.Add(item.Id, item.Total);
            if (page++ >= result.TotalPages) break;
        }
        Check(listed.Count == originals.Count, "Pagination includes all existing invoices exactly once");
        foreach (var original in originals)
        {
            var invoice = (await read.GetInvoice(original.Id))!;
            string pdf = encoding.GetString(presenter.Render(invoice));
            Check(invoice.Subtotal == original.Subtotal && invoice.Total == original.Total && listed[original.Id] == original.Total &&
                pdf.Contains($"(${original.Total.ToString("N2", spanish)})") && !pdf.Contains("$$") &&
                pdf.Contains($"({original.Subtotal.ToString("N2", spanish)})"),
                $"Invoice {original.Id}: original subtotal and total preserved in query, listing and PDF");
            Check(!pdf.Contains("(Descuento)") && !pdf.Contains("(Base imponible)") && pdf.Contains("(TOTAL A PAGAR)"),
                $"Invoice {original.Id}: uniform three-row summary");
            if (original.Tax is { } tax)
                Check(invoice.TaxRecorded && invoice.TaxAmount == tax && invoice.TaxRateRecorded == original.Rate.HasValue &&
                    pdf.Contains($"({tax.ToString("N2", spanish)})") && !pdf.Contains("IVA histórico no disponible"),
                    $"Invoice {original.Id}: stored IVA preserved, including explicit zero");
            else
                Check(!invoice.TaxRecorded && pdf.Contains("IVA histórico no disponible") && !pdf.Contains(@"(IVA \("),
                    $"Invoice {original.Id}: unknown IVA is disclosed without inventing a rate");
        }
        Check(await Snapshot() == before, "All original orders, details, prices, discounts and inventory remain byte-for-byte unchanged");
        Console.WriteLine($"Verified {originals.Count} existing invoices: {originals.Count(x => x.Tax.HasValue)} with recorded IVA, {originals.Count(x => !x.Tax.HasValue)} without IVA. PDFs generated only in memory.");
    }
}
