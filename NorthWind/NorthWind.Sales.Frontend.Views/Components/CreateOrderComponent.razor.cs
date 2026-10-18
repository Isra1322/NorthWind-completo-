namespace NorthWind.Sales.Frontend.Views.Components;

using Microsoft.JSInterop;

public partial class CreateOrderComponent : IAsyncDisposable
{
    [Parameter, EditorRequired]
    public CreateOrderViewModel Order { get; set; } = null!;
    [Inject] public IJSRuntime JS { get; set; } = null!;
    private IJSObjectReference? InvoiceModule;
    private string? InvoiceUrl;
    private int? InvoiceUrlOrderId;
    private bool InvoicePdfNeedsManualActions;
    private bool IsPreparingInvoicePdf;
    private bool ValidSubmitReached;
    private string InvoicePdfMessage = string.Empty;
    private bool SearchOpen;
    private bool SearchProducts;
    private CreateOrderDetailViewModel? Replacing;
    private CreateOrderDetailViewModel? PendingRemoval;
    private bool IsSubmitting;
    private async Task SubmitOrderAsync()
    {
        if (IsSubmitting || Order.IsSaving || Order.IsSelectingCustomer || Order.IsCancellationPending) return;
        IsSubmitting = true;
        ValidSubmitReached = false;
        try { await Order.ModelValidatorComponentReference!.ValidateFormAsync(); }
        finally
        {
            if (!ValidSubmitReached) await CloseReservedInvoiceTabAsync();
            IsSubmitting = false;
        }
    }

    private async Task SubmitValidatedOrderAsync()
    {
        ValidSubmitReached = true;
        InvoicePdfMessage = string.Empty;
        InvoicePdfNeedsManualActions = false;
        InvoiceUrlOrderId = null;
        await ReleaseInvoiceUrlAsync();

        await Order.Send();
        if (Order.SavedOrderId is not int orderId)
        {
            await CloseReservedInvoiceTabAsync();
            return;
        }

        await PrepareAndDeliverInvoiceAsync(orderId);
    }

