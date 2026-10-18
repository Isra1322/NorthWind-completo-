using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NorthWind.Entities.Interfaces;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces.CreateOrder;
using NorthWind.Sales.Entities.Dtos.CreateOrder;

internal static class InvoiceEditingChecks
{
    public static async Task Run(IServiceProvider provider, string connectionString)
    {
        decimal Scalar(string sql)
        {
            using var connection = new SqlConnection(connectionString); connection.Open();
            using var command = new SqlCommand(sql, connection); return Convert.ToDecimal(command.ExecuteScalar());
        }
        string StringScalar(string sql)
        {
            using var connection = new SqlConnection(connectionString); connection.Open();
            using var command = new SqlCommand(sql, connection); return Convert.ToString(command.ExecuteScalar())!;
        }
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        using var scope = provider.CreateScope();
        var price1 = Scalar("SELECT UnitPrice FROM Products WHERE Id=1");
        var price3 = Scalar("SELECT UnitPrice FROM Products WHERE Id=3");
        decimal stock1 = Scalar("SELECT UnitsInStock FROM Products WHERE Id=1");
        decimal stock3 = Scalar("SELECT UnitsInStock FROM Products WHERE Id=3");
        string customerId = StringScalar("SELECT TOP (1) Id FROM Customers WHERE CurrentBalance = 0 ORDER BY Id");
        if (price1 <= 0 || price3 <= 0 || stock1 < 2 || stock3 < 2) throw new Exception("Se requiere stock y precio de prueba en los productos 1 y 3.");
        int invoiceId = 0;
        try
        {
            await scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>().Handle(
                new CreateOrderDto(customerId, "Calle de prueba", "Quito", "Ecuador", "170101", [new(1, price1, 1)]));
            invoiceId = scope.ServiceProvider.GetRequiredService<ICreateOrderOutputPort>().OrderId;
            var editor = scope.ServiceProvider.GetRequiredService<IInvoiceEditingService>();
            var version2 = await editor.UpdateAsync(invoiceId, new("Entrega editada", "Cuenca", "Ecuador", "010101", [new(1, 2)]), "test-user");
            var v1 = await editor.GetVersionAsync(invoiceId, 1);
            var v2 = await editor.GetVersionAsync(invoiceId, version2.VersionNumber);
            Check(version2.VersionNumber == 2 && v1?.Invoice.Id == invoiceId && v1.Invoice.Lines.Single().Quantity == 1,
                "Factura editada conserva número y snapshot original V1");
            Check(v1 is not null && v2 is not null && Encoding.Latin1.GetString(v1.PdfContent).Contains("Calle de prueba") &&
                v2.Invoice.Lines.Single().Quantity == 2 && Encoding.Latin1.GetString(v2.PdfContent).Contains("Versión 2"),
                "Edición guarda un PDF inmutable por versión con detalle actualizado");
            Check(Scalar("SELECT UnitsInStock FROM Products WHERE Id=1") == stock1 - 2,
                "Aumentar cantidad descuenta solo la cantidad neta de stock");
            var version3 = await editor.UpdateAsync(invoiceId, new("Entrega editada", "Cuenca", "Ecuador", "010101", [new(3, 2)]), "test-user");
            Check(version3.VersionNumber == 3 && Scalar("SELECT UnitsInStock FROM Products WHERE Id=1") == stock1 &&
                Scalar("SELECT UnitsInStock FROM Products WHERE Id=3") == stock3 - 2,
                "Reemplazar producto devuelve el anterior al inventario y descuenta el nuevo");
            Check((await editor.GetVersionAsync(invoiceId, 1))!.PdfContent.SequenceEqual(v1!.PdfContent),
                "La versión PDF anterior permanece intacta después de otra edición");
            bool rejected = false;
            try { await editor.UpdateAsync(invoiceId, new("Entrega editada", "Cuenca", "Ecuador", "010101", [new(3, short.MaxValue)]), "test-user"); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && (await editor.GetVersionsAsync(invoiceId)).Count == 3,
                "Edición sin stock suficiente se rechaza y no crea versión adicional");
        }
        finally
        {
            if (invoiceId > 0)
            {
                using var connection = new SqlConnection(connectionString); await connection.OpenAsync();
                using var command = new SqlCommand("DELETE FROM dbo.InvoiceVersions WHERE InvoiceId=@id; DELETE FROM dbo.OrderDetails WHERE OrderId=@id; DELETE FROM dbo.Orders WHERE Id=@id; UPDATE dbo.Products SET UnitsInStock=@stock1 WHERE Id=1; UPDATE dbo.Products SET UnitsInStock=@stock3 WHERE Id=3;", connection);
                command.Parameters.AddWithValue("@id", invoiceId); command.Parameters.AddWithValue("@stock1", stock1); command.Parameters.AddWithValue("@stock3", stock3);
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
