using System.Text;
using System.Transactions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NorthWind.Entities.Interfaces;
using NorthWind.Exceptions.Entities.Exceptions;
using NorthWind.Sales.Backend.BusinessObjects.Aggregates;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces.CreateOrder;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces.Repositories;
using NorthWind.Sales.Entities.Dtos.CreateOrder;
using Microsoft.Extensions.Options;
using NorthWind.Sales.Backend.BusinessObjects.ValueObjects;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NorthWind.Membership.Entities.Dtos.UserRegistration;
using NorthWind.Membership.Entities.Dtos.UserLogin;

if (args.Contains("--registration-lengths-only"))
{
    await RegistrationLengthChecks.Run();
    return;
}

if (args.Contains("--live-search-only"))
{
    await LiveSearchChecks.Run();
    return;
}

if (args.Contains("--customer-only"))
{
    await CustomerChecks.Run();
    return;
}

if (args.Contains("--inventory-concurrency-only"))
{
    await InventoryConcurrencyChecks.Run();
    return;
}
if (args.Length == 2 && args[0] == "--inventory-fixture-create")
{
    await InventoryConcurrencyChecks.CreateFixture(args[1]);
    return;
}
if (args.Length == 2 && args[0] == "--inventory-fixture-delete")
{
    await InventoryConcurrencyChecks.DeleteFixture(args[1]);
    return;
}

if (args.Contains("--invoice-calculation-only"))
{
    InvoiceCalculationChecks.Run();
    return;
}
if (args.Contains("--progressive-validation-only"))
{
    await ProgressiveValidationChecks.Run();
    return;
}

if (args.Contains("--registration-validation-only"))
{
    await RegistrationValidationChecks.Run();
    return;
}

if (args.Contains("--order-form-only"))
{
    await OrderFormChecks.Run();
    await DraftCancellationChecks.Run();
    return;
}

if (args.Contains("--membership-form-only"))
{
    await MembershipFormChecks.Run();
    return;
}

const string connectionString = "Server=(localdb)\\mssqllocaldb;Database=NorthWindDB;Integrated Security=true;TrustServerCertificate=true";
if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("SQL Server transaction checks require Windows.");
TransactionManager.ImplicitDistributedTransactions = true;
var configuration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "sales-appsettings.json")).Build();
var services = new ServiceCollection();
string jwtKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(48));
services.AddSingleton<IConfiguration>(configuration);
services.AddLogging();
services.AddNorthWindSalesServices(o =>
{
    o.ConnectionString = connectionString;
    o.DomainLogsConnectionString = connectionString.Replace("NorthWindDB", "NorthWindLogsDB");
}, _ => { }, o => o.ConnectionString = connectionString.Replace("NorthWindDB", "NorthWindUsersDB"), o =>
{
    o.SecurityKey = jwtKey;
    o.ValidIssuer = "NorthWindSales";
    o.ValidAudience = "NorthWindSalesClient";
    o.ExpireInMinutes = 60;
}, true);
var mail = new MailProbe();
services.AddSingleton<IMailService>(mail);
using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

if (args.Contains("--historical-invoices-only"))
{
    await HistoricalInvoiceChecks.Run(provider, connectionString);
    return;
}

if (args.Contains("--invoice-editing-only"))
{
    await InvoiceEditingChecks.Run(provider, connectionString);
    return;
}

if (args.Contains("--special-orders-only"))
{
    await SpecialOrderChecks.Run(provider, connectionString, mail);
    await OrderRecoveryChecks.Run();
    return;
}
if (args.Contains("--form-search-only"))
{
    await FormSearchChecks.Run(provider, connectionString);
    return;
}
if (args.Contains("--invoice-editing-rollback-only"))
{
    await InvoiceEditingRollbackChecks.Run(provider, connectionString);
    return;
}

async Task<decimal> Scalar(string sql)
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new SqlCommand(sql, connection);
    return Convert.ToDecimal(await command.ExecuteScalarAsync());
}
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine("PASS " + message);
}
TransactionScope RollbackScope() => new(TransactionScopeOption.RequiresNew,
    new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled);
CreateOrderDto Dto(params CreateOrderDetailDto[] details) =>
    new("ALFKI", "Calle 123", "Quito", "Ecuador", "170101", details);
