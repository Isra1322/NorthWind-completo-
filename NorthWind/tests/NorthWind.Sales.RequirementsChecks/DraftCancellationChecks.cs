using Microsoft.AspNetCore.Components.Forms;
using NorthWind.Sales.Entities.Dtos;
using NorthWind.Sales.Entities.Dtos.CreateOrder;
using NorthWind.Sales.Frontend.BusinessObjects.Interfaces;
using NorthWind.Sales.Frontend.Views.ViewModels.CreateOrder;
using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Validation.Entities.ValueObjects;

internal static class DraftCancellationChecks
{
    public static async Task Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        var gateway = new GatewayProbe();
        var model = new CreateOrderViewModel(gateway, new ValidatorProbe());
        await model.LoadTaxSettingsAsync();
        await model.SelectCustomerAsync(gateway.Customer);
        model.SelectProduct(new("1", "Chai", 35m, 10), null);
        model.OrderDetails[0].Quantity = 2;
        var detail = model.OrderDetails.Single();
        var context = model.FormContext;
        context.NotifyFieldChanged(new(model, nameof(model.ShipCity)));
        new ValidationMessageStore(context).Add(new FieldIdentifier(model, nameof(model.CustomerId)), "Previous validation error");
        model.SelectProduct(new("1", "Chai", 35m, 10), null); // Existing duplicate-product error.
        var error = model.ErrorMessage;
        int calls = gateway.Calls;
        model.RequestDraftCancellation();
        Check(model.IsCancellationPending && model.SelectedCustomer == gateway.Customer &&
            model.CustomerId == "ALFKI" && model.ShipAddress == "Calle 123" && model.ShipCity == "Quito" &&
            model.ShipCountry == "Ecuador" && model.ShipPostalCode == "010101" &&
            ReferenceEquals(detail, model.OrderDetails.Single()) && detail.Quantity == 2 && model.Total == 80.50m &&
            model.ErrorMessage == error && ReferenceEquals(context, model.FormContext) && context.GetValidationMessages().Any(),
            "Opening draft cancellation preserves customer, delivery, products, totals, messages and validation");
        await model.Send();
        model.NewOrder();
        Check(gateway.Calls == calls && model.Total == 80.50m && model.IsCancellationPending,
            "Pending confirmation blocks sending and bypassing confirmation with NewOrder");
        model.DismissDraftCancellation();
        model.ConfirmDraftCancellation();
        Check(!model.IsCancellationPending && ReferenceEquals(detail, model.OrderDetails.Single()) &&
            ReferenceEquals(context, model.FormContext) && model.Total == 80.50m && model.ErrorMessage == error,
            "Dismissal preserves the draft; stale confirmation cannot erase it");
        model.RequestDraftCancellation();
        model.ConfirmDraftCancellation();
        Check(!model.IsCancellationPending && model.SelectedCustomer is null &&
            new[] { model.CustomerId, model.ShipAddress, model.ShipCity, model.ShipCountry, model.ShipPostalCode }.All(string.IsNullOrEmpty) &&
            model.OrderDetails.Count == 0 && model.Subtotal == 0 && model.TaxAmount == 0 && model.Total == 0 &&
            model.InformationMessage == "" && model.ErrorMessage == "" && !model.FormContext.GetValidationMessages().Any() &&
            !model.FormContext.IsModified() && !ReferenceEquals(context, model.FormContext) && model.TaxSettings!.Rate == .15m,
            "Confirmed cancellation resets all draft data, validation and messages while preserving tax configuration");
        Check(gateway.Calls == calls && gateway.SavedOrders.Count == 0 && gateway.Stock == 10,
            "Cancellation makes no gateway calls, saves no order and cannot alter inventory");
        await model.SelectCustomerAsync(gateway.Customer);
        model.SelectProduct(new("1", "Chai", 35m, 10), null);
        Check(model.OrderDetails.Single().Quantity == 1, "A fresh product starts with quantity one after cancellation");
        var sending = model.Send();
        model.RequestDraftCancellation();
        model.ConfirmDraftCancellation();
        await model.Send();
        Check(model.IsSaving && !model.IsCancellationPending && model.CustomerId == "ALFKI" && model.OrderDetails.Count == 1 && gateway.SaveCalls == 1,
            "Cancellation and duplicate submission are blocked while saving; pending draft remains intact");
        gateway.Response.SetResult(123);
        await sending;
        var saved = gateway.SavedOrders.Single();
        var stock = gateway.Stock;
        calls = gateway.Calls;
        model.RequestDraftCancellation();
        model.ConfirmDraftCancellation();
        Check(model.SavedOrderId == 123 && gateway.SavedOrders.Single() == saved && gateway.Stock == stock && gateway.Calls == calls &&
            model.InformationMessage == "" && model.ErrorMessage == "",
            "Cancelling after a successful save clears only local messages and never changes the saved order or stock");
    }

    private sealed class GatewayProbe : ICreateOrderGateway
    {
        public LookupItem Customer { get; } = new("ALFKI", "Cliente de prueba", Address: "Calle 123", City: "Quito", Country: "Ecuador", PostalCode: "010101");
        public int Calls { get; private set; }
        public int SaveCalls { get; private set; }
        public int Stock { get; private set; } = 10;
        public List<CreateOrderDto> SavedOrders { get; } = [];
        public TaskCompletionSource<int> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<int> CreateOrderAsync(CreateOrderDto order)
        {
            Calls++; SaveCalls++;
            var snapshot = new CreateOrderDto(order.CustomerId, order.ShipAddress, order.ShipCity,
                order.ShipCountry, order.ShipPostalCode, order.OrderDetails.ToArray());
            int id = await Response.Task;
            SavedOrders.Add(snapshot);
            Stock -= snapshot.OrderDetails.Sum(d => d.Quantity);
            return id;
        }
        public Task<TaxSettings> GetTaxSettingsAsync() { Calls++; return Task.FromResult(new TaxSettings(true, .15m)); }
        public Task<SearchPage> SearchAsync(bool products, string id, string name, int page)
        { Calls++; return Task.FromResult(new SearchPage([Customer], 1, 1)); }
        public Task<InvoiceDto?> GetInvoiceAsync(int id) { Calls++; throw new NotSupportedException(); }
        public Task<byte[]> GetInvoicePdfAsync(int id) { Calls++; throw new NotSupportedException(); }
        public Task<InvoicePage> FilterInvoicesAsync(InvoiceSearchFilters filters, int page) { Calls++; throw new NotSupportedException(); }
    }
    private sealed class ValidatorProbe : IModelValidatorHub<CreateOrderViewModel>
    {
        public IEnumerable<ValidationError> Errors => [];
        public Task<bool> Validate(CreateOrderViewModel model) => Task.FromResult(true);
    }
}
