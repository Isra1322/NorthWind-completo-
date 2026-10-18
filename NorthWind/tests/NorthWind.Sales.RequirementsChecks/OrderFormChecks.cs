using Microsoft.AspNetCore.Components.Forms;
using NorthWind.Sales.Entities.Dtos;
using NorthWind.Sales.Entities.Dtos.CreateOrder;
using NorthWind.Sales.Frontend.BusinessObjects.Interfaces;
using NorthWind.Sales.Frontend.Views.ViewModels.CreateOrder;
using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Validation.Entities.ValueObjects;

internal static class OrderFormChecks
{
    public static async Task Run()
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        var gateway = new GatewayProbe();
        var model = new CreateOrderViewModel(gateway, new ValidatorProbe());
        await model.LoadTaxSettingsAsync();
        Check(model.TaxSettings is { Enabled: true, Rate: .15m }, "Draft loads valid 15% IVA settings");
        model.SelectProduct(new("1", "Producto A", 20m, 10), null);
        model.OrderDetails[0].Quantity = 2;
        model.SelectProduct(new("2", "Producto B", 30m, 10), null);
        model.OrderDetails[1].Quantity = 2;
        Check(model.Subtotal == 100m && model.DiscountAmount == 0m && model.TaxableBase == 100m &&
            model.TaxAmount == 15m && model.Total == 115m, "Draft matches exact 100.00 verification example without discount");
        model.OrderDetails[1].Quantity = 1;
        Check(model.Total == 80.50m && model.TaxAmount == 10.50m, "Changing quantity updates IVA and total");
        model.SelectProduct(new("3", "Producto C", 10m, 10), model.OrderDetails[1]);
        Check(model.Subtotal == 50m && model.TaxAmount == 7.50m && model.Total == 57.50m,
            "Replacing a product updates IVA and total");
        model.OrderDetails.RemoveAt(1);
        Check(model.Subtotal == 40m && model.TaxAmount == 6m && model.Total == 46m,
            "Removing a product updates IVA and total");
        model.NewOrder();
        model.SelectProduct(new("1", "Chai", 35m, 10), null);
        Check(model.Subtotal == 35m && model.TaxAmount == 5.25m && model.Total == 40.25m,
            "Chai draft: 35.00 subtotal, 5.25 IVA, 40.25 total");
        model.NewOrder();
        void Fill()
        {
            model.CustomerId = "ALFKI";
            model.ShipAddress = "Calle 123";
            model.ShipCity = "Quito";
            model.ShipCountry = "Ecuador";
            model.ShipPostalCode = "170101";
            model.SelectProduct(new("1", "Chai", 35, 10), null);
            model.OrderDetails[0].Quantity = 2;
        }
        Fill();
        var context = model.FormContext;
        context.NotifyFieldChanged(new(model, nameof(model.ShipCity)));
        new ValidationMessageStore(context).Add(new FieldIdentifier(model, nameof(model.CustomerId)), "Previous error");
        int resets = 0;
        model.FormReset += () => resets++;
        Check(model.Subtotal == 70 && model.DiscountAmount == 0 && model.TaxableBase == 70 && model.TaxAmount == 10.50m && model.Total == 80.50m, "Draft calculates configured IVA without discount before saving");
        var sending = model.Send();
        await model.Send();
        model.NewOrder();
        Check(model.IsSaving && gateway.Requests == 1 && model.CustomerId == "ALFKI" && model.OrderDetails.Count == 1,
            "Overlapping sends and draft discard are blocked while saving");
        gateway.Response.SetResult(86);
        await sending;
        Check(new[] { model.CustomerId, model.ShipAddress, model.ShipCity, model.ShipCountry, model.ShipPostalCode }.All(string.IsNullOrEmpty) && model.OrderDetails.Count == 0,
            "Confirmed success clears every field and all selected products");
        Check(model.Subtotal == 0 && model.TaxAmount == 0 && model.Total == 0 && model.TaxSettings!.Rate == .15m,
            "Confirmed success resets totals without changing IVA configuration");
        Check(model.SavedOrderId == 86 && model.InformationMessage.Contains("86") && model.ErrorMessage == "" && !model.IsSaving,
            "Confirmed success retains the real invoice number and success message");
        Check(!ReferenceEquals(context, model.FormContext) && !model.FormContext.IsModified() && !model.FormContext.GetValidationMessages().Any() && resets == 1,
            "Confirmed success replaces EditContext and notifies temporary selection reset");