var baselineOrders = await Scalar("SELECT COUNT(*) FROM Orders");
var baselineStock = await Scalar("SELECT UnitsInStock FROM Products WHERE Id=1");
var baselineDetails = await Scalar("SELECT COUNT(*) FROM OrderDetails");
var baselineCustomers = await Scalar("SELECT COUNT(*) FROM Customers");
var price = await Scalar("SELECT UnitPrice FROM Products WHERE Id=1");
Check(price > 0 && baselineStock >= 2, "Existing catalog has a priced product for transactional checks");

using (var rollback = RollbackScope())
using (var scope = provider.CreateScope())
{
    // Temporary master data is rolled back with the test order; no production examples are inserted.
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new SqlCommand("""
        UPDATE Customers SET Address=N'Dirección maestra de prueba', City=N'Cuenca',
            Country=N'Ecuador', PostalCode=N'010101' WHERE Id='ALFKI'
        """, connection);
    await command.ExecuteNonQueryAsync();
    var read = scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>();
    var customer = (await read.SearchCustomers("ALFKI", null, 1)).Items.Single();
    Check(customer is { Address: "Dirección maestra de prueba", City: "Cuenca", Country: "Ecuador", PostalCode: "010101" },
        "SQL customer lookup includes all delivery defaults in its existing paginated response");
    await scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>().Handle(
        new("ALFKI", "Entrega alternativa", "Quito", "Ecuador", "170101", [new(1, price, 1)]));
    int id = scope.ServiceProvider.GetRequiredService<ICreateOrderOutputPort>().OrderId;
    var invoice = await read.GetInvoice(id);
    Check(invoice is { ShipAddress: "Entrega alternativa", ShipCity: "Quito", ShipCountry: "Ecuador", ShipPostalCode: "170101" },
        "Saved order and invoice preserve all edited delivery fields");
    Check((await read.SearchCustomers("ALFKI", null, 1)).Items.Single() == customer,
        "Saving an order never overwrites the customer master address or contact data");
    string pdf = Encoding.Latin1.GetString(scope.ServiceProvider.GetRequiredService<IInvoicePdfPresenter>().Render(invoice!));
    Check(pdf.Contains("Entrega alternativa") && pdf.Contains("Quito") && pdf.Contains("170101"),
        "Saved invoice PDF uses the order-specific delivery address");
}

using (var rollback = RollbackScope())
using (var scope = provider.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>().Handle(Dto(new CreateOrderDetailDto(1, price, 2)));
    int id = scope.ServiceProvider.GetRequiredService<ICreateOrderOutputPort>().OrderId;
    var invoice = await scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>().GetInvoice(id);
    Check(invoice is not null && invoice.Lines.Count == 1 && invoice.Lines[0].Quantity == 2, "Order and invoice lines persisted together");
    Check(invoice!.Subtotal == price * 2 && invoice.TaxRate == .15m &&
        invoice.TaxAmount == decimal.Round(invoice.Subtotal * .15m, 2, MidpointRounding.AwayFromZero) &&
        invoice.Total == invoice.Subtotal + invoice.TaxAmount && invoice.DiscountAmount == 0,
        "Configured IVA and persisted totals are correct without discount");
    Check(await Scalar("SELECT UnitsInStock FROM Products WHERE Id=1") == baselineStock - 2, "Sale decrements stock exactly once");
    var pdf = scope.ServiceProvider.GetRequiredService<IInvoicePdfPresenter>().Render(invoice);
    Check(Encoding.Latin1.GetString(pdf).StartsWith("%PDF-1.4") && Encoding.Latin1.GetString(pdf).EndsWith("%%EOF\n"), "Invoice presenter produces PDF bytes");
    var longInvoice = invoice with { Lines = Enumerable.Range(1, 100).Select(i => invoice.Lines[0] with { ProductId = i }).ToArray() };
    var longPdf = Encoding.Latin1.GetString(scope.ServiceProvider.GetRequiredService<IInvoicePdfPresenter>().Render(longInvoice));
    var pageCount = System.Text.RegularExpressions.Regex.Match(longPdf, @"/Type /Pages /Count (\d+)");
    Check(pageCount.Success && int.Parse(pageCount.Groups[1].Value) > 1 && longPdf.Contains("(100)"), "Long invoices paginate PDF output without dropping the last product");
    var tax = scope.ServiceProvider.GetRequiredService<IOptions<SalesTaxOptions>>().Value;
    tax.Rate = .20m;
    var saved = await scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>().GetInvoice(id);
    Check(saved is not null && saved with { Lines = invoice.Lines } == invoice && saved.Lines.SequenceEqual(invoice.Lines),
        "Saved invoice retains its IVA rate and all amounts after configuration changes");
    Check(scope.ServiceProvider.GetRequiredService<IInvoicePdfPresenter>().Render(saved!).SequenceEqual(pdf),
        "Saved PDF retains all original amounts after configuration changes");
    tax.Rate = .15m;
}
using (var rollback = RollbackScope())
using (var scope = provider.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>().Handle(
        new("D0001", "Entrega ficticia", "Loja", "Ecuador", "110101", [new(1, price, 1)]));
    int id = scope.ServiceProvider.GetRequiredService<ICreateOrderOutputPort>().OrderId;
    var invoice = await scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>().GetInvoice(id);
    var invoiceListing = await scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>()
        .FilterInvoices(new("D0001", "", ""), 1);
    Check(invoiceListing.Items.Single(item => item.Id == id).CustomerName == "María Fernanda López",
        "Invoice listing displays the current natural customer name without changing invoice values");
    Check(invoice is { CustomerId: "D0001", CustomerIdentification: "0910000041", CustomerName: "María Fernanda López" },
        "A seeded demonstration customer can create an order using the preserved internal foreign key");
}
Check(await Scalar("SELECT COUNT(*) FROM Orders") == baselineOrders &&
    await Scalar("SELECT COUNT(*) FROM OrderDetails") == baselineDetails &&
    await Scalar("SELECT UnitsInStock FROM Products WHERE Id=1") == baselineStock, "Test rollback preserves original orders, details and stock");

