using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using NorthWind.Sales.Entities.Dtos;
using NorthWind.Sales.Backend.BusinessObjects.Aggregates;
using NorthWind.Sales.Backend.BusinessObjects.Enums;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces;
using NorthWind.Sales.Backend.BusinessObjects.ValueObjects;

internal static class InvoiceCalculationChecks
{
    public static void Run(bool exportPdf = false)
    {
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            Console.WriteLine("PASS " + name);
        }
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "sales-appsettings.json")).Build();
        using var configuredServices = new ServiceCollection().AddSingleton<IConfiguration>(configuration)
            .AddDataContexts(_ => { }).BuildServiceProvider();
        var configuredTax = configuredServices.GetRequiredService<IOptions<SalesTaxOptions>>().Value;
        Check(configuredTax.Enabled && configuredTax.EffectiveRate == .15m,
            "Backend binds enabled 15% IVA from actual WebApi appsettings");
        var example = OrderAmounts.Calculate([40m, 60m], configuredTax.EffectiveRate);
        Check(example == new OrderAmounts(100m, 0m, 100m, 15m, 115m),
            "100.00 subtotal produces 15.00 IVA and 115.00 total without discount");
        var exampleOrder = new OrderAggregate();
        exampleOrder.AddDetail(1, 20m, 2); exampleOrder.AddDetail(2, 30m, 2);
        exampleOrder.CalculateTotals(configuredTax.EffectiveRate);
        Check(exampleOrder.Subtotal == example.Subtotal && exampleOrder.TaxRate == .15m &&
            exampleOrder.TaxAmount == example.TaxAmount && exampleOrder.Total == example.Total,
            "Backend aggregate matches the 15% verification example");
        var amounts = OrderAmounts.Calculate([35m], configuredTax.EffectiveRate);
        Check(amounts == new OrderAmounts(35, 0, 35, 5.25m, 40.25m), "Chai: 35.00 subtotal, 5.25 IVA and 40.25 total");
        var order = new OrderAggregate();
        order.AddDetail(1, 35, 1); order.AddDetail(2, 12.55m, 3); order.CalculateTotals(configuredTax.EffectiveRate);
        var multiple = OrderAmounts.Calculate([35m, 37.65m], configuredTax.EffectiveRate);
        Check(multiple == new OrderAmounts(72.65m, 0, 72.65m, 10.90m, 83.55m),
            "Mixed quantities: 72.65 subtotal, 10.90 rounded IVA, 83.55 total");
        Check(order.Subtotal == multiple.Subtotal && order.Total == multiple.Total && order.TaxAmount == multiple.TaxAmount,
            "Multiple products: backend uses the shared decimal calculation");
        order.DiscountType = DiscountType.Flat; order.Discount = 5m; order.CalculateTotals(.125m);
        Check(order.Total == OrderAmounts.Calculate([35m, 37.65m], .125m, 0, 5).Total, "Existing flat discount remains distinct from IVA");
        Check(OrderAmounts.Calculate([.335m, .335m], 0, 0).Subtotal == .68m, "Rounding is per line and away from zero");
        bool missingRateRejected = false;
        try { _ = new SalesTaxOptions { Enabled = true }.EffectiveRate; }
        catch (InvalidOperationException) { missingRateRejected = true; }
        Check(missingRateRejected, "Missing IVA rate cannot silently become zero");
        Check(new SalesTaxOptions { Enabled = false }.EffectiveRate == 0, "Explicitly disabled tax remains configurable");
        Check(SearchInputRules.CustomerCode("ALFKItool") == "ALFKI" &&
            SearchInputRules.ValidateLookup(false, "ALF", "José O'Connor-Ñ").Count == 0 &&
            SearchInputRules.ValidateLookup(false, "ALF", "José12").ContainsKey("Name") &&
            SearchInputRules.ValidateLookup(false, "ALFKI!", "").Count == 1,
            "Customer search permits partial alphanumeric codes and Spanish names, rejecting invalid code characters");
        Check(SearchInputRules.ValidateLookup(true, "2147483647", "Chef Anton's Cajun Seasoning").Count == 0 &&
            SearchInputRules.ValidateLookup(true, "21474836480", "").ContainsKey("Id") &&
            SearchInputRules.ValidateLookup(true, "12A", "").ContainsKey("Id") &&
            SearchInputRules.Name(new string('x', 60)).Length == 50,
            "Product search enforces the SQL int range and 50-character names while allowing apostrophes");
        var badInvoiceFilters = SearchInputRules.ValidateInvoices(new("ALFKI", "+0", "2026-10-16"), new DateOnly(2026, 10, 15));
        Check(badInvoiceFilters.ContainsKey("InvoiceNumber") && badInvoiceFilters.ContainsKey("IssueDate") &&
            SearchInputRules.ValidateInvoices(new("alf", "376", "2026-10-15"), new DateOnly(2026, 10, 15)).Count == 0 &&
            SearchInputRules.ValidateInvoices(new("ALFKI", "1.5", ""), new DateOnly(2026, 10, 15)).ContainsKey("InvoiceNumber"),
            "Invoice filters require a partial alphanumeric client ID, positive integer ID and valid nonfuture ISO date");
        var history = new InvoiceDto(91, "ALFKI", DateTime.Now, "Dirección México", "Puebla", "México", "72100", 35, 0, 0, 35,
            [new(1, "Chai", 35, 1)], true);
        Check(history.DiscountAmount == 0 && history.Total == 35, "Invoice 91-style recorded zero tax is never repriced");
        using var provider = new ServiceCollection().AddPresenters().BuildServiceProvider();
        var presenter = provider.GetRequiredService<IInvoicePdfPresenter>();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var exampleInvoice = new InvoiceDto(100, "ALFKI", DateTime.Now, "Calle 123", "Quito", "Ecuador", "170101",
            exampleOrder.Subtotal!.Value, exampleOrder.TaxRate!.Value, exampleOrder.TaxAmount!.Value,
            exampleOrder.Total!.Value, [new(1, "Producto A", 20m, 2), new(2, "Producto B", 30m, 2)], true);
        Check(exampleInvoice.DiscountAmount == 0m && exampleInvoice.TaxableBase == 100m,
            "New invoice records no discount");
        string examplePdf = Encoding.GetEncoding(1252).GetString(presenter.Render(exampleInvoice));
        Check(new[] { "(Subtotal)", @"(IVA \(15 %\))", "(TOTAL A PAGAR)",
            "(100,00)", "(15,00)", "($115,00)" }.All(examplePdf.Contains) &&
            !examplePdf.Contains("(Descuento)") && !examplePdf.Contains("(Base imponible)"),
            "New PDF shows only subtotal, recorded 15% IVA and total");
        var discountedHistory = exampleInvoice with { TaxAmount = 13.50m, Total = 103.50m };
        string discountedPdf = Encoding.GetEncoding(1252).GetString(presenter.Render(discountedHistory));
        Check(discountedHistory.DiscountAmount == 10m && discountedPdf.Contains("($103,50)") &&
            discountedPdf.Contains("(13,50)") && !discountedPdf.Contains("(Descuento)") &&
            !discountedPdf.Contains("(Base imponible)") && discountedPdf.Contains("descuento histórico de 10,00"),
            "Historical discounted invoice retains its recorded tax, discount and total");
        var unknownTax = history with { TaxRecorded = false };
        string unknownPdf = Encoding.GetEncoding(1252).GetString(presenter.Render(unknownTax));
        Check(unknownTax.Total == 35m && unknownPdf.Contains("IVA histórico no disponible") &&
            !unknownPdf.Contains(@"(IVA \(") && !unknownPdf.Contains("($40,25)"),
            "Historical invoice without tax does not invent IVA or change its total");
        var amountWithoutRate = history with { TaxAmount = 5.25m, Total = 40.25m, TaxRateRecorded = false };
        string amountPdf = Encoding.GetEncoding(1252).GetString(presenter.Render(amountWithoutRate));
        Check(amountPdf.Contains("(IVA)") && amountPdf.Contains("(5,25)") && !amountPdf.Contains(@"(IVA \("),
            "Recorded IVA amount is shown even without a known rate; no rate is invented");
        var currencyExample = exampleInvoice with { Subtotal = 160m, TaxAmount = 24m, Total = 184m };
        string currencyPdf = Encoding.GetEncoding(1252).GetString(presenter.Render(currencyExample));
        Check(currencyPdf.Contains("($184,00)") && !currencyPdf.Contains("$$") && currencyExample.Total == 184m,
            "TOTAL A PAGAR displays exactly $184,00 without changing the value");
        bool AtX(string label, int x) => System.Text.RegularExpressions.Regex.IsMatch(examplePdf,
            @"1 0 0 1 " + x + @" [0-9.]+ Tm \(" + System.Text.RegularExpressions.Regex.Escape(label) + @"\)");
        Check(AtX("Producto", 115) && AtX("Producto A", 115) && AtX("Producto B", 115),
            "PDF product header and names share the same left column anchor");
        decimal TextX(string label) => decimal.Parse(System.Text.RegularExpressions.Regex.Match(examplePdf,
            @"1 0 0 1 ([0-9.]+) [0-9.]+ Tm \(" + System.Text.RegularExpressions.Regex.Escape(label) + @"\)").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        Check(TextX("1") > TextX("Código") && TextX("20,00") > TextX("Precio unitario") &&
            TextX("2") > TextX("Cantidad") && TextX("40,00") > TextX("Importe") &&
            TextX("20,00") + 25.02m == 397m && TextX("2") + 5.56m == 455m && TextX("40,00") + 25.02m == 549m,
            "PDF numeric values end at the right column anchors; shorter values start farther right");
        string pdf = Encoding.GetEncoding(1252).GetString(presenter.Render(history));
        Check(pdf.Contains("FACTURA") && pdf.Contains("Dirección México") && pdf.Contains("TOTAL A PAGAR") && pdf.Contains("Código") && !pdf.Contains(" | "),
            "PDF has organized headings, Spanish characters and table columns");
        var many = history with { Lines = Enumerable.Range(1, 100).Select(i => new InvoiceLine(i, "Producto español con nombre largo y descripción", 35, 1)).ToArray() };
        string paged = Encoding.GetEncoding(1252).GetString(presenter.Render(many));
        Check(paged.Contains("Página 2") && paged.Split("(Código)").Length > 2 && paged.Contains("Producto español"), "Multiple pages repeat the table header and preserve product names");
        if (exportPdf)
        {
            Directory.CreateDirectory("artifacts/invoice-checks");
            File.WriteAllBytes("artifacts/invoice-checks/iva-15.pdf", presenter.Render(exampleInvoice));
            File.WriteAllBytes("artifacts/invoice-checks/layout-91.pdf", presenter.Render(history));
            File.WriteAllBytes("artifacts/invoice-checks/layout-multipage.pdf", presenter.Render(many));
        }
    }
}