        Fill();
        gateway.Response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = model.Send();
        gateway.Response.SetResult(87);
        await second;
        Check(gateway.Requests == 2 && model.SavedOrderId == 87 && model.OrderDetails.Count == 0,
            "Another order can be submitted without reloading or pressing New order");

        Fill();
        var failureContext = model.FormContext;
        var detail = model.OrderDetails.Single();
        gateway.Response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = model.Send();
        gateway.Response.SetException(new HttpRequestException("Rejected"));
        await failure;
        Check(model.CustomerId == "ALFKI" && model.ShipAddress == "Calle 123" && model.ShipCity == "Quito" &&
            model.ShipCountry == "Ecuador" && model.ShipPostalCode == "170101" && ReferenceEquals(detail, model.OrderDetails.Single()) && detail.Quantity == 2,
            "Backend failure preserves customer, shipping fields, product and quantity");
        Check(ReferenceEquals(failureContext, model.FormContext) && model.InformationMessage == "" && model.ErrorMessage != "" && !model.IsSaving,
            "Backend failure preserves validation context, reports error and releases actions");
        gateway.Response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var conflict = model.Send();
        var conflictException = new HttpRequestException("Conflict", null, System.Net.HttpStatusCode.Conflict);
        conflictException.Data["Errors"] = new[] { new ValidationError("OrderDetails[0].Quantity",
            "No se pudo guardar la orden porque el inventario cambió. Stock disponible: 0. Cantidad solicitada: 2.") };
        gateway.Response.SetException(conflictException);
        await conflict;
        Check(!model.IsSaving && model.ErrorMessage.Contains("Stock disponible: 0") &&
            ReferenceEquals(failureContext, model.FormContext) && model.OrderDetails.Single().Quantity == 2,
            "409 conflict displays actual stock and preserves the draft for retry");
        gateway.Response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var retry = model.Send();
        gateway.Response.SetResult(88);
        await retry;
        Check(model.SavedOrderId == 88 && model.OrderDetails.Count == 0 && !model.IsSaving && model.ErrorMessage == "",
            "Retry after conflict succeeds without recreating the view model");
        Fill();
        var timeoutContext = model.FormContext;
        gateway.Response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var timeout = model.Send();
        gateway.Response.SetException(new TaskCanceledException("Timeout"));
        await timeout;
        Check(!model.IsSaving && ReferenceEquals(timeoutContext, model.FormContext) && model.OrderDetails.Count == 1 &&
            model.ErrorMessage.Contains("tardó demasiado"), "Timeout preserves the draft, releases saving state and avoids an unhandled UI exception");
        int before = gateway.Requests;
        model.NewOrder();
        Check(gateway.Requests == before && model.OrderDetails.Count == 0 && model.CustomerId == "" && model.Total == 0,
            "New order discards only the local draft without sending any request");
    }

    private sealed class GatewayProbe : ICreateOrderGateway
    {
        public int Requests { get; private set; }
        public TaskCompletionSource<int> Response { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<int> CreateOrderAsync(CreateOrderDto order) { Requests++; return Response.Task; }
        public Task<TaxSettings> GetTaxSettingsAsync() => Task.FromResult(new TaxSettings(true, .15m));
        public Task<SearchPage> SearchAsync(bool products, string id, string name, int page) => throw new NotSupportedException();
        public Task<InvoiceDto?> GetInvoiceAsync(int id) => throw new NotSupportedException();
        public Task<byte[]> GetInvoicePdfAsync(int id) => throw new NotSupportedException();
        public Task<InvoicePage> FilterInvoicesAsync(InvoiceSearchFilters filters, int page) => throw new NotSupportedException();
    }
    private sealed class ValidatorProbe : IModelValidatorHub<CreateOrderViewModel>
    {
        public IEnumerable<ValidationError> Errors => [];
        public Task<bool> Validate(CreateOrderViewModel model) => Task.FromResult(true);
    }
}