using (var rollback = RollbackScope())
using (var scope = provider.CreateScope())
{
    int productId;
    await using (var connection = new SqlConnection(connectionString))
    {
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT Products(Name,UnitsInStock,UnitPrice) OUTPUT INSERTED.Id VALUES('IVA verification',10,50)", connection);
        productId = Convert.ToInt32(await command.ExecuteScalarAsync());
    }
    await scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>()
        .Handle(Dto(new CreateOrderDetailDto(productId, 50m, 2)));
    int id = scope.ServiceProvider.GetRequiredService<ICreateOrderOutputPort>().OrderId;
    var invoice = await scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>().GetInvoice(id);
    Check(invoice is { Subtotal: 100m, DiscountAmount: 0m, TaxableBase: 100m, TaxRate: .15m,
        TaxAmount: 15m, Total: 115m }, "Exact 100.00 example survives saving and reading SQL Server invoice");
    Check(await Scalar($"SELECT Subtotal FROM Orders WHERE Id={id}") == 100m &&
        await Scalar($"SELECT TaxRate FROM Orders WHERE Id={id}") == .15m &&
        await Scalar($"SELECT TaxAmount FROM Orders WHERE Id={id}") == 15m &&
        await Scalar($"SELECT Total FROM Orders WHERE Id={id}") == 115m &&
        await Scalar($"SELECT Discount FROM Orders WHERE Id={id}") == 0m,
        "Database columns store exact 15% example amounts");
    var pdf = Encoding.Latin1.GetString(scope.ServiceProvider.GetRequiredService<IInvoicePdfPresenter>().Render(invoice!));
    Check(new[] { "(100,00)", "(15,00)", "($115,00)" }.All(pdf.Contains) &&
        !pdf.Contains("(Descuento)") && !pdf.Contains("(Base imponible)"),
        "PDF from saved SQL invoice matches exact 100.00 example");
    var summary = (await scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>()
        .SearchInvoices(id.ToString(), 1)).Items.Single(x => x.Id == id);
    Check(summary.Total == invoice!.Total, "Paginated listing preserves recorded 15% total");
}

