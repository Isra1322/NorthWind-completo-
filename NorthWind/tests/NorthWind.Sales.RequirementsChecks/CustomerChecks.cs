using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces;
using NorthWind.Sales.Backend.Repositories.Entities;
using NorthWind.Sales.Entities.Dtos;
using NorthWind.Sales.Entities.Dtos.CreateOrder;
using NorthWind.Sales.Frontend.BusinessObjects.Interfaces;
using NorthWind.Sales.Frontend.Views.ViewModels.CreateOrder;
using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Validation.Entities.ValueObjects;

internal static class CustomerChecks
{
    public static async Task Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        static bool Valid(Customer customer) => Validator.TryValidateObject(customer,
            new ValidationContext(customer), new List<ValidationResult>(), true);
        Check(Valid(new() { Id = "TEST1", Name = "Comercial de demostración" }),
            "Legacy customers may omit optional identification and contact fields");
        foreach (string name in new[] { "", "   ", "\t\r\n", new string('a', 101) })
            Check(!Valid(new() { Name = name }), "Blank or oversized customer name is rejected");
        Check(!Valid(new() { Name = "Demo", Email = "invalid" }) &&
            !Valid(new() { Name = "Demo", Email = "a b@example.invalid" }), "Invalid email is rejected");
        Check(!Valid(new() { Name = "Demo", Identification = "<script>" }) &&
            !Valid(new() { Name = "Demo", Identification = new string('a', 31) }), "Invalid identification format and length are rejected");
        Check(Valid(new() { Name = "Demo", Identification = "DEMO-PASSPORT-A1", Email = "demo@example.invalid" }),
            "General foreign or demonstration documents do not require an Ecuadorian cedula");
        foreach (string cedula in new[] { "0910000017", "0910000025", "0910000033", "0910000207" })
            Check(SearchInputRules.IsValidEcuadorianCedula(cedula), "Synthetic Ecuadorian cedula passes its checksum");
        Check(!SearchInputRules.IsValidEcuadorianCedula("0910000018") &&
            SearchInputRules.ValidateCustomerCedulaLookup("0910000018", "").Count == 0 &&
            SearchInputRules.ValidateCustomerCedulaLookup("091", "Demo").Count == 0,
            "Search accepts stored numeric documents; complete cedula validation remains available for operations that require it");
        Check(!Valid(new() { Name = "Demo", Phone = "1-----" }) &&
            !Valid(new() { Name = "Demo", Phone = "letters" }) &&
            !Valid(new() { Name = "Demo", Phone = new string('1', 16) }), "Phone requires 6 to 15 digits");
        Check(Valid(new() { Name = "Demo", Phone = "+1 202-555-0100" }), "Formatted fictitious telephone is accepted");
        Check(!Valid(new() { Name = "Demo", Address = new string('a', 251) }), "Oversized address is rejected");

