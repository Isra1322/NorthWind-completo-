namespace NorthWind.Sales.Frontend.WebApiGateways;

internal class CreateOrderGateway(HttpClient client) : ICreateOrderGateway
{
    public async Task<int> CreateOrderAsync(CreateOrderDto order)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(Endpoints.CreateOrder, order);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<int>();
        }
        catch (Exception exception) when (order.RequestId.HasValue &&
            exception is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            // Query the persisted receipt; never issue another POST to recover a lost response.
            try
            {
                using var receipt = await client.GetAsync($"sales/orders/requests/{order.RequestId:D}");
                if (receipt.IsSuccessStatusCode) return await receipt.Content.ReadFromJsonAsync<int>();
            }
            catch (Exception recoveryException) when (recoveryException is HttpRequestException or
                OperationCanceledException or System.Text.Json.JsonException) { }
            if (exception is System.Text.Json.JsonException)
                throw new HttpRequestException("No se pudo leer la confirmación de la orden.", exception);
            throw;
        }
    }
    public Task<NorthWind.Sales.Entities.Dtos.SearchPage> SearchAsync(bool products, string id, string name, int page) => SearchAsync(products, id, name, page, CancellationToken.None);
    public async Task<NorthWind.Sales.Entities.Dtos.SearchPage> SearchAsync(bool products, string id, string name, int page, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<NorthWind.Sales.Entities.Dtos.SearchPage>(
            $"sales/{(products ? "products" : "customers")}?id={Uri.EscapeDataString(id)}&name={Uri.EscapeDataString(name)}&page={page}", cancellationToken)
        ?? throw new HttpRequestException("No se pudo cargar la búsqueda.");
    public Task<NorthWind.Sales.Entities.Dtos.SearchPage> SearchCustomersByIdentificationAsync(string identification, string name, int page) => SearchCustomersByIdentificationAsync(identification, name, page, CancellationToken.None);
    public async Task<NorthWind.Sales.Entities.Dtos.SearchPage> SearchCustomersByIdentificationAsync(string identification, string name, int page, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<NorthWind.Sales.Entities.Dtos.SearchPage>(
            $"sales/customers/lookup?identification={Uri.EscapeDataString(identification)}&name={Uri.EscapeDataString(name)}&page={page}", cancellationToken)
        ?? throw new HttpRequestException("No se pudo cargar la búsqueda de clientes.");
    public async Task<NorthWind.Sales.Entities.Dtos.TaxSettings> GetTaxSettingsAsync() =>
        await client.GetFromJsonAsync<NorthWind.Sales.Entities.Dtos.TaxSettings>("sales/tax")
        ?? throw new HttpRequestException("No se pudo consultar la configuración de IVA.");
    public async Task<NorthWind.Sales.Entities.Dtos.InvoiceDto?> GetInvoiceAsync(int id)
    {
        try
        {
            using var response = await client.GetAsync($"sales/invoices/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<NorthWind.Sales.Entities.Dtos.InvoiceDto>();
        }
        catch (HttpRequestException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        { return null; }
    }
    public Task<byte[]> GetInvoicePdfAsync(int id) => client.GetByteArrayAsync($"sales/invoices/{id}/pdf");
    public Task<NorthWind.Sales.Entities.Dtos.InvoicePage> FilterInvoicesAsync(NorthWind.Sales.Entities.Dtos.InvoiceSearchFilters filters, int page) => FilterInvoicesAsync(filters, page, CancellationToken.None);
    public async Task<NorthWind.Sales.Entities.Dtos.InvoicePage> FilterInvoicesAsync(NorthWind.Sales.Entities.Dtos.InvoiceSearchFilters filters, int page, CancellationToken cancellationToken) =>
        await client.GetFromJsonAsync<NorthWind.Sales.Entities.Dtos.InvoicePage>(
            $"sales/invoices?customerId={Uri.EscapeDataString(filters.CustomerId)}&invoiceNumber={Uri.EscapeDataString(filters.InvoiceNumber)}&issueDate={Uri.EscapeDataString(filters.IssueDate)}&identification={Uri.EscapeDataString(filters.Identification)}&page={page}", cancellationToken)
        ?? throw new HttpRequestException("No se pudo consultar el listado de facturas.");
    public async Task<NorthWind.Sales.Entities.Dtos.InvoiceVersionInfo> UpdateInvoiceAsync(int id, NorthWind.Sales.Entities.Dtos.UpdateInvoiceDto update)
    {
        using var response = await client.PutAsJsonAsync($"sales/invoices/{id}", update);
        if (!response.IsSuccessStatusCode) throw await CreateError(response);
        return await response.Content.ReadFromJsonAsync<NorthWind.Sales.Entities.Dtos.InvoiceVersionInfo>()
            ?? throw new HttpRequestException("No se recibió la versión de la factura guardada.");
    }
    public async Task<IReadOnlyList<NorthWind.Sales.Entities.Dtos.InvoiceVersionInfo>> GetInvoiceVersionsAsync(int id) =>
        await client.GetFromJsonAsync<IReadOnlyList<NorthWind.Sales.Entities.Dtos.InvoiceVersionInfo>>($"sales/invoices/{id}/versions")
        ?? [];
    public async Task<NorthWind.Sales.Entities.Dtos.InvoiceVersionDto?> GetInvoiceVersionAsync(int id, int version)
    {
        using var response = await client.GetAsync($"sales/invoices/{id}/versions/{version}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NorthWind.Sales.Entities.Dtos.InvoiceVersionDto>();
    }
    public async Task<NorthWind.Sales.Entities.Dtos.LookupItem?> GetProductForEditAsync(int productId)
    {
        using var response = await client.GetAsync($"sales/products/{productId}/edit-availability");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NorthWind.Sales.Entities.Dtos.LookupItem>();
    }
    private static async Task<HttpRequestException> CreateError(HttpResponseMessage response)
    {
        string message = "No se pudo guardar la factura.";
        try { var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(); if (body?.TryGetValue("message", out var value) == true) message = value; }
        catch (System.Text.Json.JsonException) { }
        return new HttpRequestException(message, null, response.StatusCode);
    }
}