using (var rollback = RollbackScope())
using (var scope = provider.CreateScope())
{
    int productId;
    await using (var connection = new SqlConnection(connectionString))
    {
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "INSERT Products(Name,UnitsInStock,UnitPrice) OUTPUT INSERTED.Id VALUES('Chai verification',10,35)", connection);
        productId = Convert.ToInt32(await command.ExecuteScalarAsync());
    }
    await scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>()
        .Handle(Dto(new CreateOrderDetailDto(productId, 35m, 1)));
    int id = scope.ServiceProvider.GetRequiredService<ICreateOrderOutputPort>().OrderId;
    var read = scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>();
    var invoice = await read.GetInvoice(id);
    Check(invoice is { Subtotal: 35m, TaxAmount: 5.25m, Total: 40.25m, DiscountAmount: 0m },
        "Chai example persists 35.00 subtotal, 5.25 IVA and 40.25 total in SQL Server");
    Check((await read.SearchInvoices(id.ToString(), 1)).Items.Single(x => x.Id == id).Total == 40.25m,
        "Chai invoice listing matches saved total");
    var pdf = Encoding.Latin1.GetString(scope.ServiceProvider.GetRequiredService<IInvoicePdfPresenter>().Render(invoice!));
    Check(new[] { "(35,00)", "(5,25)", "($40,25)" }.All(pdf.Contains) &&
        !pdf.Contains("(Descuento)") && !pdf.Contains("(Base imponible)"), "Saved Chai PDF matches all three amounts");

    // Simulate legacy records only on this rollback-only test order, never on an existing invoice.
    await using var legacyConnection = new SqlConnection(connectionString);
    await legacyConnection.OpenAsync();
    await using var legacyCommand = new SqlCommand(
        "UPDATE Orders SET Subtotal=100, TaxRate=.15, TaxAmount=13.50, Total=103.50, Discount=10 WHERE Id=@id", legacyConnection);
    legacyCommand.Parameters.AddWithValue("@id", id);
    await legacyCommand.ExecuteNonQueryAsync();
    var legacy = await read.GetInvoice(id);
    Check(legacy is { Subtotal: 100m, TaxAmount: 13.50m, Total: 103.50m, DiscountAmount: 10m, TaxRecorded: true },
        "Historical recorded discount and IVA are read unchanged from SQL");
    legacyCommand.CommandText = "UPDATE Orders SET Subtotal=35, TaxRate=NULL, TaxAmount=NULL, Total=35 WHERE Id=@id";
    await legacyCommand.ExecuteNonQueryAsync();
    legacy = await read.GetInvoice(id);
    var legacyPdf = Encoding.Latin1.GetString(scope.ServiceProvider.GetRequiredService<IInvoicePdfPresenter>().Render(legacy!));
    Check(legacy is { Total: 35m, TaxRecorded: false } && legacyPdf.Contains("IVA histórico no disponible") &&
        !legacyPdf.Contains(@"(IVA \(") && !legacyPdf.Contains("($40,25)"),
        "Historical missing IVA retains stored total and PDF does not invent tax");
    legacyCommand.CommandText = "UPDATE Orders SET Subtotal=NULL, Total=NULL WHERE Id=@id";
    await legacyCommand.ExecuteNonQueryAsync();
    legacy = await read.GetInvoice(id);
    Check(legacy is { Subtotal: 35m, Total: 35m, TaxRecorded: false } &&
        (await read.SearchInvoices(id.ToString(), 1)).Items.Single(x => x.Id == id).Total == 35m,
        "Pre-migration invoice uses original detail prices without applying current IVA");
}