        var customer = new LookupItem("TEST1", "Cliente ficticio", Identification: "DEMO-TEST1", Address: "Dirección ficticia",
            Email: "demo@example.invalid", Phone: "+1 202-555-0100", City: "Cuenca", Country: "Ecuador", PostalCode: "010101");
        var gateway = new GatewayProbe { Results = new([customer], 1, 1) };
        var model = new CreateOrderViewModel(gateway, new ValidatorProbe()) { ShipAddress = "Envío elegido" };
        Check(await model.SelectCustomerAsync(customer with { Name = "Nombre desactualizado" }) &&
            model.CustomerId == "TEST1" && model.SelectedCustomer == customer && model.ShipAddress == customer.Address &&
            model.ShipCity == customer.City && model.ShipCountry == customer.Country && model.ShipPostalCode == customer.PostalCode,
            "Selection reloads current master data and copies all available delivery fields");
        model.ShipAddress = "Entrega alternativa";
        model.ShipCity = "Quito";
        model.ShipCountry = "Ecuador";
        model.ShipPostalCode = "170101";
        var orderDto = (CreateOrderDto)model;
        Check(model.SelectedCustomer == customer && model.SelectedCustomer.Address == "Dirección ficticia" &&
            orderDto.CustomerId == customer.Id && orderDto.ShipAddress == "Entrega alternativa" &&
            orderDto.ShipCity == "Quito" && orderDto.ShipCountry == "Ecuador" && orderDto.ShipPostalCode == "170101",
            "Editing delivery only changes the order DTO and leaves customer master data intact");
        gateway.Results = new([], 1, 1);
        Check(!await model.SelectCustomerAsync(new("NONE1", "Inexistente")) && model.CustomerId == "TEST1",
            "Nonexistent customer cannot replace the selected customer");
        foreach (var error in new Exception[] { new HttpRequestException("Disconnected"), new TaskCanceledException("Timeout") })
        {
            gateway.Failure = error;
            Check(!await model.SelectCustomerAsync(customer) && !model.IsSelectingCustomer && model.CustomerId == "TEST1",
                "Failed or timed-out customer lookup preserves selection and releases the busy state");
        }
        var otherCustomer = new LookupItem("TEST2", "Otro cliente");
        gateway.Failure = null;
        gateway.Results = new([otherCustomer], 1, 1);
        Check(await model.SelectCustomerAsync(otherCustomer) && model.CustomerId == "TEST2" && model.SelectedCustomer == otherCustomer &&
            new[] { model.ShipAddress, model.ShipCity, model.ShipCountry, model.ShipPostalCode }.All(string.IsNullOrEmpty) &&
            model.SelectedCustomer.Identification is null && model.SelectedCustomer.Email is null && model.SelectedCustomer.Phone is null,
            "Changing to a customer with missing data clears all previous customer and delivery values");
        var incompatible = customer with { Address = new string('A', 61), City = new string('B', 16), Country = new string('C', 16), PostalCode = "AB 01234567890" };
        gateway.Results = new([incompatible], 1, 1);
        Check(await model.SelectCustomerAsync(incompatible) && model.SelectedCustomer == incompatible &&
            model.ShipAddress == incompatible.Address && model.ShipCity == incompatible.City &&
            model.ShipCountry == incompatible.Country && model.ShipPostalCode == incompatible.PostalCode,
            "Autocomplete preserves incompatible master data verbatim for correction, never silently truncates it");
        model.ShipAddress = "Entrega alternativa";
        model.ShipPostalCode = "0123456789";
        Check(model.SelectedCustomer == incompatible, "Correcting delivery leaves customer master data intact");
        model.NewOrder();
        Check(model.SelectedCustomer is null && model.CustomerId == "", "New order clears selected customer information");
        foreach (string customerId in new[] { "ALFKI", "ANATR", "BONAP" })
        {
            var selected = customer with { Id = customerId, Name = "Cliente " + customerId };
            gateway.Results = new([selected], 1, 1);
            Check(await model.SelectCustomerAsync(selected) && model.CustomerId == customerId &&
                model.SelectedCustomer == selected && model.ShipAddress == selected.Address &&
                model.ShipCity == selected.City && model.ShipPostalCode == "010101",
                customerId + ": selection retains row identity and autocompletes delivery, including leading postal zero");
        }

        using var provider = new ServiceCollection().AddPresenters().BuildServiceProvider();
        var presenter = provider.GetRequiredService<IInvoicePdfPresenter>();
        var invoice = new InvoiceDto(12, "TEST1", new DateTime(2026, 1, 1), "Envío ficticio", "Quito", "Ecuador", null,
            100, .15m, 13.50m, 103.50m, [new(1, "Producto", 100, 1)], true,
            "Cliente ficticio", "DEMO-TEST1", "Dirección ficticia", "demo@example.invalid", "+1 202-555-0100");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string pdf = Encoding.GetEncoding(1252).GetString(presenter.Render(invoice));
        Check(new[] { "Cliente ficticio", "DEMO-TEST1", "Dirección ficticia", "demo@example.invalid", "+1 202-555-0100", "103,50" }.All(pdf.Contains),
            "PDF includes customer name, identification, contact data and recorded total");
        var historical = invoice with { CustomerName = null, CustomerIdentification = null, CustomerAddress = null, CustomerEmail = null, CustomerPhone = null };
        Check(historical.CustomerDisplayName == "TEST1" && presenter.Render(historical).Length > 0 && historical.Total == invoice.Total,
            "Historical invoice remains printable with missing customer details and unchanged amounts");
    }

    private sealed class GatewayProbe : ICreateOrderGateway
    {
        public SearchPage Results { get; set; } = new([], 1, 1);
        public Exception? Failure { get; set; }
        public Task<SearchPage> SearchAsync(bool products, string id, string name, int page) =>
            Failure is null ? Task.FromResult(Results) : Task.FromException<SearchPage>(Failure);
        public Task<int> CreateOrderAsync(CreateOrderDto order) => throw new NotSupportedException();
        public Task<TaxSettings> GetTaxSettingsAsync() => throw new NotSupportedException();
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
