using Microsoft.AspNetCore.Components.Forms;

namespace NorthWind.Sales.Frontend.Views.ViewModels.CreateOrder;

public class CreateOrderViewModel(ICreateOrderGateway gateway,
    IModelValidatorHub<CreateOrderViewModel> validator)
{
    private EditContext? formContext;
    private Guid requestId = Guid.NewGuid();
    public EditContext FormContext => formContext ??= new EditContext(this);
    public event Action? FormReset;
    public string CustomerId { get; set; } = string.Empty;
    public NorthWind.Sales.Entities.Dtos.LookupItem? SelectedCustomer { get; private set; }
    public bool IsSelectingCustomer { get; private set; }
    public async Task<bool> SelectCustomerAsync(NorthWind.Sales.Entities.Dtos.LookupItem customer)
    {
        if (IsSaving || IsSelectingCustomer) return false;
        IsSelectingCustomer = true;
        var draftContext = FormContext;
        try
        {
            var result = await gateway.SearchAsync(false, customer.Id, string.Empty, 1);
            var current = result.Items.SingleOrDefault(c => c.Id == customer.Id);
            if (IsSaving || !ReferenceEquals(draftContext, FormContext)) return false;
            if (current is null)
            {
                ErrorMessage = "El cliente ya no está disponible. Actualice la búsqueda.";
                return false;
            }
            CustomerId = current.Id;
            SelectedCustomer = current;
            // Copy master data into this order's delivery fields, including missing values.
            ShipAddress = current.Address ?? string.Empty;
            ShipCity = current.City ?? string.Empty;
            ShipCountry = current.Country ?? string.Empty;
            ShipPostalCode = current.PostalCode ?? string.Empty;
            ErrorMessage = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = "No se pudo comprobar el cliente. Intente nuevamente.";
            return false;
        }
        finally { IsSelectingCustomer = false; }
    }
    public string ShipAddress { get; set; } = string.Empty;
    public string ShipCity { get; set; } = string.Empty;
    public string ShipCountry { get; set; } = string.Empty;
    public string ShipPostalCode { get; set; } = string.Empty;
    public List<CreateOrderDetailViewModel> OrderDetails { get; } = [];
    public string InformationMessage { get; private set; } = string.Empty;
    public string ErrorMessage { get; private set; } = string.Empty;
    public bool IsSaving { get; private set; }
    public bool IsCancellationPending { get; private set; }
    public void RequestDraftCancellation()
    {
        if (!IsSaving && !IsSelectingCustomer) IsCancellationPending = true;
    }
    public void DismissDraftCancellation() => IsCancellationPending = false;
    public void ConfirmDraftCancellation()
    {
        if (!IsCancellationPending || IsSaving || IsSelectingCustomer) return;
        IsCancellationPending = false;
        NewOrder();
    }
    public int? SavedOrderId { get; private set; }
    public NorthWind.Sales.Entities.Dtos.TaxSettings? TaxSettings { get; private set; }
    private NorthWind.Sales.Entities.Dtos.OrderAmounts Amounts => NorthWind.Sales.Entities.Dtos.OrderAmounts.Calculate(
        OrderDetails.Select(d => d.UnitPrice * d.Quantity), TaxSettings?.Rate ?? 0);
    public decimal Subtotal => Amounts.Subtotal;
    public decimal DiscountAmount => Amounts.DiscountAmount;
    public decimal TaxableBase => Amounts.TaxableBase;
    public decimal TaxAmount => Amounts.TaxAmount;
    public decimal Total => Amounts.Total;
    public Task<NorthWind.Sales.Entities.Dtos.SearchPage> SearchAsync(bool products, string id, string name, int page, CancellationToken cancellationToken = default) =>
        products ? gateway.SearchAsync(true, id, name, page, cancellationToken) : gateway.SearchCustomersByIdentificationAsync(id, name, page, cancellationToken);
    public Task<byte[]> GetInvoicePdfAsync(int orderId) => gateway.GetInvoicePdfAsync(orderId);
    public void SetInformationMessage(string message) => InformationMessage = message;
    public async Task LoadTaxSettingsAsync()
    {
        try { TaxSettings = await gateway.GetTaxSettingsAsync(); ErrorMessage = string.Empty; }
        catch (HttpRequestException) { TaxSettings = null; ErrorMessage = "No se pudo cargar el IVA. Verifique SalesTax:Enabled y SalesTax:Rate en el backend antes de guardar."; }
    }

    public bool SelectProduct(NorthWind.Sales.Entities.Dtos.LookupItem product, CreateOrderDetailViewModel? replacing)
    {
        ErrorMessage = string.Empty;
        int id = int.Parse(product.Id);
        if (OrderDetails.Any(d => d != replacing && d.ProductId == id))
        {
            ErrorMessage = "El producto ya está en la orden.";
            return false;
        }
        if (product.UnitsInStock <= 0 || product.UnitPrice <= 0) return false;
        var detail = replacing ?? new CreateOrderDetailViewModel { Quantity = 1 };
        detail.ProductId = id;
        detail.ProductName = product.Name;
        detail.UnitPrice = product.UnitPrice;
        detail.UnitsInStock = product.UnitsInStock;
        if (replacing is null) OrderDetails.Add(detail);
        return true;
    }

    public void NewOrder()
    {
        if (IsSaving || IsSelectingCustomer || IsCancellationPending) return;
        ResetForm();
        InformationMessage = ErrorMessage = string.Empty;
    }
    public void ClearInformationMessage() => InformationMessage = string.Empty;
    private void ResetForm()
    {
        requestId = Guid.NewGuid();
        IsCancellationPending = false;
        CustomerId = ShipAddress = ShipCity = ShipCountry = ShipPostalCode = string.Empty;
        SelectedCustomer = null;
        OrderDetails.Clear();
        ErrorMessage = string.Empty;
        formContext = new EditContext(this);
        ModelValidatorComponentReference = null;
        FormReset?.Invoke();
    }
    public IModelValidatorHub<CreateOrderViewModel> Validator => validator;
    public ModelValidator<CreateOrderViewModel>? ModelValidatorComponentReference { get; set; }

    public void AddNewOrderDetailItem() =>
        OrderDetails.Add(new CreateOrderDetailViewModel());

    public async Task Send()
    {
        if (IsSaving || IsSelectingCustomer || IsCancellationPending) return;
        InformationMessage = string.Empty;
        ErrorMessage = string.Empty;
        SavedOrderId = null;
        if (TaxSettings is null) { ErrorMessage = "Consulte la configuración de IVA antes de guardar."; return; }
        if (OrderDetails.Any(d => d.Quantity <= 0 || d.Quantity > d.UnitsInStock))
        { ErrorMessage = "La cantidad debe ser mayor que cero y no superar el stock disponible."; return; }
        IsSaving = true;
        try
        {
            int OrderId = await gateway.CreateOrderAsync((CreateOrderDto)this);
            SavedOrderId = OrderId;
            ResetForm();
            InformationMessage = string.Format(CreateOrderMessages.CreatedOrderTemplate, OrderId);
        }
        catch (HttpRequestException ex)
        {
            if (ex.Data["Errors"] is IEnumerable<ValidationError> errors &&
                ModelValidatorComponentReference is { } component)
                component.AddErrors(errors);
            ErrorMessage = ex.StatusCode == System.Net.HttpStatusCode.Conflict
                ? ex.Data["Errors"] is IEnumerable<ValidationError> conflicts
                    ? string.Join(" ", conflicts.Select(error => error.Message).Distinct())
                    : "El inventario cambió. La orden no se guardó. Vuelva a enviar para comprobar nuevamente el stock."
                : ex.Data["Errors"] is IEnumerable<ValidationError>
                ? "Revise los campos indicados. La orden no se guardó."
                : "No se pudo confirmar la operación. Consulte la factura antes de volver a guardar.";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "La operación tardó demasiado. Compruebe si la factura se guardó antes de volver a enviar.";
        }
        finally { IsSaving = false; }
    }

    public static explicit operator CreateOrderDto(CreateOrderViewModel order) =>
        new(order.CustomerId, order.ShipAddress, order.ShipCity,
            order.ShipCountry, order.ShipPostalCode,
            order.OrderDetails.Select(d =>
                new CreateOrderDetailDto(d.ProductId, d.UnitPrice, d.Quantity))) { RequestId = order.requestId };
}