using (var rollback = RollbackScope())
using (var scope = provider.CreateScope())
{
    string customerId = "Z" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
    string customerName = "InvoiceCheck" + Guid.NewGuid().ToString("N");
    var ids = new List<int>();
    await using (var connection = new SqlConnection(connectionString))
    {
        await connection.OpenAsync();
        await using var customer = new SqlCommand("INSERT Customers(Id,Name,CurrentBalance) VALUES(@id,@name,0)", connection);
        customer.Parameters.AddWithValue("@id", customerId);
        customer.Parameters.AddWithValue("@name", customerName);
        await customer.ExecuteNonQueryAsync();
        for (int i = 0; i < 23; i++)
        {
            await using var order = new SqlCommand("""
                INSERT Orders(CustomerId,ShipAddress,ShipCity,ShipCountry,OrderDate,ShippingType,DiscountType,Discount,Subtotal,TaxRate,TaxAmount,Total)
                OUTPUT INSERTED.Id VALUES(@customer,'Test','Quito','Ecuador',@date,3,1,10,100,0.15,13.50,103.50)
                """, connection);
            order.Parameters.AddWithValue("@customer", customerId);
                order.Parameters.AddWithValue("@date", new DateTime(2020, 1, 1).AddMinutes(i / 2));
            ids.Add(Convert.ToInt32(await order.ExecuteScalarAsync()));
        }
    }
    var read = scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>();
    var first = await read.SearchInvoices(customerName[4..], 1);
    var second = await read.SearchInvoices(customerName, 2);
    var third = await read.SearchInvoices(customerName, 3);
    Check(first.TotalCount == 23 && first.TotalPages == 3 && first.Items.Count == 10 &&
        second.Items.Count == 10 && third.Items.Count == 3, "Invoice search uses real SQL pagination with ten rows per page");
    Check(first.Items.Concat(second.Items).Concat(third.Items).Select(x => x.Id).SequenceEqual(ids.AsEnumerable().Reverse()),
        "Invoice pages sort newest first with deterministic ID order for equal dates");
    Check(first.Items.All(x => x.CustomerId == customerId && x.CustomerName == customerName && x.Total == 103.50m),
        "Invoice projection includes real customer identification, name and recorded totals");
    Check((await read.SearchInvoices(customerId[1..], 1)).Items.Any(x => x.CustomerId == customerId),
        "Invoice search matches partial customer identification");
    Check((await read.SearchInvoices(ids[0].ToString(), 1)).Items.Any(x => x.Id == ids[0]),
        "Invoice search matches invoice number");
    Check((await read.SearchInvoices(customerName, 999)).Page == 3 && (await read.SearchInvoices(customerName, -1)).Page == 1,
        "Invoice pagination bounds invalid page numbers");
    var byClient = await read.FilterInvoices(new(customerId[1..].ToLowerInvariant(), "", ""), 1);
    var byInvoice = await read.FilterInvoices(new("", ids[0].ToString(), ""), 1);
    var byDate = await read.FilterInvoices(new("", "", "2020-01-01"), 2);
    var combined = await read.FilterInvoices(new(customerId[1..].ToLowerInvariant(), ids[0].ToString(), "2020-01-01"), 1);
    var unmatched = await read.FilterInvoices(new(customerId, ids[0].ToString(), "2020-01-02"), 1);
    Check(byClient.TotalCount == 23 && byClient.Items.All(x => x.CustomerId == customerId),
        "Structured customer ID filter returns only partial, case-insensitive ID matches");
    Check(byInvoice is { TotalCount: 1, Items.Count: 1 } && byInvoice.Items[0].Id == ids[0],
        "Structured invoice number filter returns one exact invoice");
    Check(byDate is { TotalCount: 23, TotalPages: 3, Page: 2, Items.Count: 10 } &&
        byDate.Items.All(x => x.OrderDate.Date == new DateTime(2020, 1, 1)),
        "Structured date filter applies the whole day before SQL pagination");
    Check(combined is { TotalCount: 1, Items.Count: 1 } && combined.Items[0].Id == ids[0] && unmatched.TotalCount == 0,
        "Combined invoice filters use AND semantics and return no mismatched row");
    Check((await read.SearchInvoices(Guid.NewGuid().ToString("N"), 1)) is { TotalCount: 0, Page: 1, TotalPages: 1, Items.Count: 0 },
        "Invoice query returns an empty page for unmatched search");
}
Check(await Scalar("SELECT COUNT(*) FROM Orders") == baselineOrders &&
    await Scalar("SELECT COUNT(*) FROM Customers") == baselineCustomers,
    "Invoice listing tests roll back every temporary invoice and customer");
using (var scope = provider.CreateScope())
{
    var read = scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>();
    var listed = await read.SearchInvoices(null, 1);
    Check(listed.TotalCount == baselineOrders && listed.Items.Count <= 10, "Unfiltered listing contains only saved database orders");
    var customerFirstPage = await read.SearchCustomersByIdentification(null, null, 1);
    var cedulaMatch = await read.SearchCustomersByIdentification("0910000017", null, 1);
    var updatedDemoName = await read.SearchCustomersByIdentification("0910000041", null, 1);
    var nameMatch = await read.SearchCustomersByIdentification(null, "cOMERCIAL alfa", 1);
    var combinedMatch = await read.SearchCustomersByIdentification("0910000017", "Comercial Alfa", 1);
    Check(customerFirstPage.Items.Count == 5 && customerFirstPage.TotalPages == 4,
        "Customer lookup returns five rows per page for the 20 seeded customers");
    Check(cedulaMatch.Items.Count == 1 && cedulaMatch.Items[0].Id == "ALFKI" &&
        cedulaMatch.Items[0].Identification == "0910000017",
        "Partial/complete cedula lookup returns the matching customer while preserving its internal key");
    Check(updatedDemoName.Items.Single().Name == "María Fernanda López",
        "Customer lookup immediately returns the updated natural demonstration name");
    Check(nameMatch.Items.Count == 1 && nameMatch.Items[0].Id == "ALFKI" &&
        combinedMatch.Items.Count == 1 && combinedMatch.Items[0].Id == "ALFKI",
        "Customer name lookup is case-insensitive and combines with cedula using AND");
    foreach (var summary in listed.Items)
        Check((await read.GetInvoice(summary.Id))!.Total == summary.Total,
            $"Listing total matches stored or historical invoice {summary.Id}");
}

