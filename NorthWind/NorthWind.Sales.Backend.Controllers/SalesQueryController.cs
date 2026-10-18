using Microsoft.AspNetCore.Http;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces;
using NorthWind.Sales.Entities.Dtos;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Builder;

public static class SalesQueryController
{
    public static WebApplication UseSalesQueryController(this WebApplication app)
    {
        app.MapGet("/sales/products", (string? id, string? name, int? page, ISalesReadInputPort input) =>
            input.SearchProducts(id, name, page ?? 1)).RequireAuthorization();
        app.MapGet("/sales/customers", (string? id, string? name, int? page, ISalesReadInputPort input) =>
            input.SearchCustomers(id, name, page ?? 1)).RequireAuthorization();
        app.MapGet("/sales/customers/lookup", (string? identification, string? name, int? page, ISalesReadInputPort input) =>
            input.SearchCustomersByIdentification(identification, name, page ?? 1)).RequireAuthorization();
        app.MapGet("/sales/tax", (ISalesReadInputPort input) => input.GetTaxSettings()).RequireAuthorization();
        app.MapGet("/sales/products/{id:int}/edit-availability", async (int id, IInvoiceEditingService editor) =>
            await editor.GetProductForEditAsync(id) is { } product ? Results.Ok(product) : Results.NotFound()).RequireAuthorization();
        app.MapGet("/sales/invoices", (string? customerId, string? invoiceNumber, string? issueDate, string? identification, int? page, ISalesReadInputPort input) =>
            input.FilterInvoices(new(customerId ?? "", invoiceNumber ?? "", issueDate ?? "", identification ?? ""), page ?? 1)).RequireAuthorization();
        app.MapGet("/sales/invoices/{id:int}", async (int id, ISalesReadInputPort input, IInvoiceEditingService editor) =>
        {
            var version = await editor.GetCurrentVersionAsync(id);
            if (version is not null) return Results.Ok(version.Invoice);
            return await input.GetInvoice(id) is { } invoice
                ? Results.Ok(invoice)
                : Results.NotFound(new { message = "No se encontró la factura." });
        }).RequireAuthorization();
        app.MapGet("/sales/invoices/{id:int}/pdf", async (int id, ISalesReadInputPort input, IInvoicePdfPresenter pdf, IInvoiceEditingService editor) =>
        {
            var version = await editor.GetCurrentVersionAsync(id);
            if (version is not null) return Results.File(version.PdfContent, "application/pdf", $"Factura_{id}_V{version.Version.VersionNumber}.pdf");
            return await input.GetInvoice(id) is { } currentInvoice
                ? Results.File(pdf.Render(currentInvoice), "application/pdf", $"Factura_{id}.pdf")
                : Results.NotFound(new { message = "No se encontró la factura." });
        }).RequireAuthorization();
        app.MapPut("/sales/invoices/{id:int}", async (int id, UpdateInvoiceDto update, ClaimsPrincipal principal, IInvoiceEditingService editor, ILoggerFactory loggerFactory) =>
        {
            try { return Results.Ok(await editor.UpdateAsync(id, update, principal.Identity?.Name ?? "Usuario autenticado")); }
            catch (KeyNotFoundException) { return Results.NotFound(new { message = "No se encontró la factura." }); }
            catch (NorthWind.Sales.Backend.BusinessObjects.Exceptions.InvoiceInputException ex) { return Results.BadRequest(new { message = ex.Message }); }
            catch (NorthWind.Sales.Backend.BusinessObjects.Exceptions.InvoiceConflictException ex)
            {
                loggerFactory.CreateLogger("InvoiceEditing").LogWarning(ex, "No se pudo editar la factura {InvoiceId}.", id);
                return Results.Conflict(new { message = ex.Message });
            }
        }).RequireAuthorization();
        app.MapGet("/sales/invoices/{id:int}/versions", async (int id, IInvoiceEditingService editor) =>
        {
            var versions = await editor.GetVersionsAsync(id);
            return versions.Count == 0 ? Results.NotFound(new { message = "No se encontró la factura." }) : Results.Ok(versions);
        }).RequireAuthorization();
        app.MapGet("/sales/invoices/{id:int}/versions/{version:int}", async (int id, int version, IInvoiceEditingService editor) =>
            await editor.GetVersionAsync(id, version) is { } result ? Results.Ok(result) : Results.NotFound(new { message = "No se encontró esa versión de la factura." }))
            .RequireAuthorization();
        app.MapGet("/sales/invoices/{id:int}/versions/{version:int}/pdf", async (int id, int version, IInvoiceEditingService editor) =>
            await editor.GetVersionAsync(id, version) is { } result
                ? Results.File(result.PdfContent, "application/pdf", $"Factura_{id}_V{version}.pdf")
                : Results.NotFound(new { message = "No se encontró esa versión de la factura." })).RequireAuthorization();
        return app;
    }
}
