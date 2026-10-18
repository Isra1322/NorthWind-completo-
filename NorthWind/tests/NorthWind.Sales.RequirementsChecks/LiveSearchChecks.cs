using System.Reflection;
using Microsoft.AspNetCore.Components;
using NorthWind.Sales.Entities.Dtos;
using NorthWind.Sales.Entities.Dtos.CreateOrder;
using NorthWind.Sales.Frontend.BusinessObjects.Interfaces;
using NorthWind.Sales.Frontend.Views.Components;
using NorthWind.Sales.Frontend.Views.ViewModels.CreateOrder;
using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Validation.Entities.ValueObjects;

internal static class LiveSearchChecks
{
    public static async Task Run()
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        var gateway = new Gateway();
        foreach (string invalid in new[] { "123", "Ana12", "---", " . ' ", "   ", "Ana+", "Ana\t" })
            Check(SearchInputRules.ValidateCustomerCedulaLookup("", invalid).ContainsKey("Name"), "Customer lookup rejects invalid name: " + invalid);
        foreach (string valid in new[] { "José Ñúñez", "María-José", "O’Connor", "Comercial S.A." })
            Check(SearchInputRules.ValidateCustomerCedulaLookup("001", valid).Count == 0, "Customer lookup accepts partial names and leading zeros: " + valid);
        foreach (string invalid in new[] { "---", "+++", "   ", "@*", "Chai\n", "''''", "////", "--''//", ".....", "Coca----Cola", "Chai///", "Chai + + +" })
            Check(SearchInputRules.ValidateLookup(true, "", invalid).ContainsKey("Name"), "Product lookup rejects invalid name: " + invalid);
        foreach (string valid in new[] { "Chef Anton's Cajun Seasoning", "Queso Cabrales", "Chocolate 70%", "Mix + vitamina C", "Gustaf's Knäckebröd", "Chai", "Aniseed Syrup", "Coca-Cola", "Producto 123", "Café molido", "7 Up", "Chocolate (70%)", "Aceite 1/2 L", "Salsa \"Extra\"" })
            Check(SearchInputRules.ValidateLookup(true, "01", valid).Count == 0, "Product lookup accepts commercial names: " + valid);
        Check(SearchInputRules.ValidateCustomerCedulaLookup("01a", "").ContainsKey("Identification"), "Backend rejects letters in identification");
        var component = new InvoiceList();
        typeof(InvoiceList).GetProperty("Gateway", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(component, gateway);
        Task Invoke(string method, params object[] args) => (Task)typeof(InvoiceList)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, args)!;
        T? Field<T>(string name) => (T?)typeof(InvoiceList).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component);
        async Task WaitFor(int count)
        {
            for (int attempt = 0; gateway.Calls.Count < count && attempt < 100; attempt++) await Task.Delay(10);
            Check(gateway.Calls.Count == count, "Debounce emits the expected number of requests");
        }
        InvoicePage Page(int count) => new([], 1, 1, count, DateOnly.FromDateTime(DateTime.Today));
        var firstKeystroke = Invoke("SetIdentification", "0");
        var secondKeystroke = Invoke("SetIdentification", "0910");
        await WaitFor(1);
        Check(gateway.Calls[0].Filters.Identification == "0910" && gateway.Calls[0].Page == 1,
            "Typing is debounced and resets invoice pagination");
        var newer = Invoke("SetIdentification", "0910000017");
        await WaitFor(2);
        Check(gateway.Calls[0].Token.IsCancellationRequested, "Changing a filter cancels the previous HTTP request");
        gateway.Calls[1].Response.SetResult(Page(2));
        await newer;
        gateway.Calls[0].Response.SetResult(Page(99));
        await Task.WhenAll(firstKeystroke, secondKeystroke);
        Check(Field<InvoicePage>("Result")?.TotalCount == 2, "A late response cannot replace the newest results or counter");
        var deletion = Invoke("SetIdentification", "091");
        await WaitFor(3); gateway.Calls[2].Response.SetResult(Page(3)); await deletion;
        Check(gateway.Calls[2].Filters.Identification == "091", "Deleting characters updates search automatically");
        var criterion = Invoke("ChangeCriterion", new ChangeEventArgs { Value = "InvoiceNumber" });
        await WaitFor(4); gateway.Calls[3].Response.SetResult(Page(4)); await criterion;
        Check(gateway.Calls[3].Filters.Identification == "" && Field<string>("SearchValue") == "",
            "Changing search criterion clears the previous value");
        var number = Invoke("SetInvoiceNumber", "7");
        await WaitFor(5); gateway.Calls[4].Response.SetResult(Page(5)); await number;
        var date = Invoke("SetIssueDate", new ChangeEventArgs { Value = "2026-01-01" });
        await WaitFor(6); gateway.Calls[5].Response.SetResult(Page(6)); await date;
        Check(gateway.Calls[5].Filters is { InvoiceNumber: "7", IssueDate: "2026-01-01" }, "Date combines with the current invoice filter");
        var clearDate = Invoke("SetIssueDate", new ChangeEventArgs { Value = "" });
        await WaitFor(7); gateway.Calls[6].Response.SetResult(Page(7)); await clearDate;
        Check(gateway.Calls[6].Filters is { InvoiceNumber: "7", IssueDate: "" }, "Clearing date preserves other criteria");
        var clear = Invoke("ClearAsync");
        await WaitFor(8); gateway.Calls[7].Response.SetResult(Page(8)); await clear;
        Check(gateway.Calls[7].Filters == new InvoiceSearchFilters() && gateway.Calls[7].Page == 1,
            "Clear restores all invoices and the first page");
        await component.DisposeAsync();
        foreach (bool products in new[] { false, true })
        {
            var dialog = new SalesSearchDialog();
            typeof(SalesSearchDialog).GetProperty("Products")!.SetValue(dialog, products);
            typeof(SalesSearchDialog).GetProperty("Order")!.SetValue(dialog, new CreateOrderViewModel(gateway, new Validator()));
            Task Change(string method, string value) => (Task)typeof(SalesSearchDialog)
                .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(dialog, [value])!;
            int count = gateway.Lookups.Count;
            var left = Change("IdChanged", "0");
            var right = Change("IdChanged", "01");
            await Task.WhenAll(left, right);
            Check(gateway.Lookups.Count == count + 1 && gateway.Lookups[^1] == (products, "01", "", 1),
                "Client/product dialogs debounce typing, preserve zeros and reset pagination");
            await Change("NameChanged", products ? "Chef Anton's" : "Comercial S.A.");
            Check(gateway.Lookups[^1].Id == "01" && gateway.Lookups[^1].Name.Length > 0,
                "Client/product dialogs combine code or identification with partial name");
            await Change("IdChanged", "");
            Check(gateway.Lookups[^1].Id == "" && gateway.Lookups[^1].Page == 1,
                "Clearing dialog text refreshes results without a Search click");
            count = gateway.Lookups.Count;
            await Change("NameChanged", "---");
            Check(gateway.Lookups.Count == count, "Separator-only search does not query the backend");
            if (!products)
            {
                await Change("NameChanged", "O'");
                Check(gateway.Lookups.Count == count, "Incomplete customer apostrophe does not query the backend");
                await Change("NameChanged", "O'Connor");
                Check(gateway.Lookups[^1].Name == "O'Connor", "Completing a legitimate customer name resumes live search");
                await Change("NameChanged", "--Jean---Pierre123");
                Check(gateway.Lookups[^1].Name == "Jean-Pierre", "Customer component sanitizes contextual separators and digits");
                await Change("NameChanged", "");
                Check(gateway.Lookups[^1].Name == "" && gateway.Lookups[^1].Page == 1, "Clearing customer name still refreshes the first page");
            }
            else
            {
                foreach (string invalid in new[] { "----", "''''", "////", "--''//", ".....", "++++", "@@@@" })
                    await Change("NameChanged", invalid);
                Check(gateway.Lookups.Count == count, "Product symbol-only input causes no unnecessary queries");
                foreach (string valid in new[] { "Chai", "Chef Anton's Cajun Seasoning", "Coca-Cola", "Producto 123", "Café molido", "7 Up" })
                {
                    await Change("NameChanged", valid);
                    Check(gateway.Lookups[^1].Name == valid && gateway.Lookups[^1].Page == 1, "Live product lookup preserves commercial name and resets pagination: " + valid);
                }
                await (Task)typeof(SalesSearchDialog).GetMethod("Search", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(dialog, [2])!;
                Check(gateway.Lookups[^1].Page == 2 && gateway.Lookups[^1].Name == "7 Up", "Product pagination retains the name filter");
                await Change("NameChanged", "@@Coca----Cola");
                Check(gateway.Lookups[^1].Name == "Coca-Cola" && gateway.Lookups[^1].Page == 1, "Product input sanitizes punctuation before querying");
                await Change("NameChanged", "");
                Check(gateway.Lookups[^1].Name == "" && gateway.Lookups[^1].Page == 1, "Clearing product name refreshes first page");

                var order = dialog.Order;
                var orderComponent = new CreateOrderComponent();
                typeof(CreateOrderComponent).GetProperty("Order")!.SetValue(orderComponent, order);
                var select = typeof(CreateOrderComponent).GetMethod("SelectItem", BindingFlags.NonPublic | BindingFlags.Instance)!;
                Task SelectProduct(LookupItem item)
                {
                    typeof(CreateOrderComponent).GetMethod("OpenSearch", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(orderComponent, [true, null]);
                    return (Task)select.Invoke(orderComponent, [item])!;
                }
                await SelectProduct(new("1", "Chai", 35m, 10));
                Check(order.OrderDetails.Count == 1 && order.OrderDetails[0].ProductName == "Chai" && order.OrderDetails[0].Quantity == 1,
                    "Product selection handler adds the selected item with its price and stock");
                order.OrderDetails[0].Quantity = 3;
                await SelectProduct(new("2", "Chef Anton's Cajun Seasoning", 22m, 5));
                await SelectProduct(new("3", "Sin stock", 10m, 0));
                Check(order.OrderDetails.Count == 2 && order.OrderDetails[0].Quantity == 3 && order.OrderDetails[1].ProductId == 2,
                    "Selecting another product preserves existing quantity and rejects zero stock");
            }
            dialog.Dispose();
        }
    }

    private sealed class Gateway : ICreateOrderGateway
    {
        public record Call(InvoiceSearchFilters Filters, int Page, CancellationToken Token, TaskCompletionSource<InvoicePage> Response);
        public List<Call> Calls { get; } = [];
        public List<(bool Products, string Id, string Name, int Page)> Lookups { get; } = [];
        public Task<InvoicePage> FilterInvoicesAsync(InvoiceSearchFilters filters, int page, CancellationToken cancellationToken)
        {
            var call = new Call(filters, page, cancellationToken, new(TaskCreationOptions.RunContinuationsAsynchronously));
            Calls.Add(call);
            return call.Response.Task; // Deliberately ignore cancellation to test stale-response protection.
        }
        public Task<InvoicePage> FilterInvoicesAsync(InvoiceSearchFilters filters, int page) => FilterInvoicesAsync(filters, page, default);
        public Task<int> CreateOrderAsync(CreateOrderDto order) => throw new NotSupportedException();
        public Task<SearchPage> SearchAsync(bool products, string id, string name, int page)
        {
            Lookups.Add((products, id, name, page));
            return Task.FromResult(new SearchPage([], page, 1));
        }
        public Task<TaxSettings> GetTaxSettingsAsync() => throw new NotSupportedException();
        public Task<InvoiceDto?> GetInvoiceAsync(int id) => throw new NotSupportedException();
        public Task<byte[]> GetInvoicePdfAsync(int id) => throw new NotSupportedException();
    }
    private sealed class Validator : IModelValidatorHub<CreateOrderViewModel>
    {
        public IEnumerable<ValidationError> Errors => [];
        public Task<bool> Validate(CreateOrderViewModel model) => Task.FromResult(true);
    }
}