foreach (var (name, dto) in new (string, CreateOrderDto)[]
{
    ("Duplicate products", Dto(new CreateOrderDetailDto(1, price, 1), new(1, price, 1))),
    ("Insufficient stock", Dto(new CreateOrderDetailDto(1, price, (short)(baselineStock + 1)))),
    ("Out of stock", Dto(new CreateOrderDetailDto(2, 1, 1))),
    ("Zero quantity", Dto(new CreateOrderDetailDto(1, price, 0))),
    ("Negative quantity", Dto(new CreateOrderDetailDto(1, price, -1))),
    ("Unknown product", Dto(new CreateOrderDetailDto(int.MaxValue, price, 1))),
    ("Unknown customer", new("ZZZZZ", "Calle 123", "Quito", "Ecuador", "", [new(1, price, 1)])),
    ("Customer with debt", new("DEBT1", "Calle 123", "Quito", "Ecuador", "", [new(1, price, 1)])),
    ("Tampered price", Dto(new CreateOrderDetailDto(1, price + 1, 1))),
    ("Invalid city", new("ALFKI", "Calle 123", "123", "Ecuador", "", [new(1, price, 1)])),
    ("Missing address", new("ALFKI", "", "Quito", "Ecuador", "", [new(1, price, 1)])),
    ("Address over 60", new("ALFKI", new string('A', 61), "Quito", "Ecuador", "", [new(1, price, 1)])),
    ("City over 15", new("ALFKI", "Calle", new string('A', 16), "Ecuador", "", [new(1, price, 1)])),
    ("Country over 15", new("ALFKI", "Calle", "Quito", new string('A', 16), "", [new(1, price, 1)])),
    ("Postal code over 10", new("ALFKI", "Calle", "Quito", "Ecuador", "01234567890", [new(1, price, 1)])),
    ("Postal letters", new("ALFKI", "Calle", "Quito", "Ecuador", "01A2", [new(1, price, 1)])),
    ("Postal spaces", new("ALFKI", "Calle", "Quito", "Ecuador", " ", [new(1, price, 1)])),
    ("Postal notation", new("ALFKI", "Calle", "Quito", "Ecuador", "-1e+2.3,4", [new(1, price, 1)])),
    ("Non-ASCII digits", new("ALFKI", "Calle", "Quito", "Ecuador", "٠١٢", [new(1, price, 1)]))
})
{
    bool rejected = false;
    using (var rollback = RollbackScope())
    using (var scope = provider.CreateScope())
    {
        if (name == "Customer with debt")
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var insertDebtCustomer = new SqlCommand(
                "INSERT INTO Customers(Id,Name,CurrentBalance) VALUES(N'DEBT1',N'Cliente temporal con saldo',275.50)", connection);
            await insertDebtCustomer.ExecuteNonQueryAsync();
        }
        try { await scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>().Handle(dto); }
        catch (ValidationException) { rejected = true; }
    }
    Check(rejected && await Scalar("SELECT COUNT(*) FROM Orders") == baselineOrders &&
        await Scalar("SELECT UnitsInStock FROM Products WHERE Id=1") == baselineStock, name + " rejected without saving a sale");
}

using (var rollback = RollbackScope())
using (var scope = provider.CreateScope())
{
    var delivery = new CreateOrderDto("ALFKI", new string('A', 60), new string('B', 15), new string('C', 15), "0123456789", [new(1, price, 1)]);
    await scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>().Handle(delivery);
    int id = scope.ServiceProvider.GetRequiredService<ICreateOrderOutputPort>().OrderId;
    var invoice = await scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>().GetInvoice(id);
    Check(invoice is not null && invoice.ShipAddress == delivery.ShipAddress && invoice.ShipCity == delivery.ShipCity &&
        invoice.ShipCountry == delivery.ShipCountry && invoice.ShipPostalCode == "0123456789",
        "Delivery limits 60/15/15/10 persist exactly, including the leading postal zero");
}