    private async Task PrepareAndDeliverInvoiceAsync(int orderId)
    {
        IsPreparingInvoicePdf = true;
        try
        {
            byte[] bytes = await Order.GetInvoicePdfAsync(orderId).WaitAsync(TimeSpan.FromSeconds(30));
            InvoiceModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/NorthWind.Sales.Frontend.Views/invoice.js");
            InvoiceUrl = await InvoiceModule.InvokeAsync<string>("create", bytes);
            InvoiceUrlOrderId = orderId;

            bool opened = await InvoiceModule.InvokeAsync<bool>("showReserved", InvoiceUrl);
            bool downloaded = await InvoiceModule.InvokeAsync<bool>("download", InvoiceUrl, $"Factura_{orderId}.pdf");
            if (opened && downloaded)
            {
                Order.SetInformationMessage($"Orden N.º {orderId} creada correctamente. La factura se ha descargado y abierto en una nueva pestaña.");
            }
            else
            {
                await CloseReservedInvoiceTabAsync();
                InvoicePdfNeedsManualActions = true;
                InvoicePdfMessage = !opened && !downloaded
                    ? "No se pudo abrir ni iniciar la descarga automática. Use los botones para abrir o descargar la factura."
                    : !opened
                        ? "La descarga se inició, pero no se pudo abrir una nueva pestaña. Use Ver PDF para abrirla."
                        : "La factura se abrió, pero no se pudo iniciar la descarga automática. Use Descargar PDF para guardarla.";
                Order.SetInformationMessage($"Orden N.º {orderId} creada correctamente.");
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or TimeoutException or JSException)
        {
            await CloseReservedInvoiceTabAsync();
            if (InvoiceUrl is not null) InvoiceUrlOrderId = orderId;
            InvoicePdfNeedsManualActions = true;
            InvoicePdfMessage = InvoiceUrl is null
                ? "No se pudo preparar automáticamente el PDF. Puede volver a intentarlo sin crear otra orden."
                : "La orden se guardó, pero falló una acción automática del PDF. Use los botones para abrirlo o descargarlo.";
            Order.SetInformationMessage($"Orden N.º {orderId} creada correctamente.");
        }
        finally { IsPreparingInvoicePdf = false; }
    }

    private Task RetryInvoicePdfAsync()
    {
        if (IsPreparingInvoicePdf || Order.SavedOrderId is not int orderId) return Task.CompletedTask;
        return PrepareInvoiceForManualActionsAsync(orderId);
    }

    private async Task PrepareInvoiceForManualActionsAsync(int orderId)
    {
        IsPreparingInvoicePdf = true;
        try
        {
            byte[] bytes = await Order.GetInvoicePdfAsync(orderId).WaitAsync(TimeSpan.FromSeconds(30));
            InvoiceModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/NorthWind.Sales.Frontend.Views/invoice.js");
            await ReleaseInvoiceUrlAsync();
            InvoiceUrl = await InvoiceModule.InvokeAsync<string>("create", bytes);
            InvoiceUrlOrderId = orderId;
            InvoicePdfMessage = "La factura está lista. Puede abrirla o descargarla con los botones.";
            InvoicePdfNeedsManualActions = true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or TimeoutException or JSException)
        {
            InvoicePdfMessage = "No se pudo obtener el PDF. Intente nuevamente; la orden ya está guardada y no se volverá a crear.";
        }
        finally { IsPreparingInvoicePdf = false; }
    }

    private async Task CloseReservedInvoiceTabAsync()
    {
        if (InvoiceModule is null) return;
        try { await InvoiceModule.InvokeVoidAsync("closeReserved"); }
        catch (JSException) { }
    }

    private async Task ReleaseInvoiceUrlAsync()
    {
        if (InvoiceUrl is null || InvoiceModule is null) return;
        try { await InvoiceModule.InvokeVoidAsync("releaseAfter", InvoiceUrl); }
        catch (JSException) { }
        InvoiceUrl = null;
    }
    private void RequestDraftCancellation()
    {
        if (!IsSubmitting && !SearchOpen && PendingRemoval is null) Order.RequestDraftCancellation();
    }
    private void SetPostalCode(string? value)
    {
        Order.ShipPostalCode = new string((value ?? string.Empty).Where(c => c is >= '0' and <= '9').Take(10).ToArray());
        Order.FormContext.NotifyFieldChanged(new(Order, nameof(Order.ShipPostalCode)));
    }
    protected override Task OnInitializedAsync()
    {
        Order.FormReset += CloseSearch;
        return Order.LoadTaxSettingsAsync();
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            InvoiceModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/NorthWind.Sales.Frontend.Views/invoice.js");
            await InvoiceModule.InvokeVoidAsync("attachSubmitReservation", "createOrderForm");
        }
        catch (JSException) { }
    }
    private void CloseSearch()
    {
        SearchOpen = false;
        SearchProducts = false;
        Replacing = null;
        PendingRemoval = null;
    }
    public async ValueTask DisposeAsync()
    {
        Order.FormReset -= CloseSearch;
        if (InvoiceModule is not null)
        {
            try
            {
                await InvoiceModule.InvokeVoidAsync("detachSubmitReservation");
                await CloseReservedInvoiceTabAsync();
                if (InvoiceUrl is not null) await InvoiceModule.InvokeVoidAsync("releaseAfter", InvoiceUrl);
                await InvoiceModule.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }
    }
    private void OpenSearch(bool products, CreateOrderDetailViewModel? replacing = null)
    {
        if (Order.IsSaving) return;
        SearchProducts = products;
        Replacing = replacing;
        SearchOpen = true;
    }
    private async Task SelectItem(NorthWind.Sales.Entities.Dtos.LookupItem item)
    {
        if (SearchProducts)
        {
            if (!Order.SelectProduct(item, Replacing)) return;
            var detail = Order.OrderDetails.Single(d => d.ProductId == int.Parse(item.Id));
            foreach (string field in new[] { nameof(detail.ProductId), nameof(detail.UnitPrice), nameof(detail.Quantity) })
                Order.FormContext.NotifyFieldChanged(new(detail, field));
            Order.FormContext.NotifyFieldChanged(new(Order, nameof(Order.OrderDetails)));
        }
        else
        {
            if (!await Order.SelectCustomerAsync(item)) return;
            await Order.ModelValidatorComponentReference!.ValidateFieldAsync(() => Order.CustomerId);
            // Validate supplied master data without truncating it or flagging missing optional defaults.
            await ValidateDeliveryDefault(Order.ShipAddress, () => Order.ShipAddress);
            await ValidateDeliveryDefault(Order.ShipCity, () => Order.ShipCity);
            await ValidateDeliveryDefault(Order.ShipCountry, () => Order.ShipCountry);
            await ValidateDeliveryDefault(Order.ShipPostalCode, () => Order.ShipPostalCode);
        }
        CloseSearch();
    }
    private Task ValidateDeliveryDefault(string value, System.Linq.Expressions.Expression<Func<string>> field) =>
        string.IsNullOrEmpty(value)
            ? Order.ModelValidatorComponentReference!.RefreshFieldAsync(field)
            : Order.ModelValidatorComponentReference!.ValidateFieldAsync(field);
    private void RequestRemoval(CreateOrderDetailViewModel detail)
    {
        if (!Order.IsSaving && PendingRemoval is null && Order.OrderDetails.Contains(detail))
            PendingRemoval = detail;
    }
    private void CancelRemoval() => PendingRemoval = null;
    private void ConfirmRemoval()
    {
        var detail = PendingRemoval;
        PendingRemoval = null;
        if (!Order.IsSaving && detail is not null && Order.OrderDetails.Remove(detail))
            Order.FormContext.NotifyFieldChanged(new(Order, nameof(Order.OrderDetails)));
    }
}