// Both contexts load the same rowversion before either saves. All writes remain in a rollback-only test transaction.
using (var rollback = RollbackScope())
using (var firstScope = provider.CreateScope())
using (var secondScope = provider.CreateScope())
{
    var first = firstScope.ServiceProvider.GetRequiredService<ICommandsRepository>();
    var second = secondScope.ServiceProvider.GetRequiredService<ICommandsRepository>();
    var dto = Dto(new CreateOrderDetailDto(1, price, (short)baselineStock));
    await first.CreateOrder(OrderAggregate.From(dto));
    await second.CreateOrder(OrderAggregate.From(dto));
    await first.SaveChanges();
    bool conflict = false;
    try { await second.SaveChanges(); }
    catch (ValidationException ex) { conflict = ex.Errors.Any(e => e.Message.Contains("otra venta")); }
    Check(conflict, "Stale rowversion rejects a competing sale with a personalized message");
    Check(await Scalar("SELECT UnitsInStock FROM Products WHERE Id=1") == 0, "Competing writes cannot produce negative stock");
}

mail.Fail = true;
using (var rollback = RollbackScope())
using (var scope = provider.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>().Handle(Dto(new CreateOrderDetailDto(1, price, 1), new(3, 65, 1), new(4, 75, 1), new(5, 50, 1)));
    Check(scope.ServiceProvider.GetRequiredService<ICreateOrderOutputPort>().OrderId > 0,
        "SMTP failure does not cancel a saved special order");
}
Check(await Scalar("SELECT COUNT(*) FROM Orders") == baselineOrders &&
    await Scalar("SELECT COUNT(*) FROM OrderDetails") == baselineDetails &&
    await Scalar("SELECT UnitsInStock FROM Products WHERE Id=1") == baselineStock, "Rollback-only special-order test preserves orders, details and inventory");
Check(await Scalar("SELECT COUNT(*) FROM Products WHERE UnitsInStock < 0") == 0, "No negative inventory after all checks");
mail.Fail = false;
using (var rollback = RollbackScope())
using (var scope = provider.CreateScope())
{
    var tax = scope.ServiceProvider.GetRequiredService<IOptions<SalesTaxOptions>>().Value;
    tax.Enabled = false;
    await scope.ServiceProvider.GetRequiredService<ICreateOrderInputPort>().Handle(Dto(new CreateOrderDetailDto(1, price, 1)));
    int id = scope.ServiceProvider.GetRequiredService<ICreateOrderOutputPort>().OrderId;
    var invoice = await scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>().GetInvoice(id);
    Check(invoice!.TaxRate == 0 && invoice.TaxAmount == 0 && invoice.Total == invoice.TaxableBase,
        "IVA is not applied when configuration disables it");
    tax.Enabled = true;
}
using (var rollback = RollbackScope())
using (var scope = provider.CreateScope())
{
    string prefix = "Check" + string.Concat(Enumerable.Range(0, 8).Select(_ => (char)('a' + Random.Shared.Next(26))));
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    for (int i = 0; i < 11; i++)
    {
        await using var command = new SqlCommand("INSERT Products(Name,UnitsInStock,UnitPrice) VALUES(@name,3,1)", connection);
        command.Parameters.AddWithValue("@name", prefix + i);
        await command.ExecuteNonQueryAsync();
    }
    var read = scope.ServiceProvider.GetRequiredService<ISalesReadInputPort>();
    var firstPage = await read.SearchProducts(null, prefix, 1);
    var secondPage = await read.SearchProducts(null, prefix, 2);
    Check(firstPage.Items.Count == 8 && firstPage.TotalPages == 2 && secondPage.Items.Count == 3,
        "Product search paginates SQL Server results");
    Check((await read.SearchProducts(firstPage.Items[0].Id, prefix, 1)).Items.Count == 1,
        "Product search combines code and name filters");
    for (int i = 0; i < 11; i++)
    {
        await using var command = new SqlCommand("INSERT Customers(Id,Name,CurrentBalance,Identification) VALUES(@id,@name,0,@identification)", connection);
        command.Parameters.AddWithValue("@id", "XQAA" + (char)('A' + i));
        command.Parameters.AddWithValue("@name", prefix + i);
        command.Parameters.AddWithValue("@identification", "DEMO-DOC-" + i);
        await command.ExecuteNonQueryAsync();
    }
    var customers = await read.SearchCustomers("XQ", prefix, 1);
    Check(customers.Items.Count == 8 && customers.TotalPages == 2 &&
        (await read.SearchCustomers("XQ", prefix, 2)).Items.Count == 3,
        "Customer search combines two fields and paginates");
    Check((await read.SearchCustomers(null, "DEMO-DOC-", 1)).Items.Count == 8 &&
        (await read.SearchCustomers(null, "DEMO-DOC-", 2)).Items.Count == 3,
        "Customer identification search uses SQL pagination independently of name");
    Check((await read.SearchCustomers("XQAAB", "DEMO-DOC-1", 1)).Items.Single().Id == "XQAAB",
        "Customer code and partial identification filters combine correctly");
    Check((await read.SearchCustomers("XQ", prefix, 999)).Page == 2 &&
        (await read.SearchCustomers("XQ", prefix, -1)).Page == 1 &&
        (await read.SearchCustomers(null, Guid.NewGuid().ToString("N"), 1)).Items.Count == 0,
        "Customer pagination handles out-of-range pages and empty results");
    Check((await read.SearchProducts("2", null, 1)).Items.Count == 0,
        "Product selection excludes zero stock");
    Check(await read.GetInvoice(int.MaxValue) is null, "Unknown invoice returns no result");
}
Check(await Scalar("SELECT COUNT(*) FROM Orders") == baselineOrders &&
    await Scalar("SELECT COUNT(*) FROM OrderDetails") == baselineDetails &&
    await Scalar("SELECT UnitsInStock FROM Products WHERE Id=1") == baselineStock,
    "All additional search and IVA tests preserve existing sales and stock");
using (var rollback = RollbackScope())
using (var scope = provider.CreateScope())
{
    // Membership ports are intentionally internal in the existing package.
    async Task<IResult> MembershipCall(string operation, object dto)
    {
        string prefix = "NorthWind.Membership.Backend.Core.Interfaces." + operation + ".";
        var inputType = Type.GetType(prefix + "I" + operation + "InputPort, NorthWind.Membership.Backend.Core", true)!;
        var outputType = Type.GetType(prefix + "I" + operation + "OutputPort, NorthWind.Membership.Backend.Core", true)!;
        var input = scope.ServiceProvider.GetRequiredService(inputType);
        await (Task)inputType.GetMethod("Handle")!.Invoke(input, [dto])!;
        return (IResult)outputType.GetProperty("Result")!.GetValue(scope.ServiceProvider.GetRequiredService(outputType))!;
    }
    string email = "check-" + Guid.NewGuid().ToString("N")[..20] + "@invalid.example";
    var registration = new UserRegistrationDto(email, "Valid123!", "José", "Pérez");
    var registered = await MembershipCall("UserRegistration", registration);
    Check(registered is IStatusCodeHttpResult { StatusCode: 200 }, "Existing registration accepts valid accented names and credentials");
    Check(await MembershipCall("UserRegistration", registration) is IStatusCodeHttpResult { StatusCode: 400 },
        "Existing registration rejects a duplicate user");
    var login = await MembershipCall("UserLogin", new UserCredentialsDto(email, "Valid123!"));
    Check(login is IStatusCodeHttpResult { StatusCode: 200 } && login is IValueHttpResult { Value: TokensDto },
        "Existing login issues a JWT after successful registration");
    var tokens = (TokensDto)((IValueHttpResult)login).Value!;
    var valid = await new JsonWebTokenHandler().ValidateTokenAsync(tokens.AccessToken, new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = "NorthWindSales", ValidateAudience = true, ValidAudience = "NorthWindSalesClient",
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    });
    Check(valid.IsValid, "Issued JWT has a valid signature, issuer, audience and lifetime");
    Check(await MembershipCall("UserLogin", new UserCredentialsDto(email, "Wrong123!")) is IStatusCodeHttpResult { StatusCode: 400 },
        "Existing login rejects an incorrect password");
}
Console.WriteLine("All SQL Server checks passed; test sales were rolled back.");

sealed class MailProbe : IMailService
{
    public bool Fail { get; set; }
    public int Calls { get; private set; }
    public Task SendMailToAdministrator(string subject, string body)
    {
        Calls++;
        return Fail ? throw new System.Net.Mail.SmtpException("Simulated SMTP failure") : Task.CompletedTask;
    }
}
