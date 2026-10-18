using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.JSInterop;
using NorthWind.Sales.Entities.Dtos.CreateOrder;
using NorthWind.Sales.Frontend.BusinessObjects.Interfaces;
using NorthWind.Sales.Frontend.Views.Components;
using NorthWind.Sales.Frontend.Views.ViewModels.CreateOrder;
using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Validation.Entities.ValueObjects;

internal static class OrderRecoveryChecks
{
    public static async Task Run()
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        foreach (bool malformedResponse in new[] { false, true })
        {
            using var handler = new LostResponseHandler(malformedResponse);
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://northwind.test/") };
            var type = Type.GetType("NorthWind.Sales.Frontend.WebApiGateways.CreateOrderGateway, NorthWind.Sales.Frontend.WebApiGateways", true)!;
            var gateway = (ICreateOrderGateway)Activator.CreateInstance(type, client)!;
            var model = new CreateOrderViewModel(gateway, new Validator());
            await model.LoadTaxSettingsAsync();
            model.CustomerId = "ANATR";
            model.ShipAddress = "Calle 123";
            model.ShipCity = "Quito";
            model.ShipCountry = "Ecuador";
            foreach (var item in new[] { (1, "Chai", 35m), (3, "Aniseed Syrup", 65m),
                (4, "Chef Anton's Cajun Seasoning", 75m), (10, "Ikura", 35m) })
                model.SelectProduct(new(item.Item1.ToString(), item.Item2, item.Item3, 10), null);
            Guid? requestId = ((CreateOrderDto)model).RequestId;
            await model.Send();
            Check(model.SavedOrderId == 987 && model.InformationMessage.Contains("987") && handler.Posts == 1 &&
                handler.RecoveredRequestId == requestId, "Lost/malformed HTTP response recovers order number with GET and only one POST");
            Check(((CreateOrderDto)model).RequestId != requestId, "Successful order resets the next draft's request reference");
            var js = new JsProbe();
            var component = new CreateOrderComponent { JS = js };
            typeof(CreateOrderComponent).GetProperty(nameof(CreateOrderComponent.Order))!.SetValue(component, model);
            async Task Invoke(string method, params object[] parameters) => await (Task)typeof(CreateOrderComponent)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, parameters)!;
            await Invoke("PrepareAndDeliverInvoiceAsync", 987);
            Check(model.SavedOrderId == 987 && model.InformationMessage.Contains("987") && handler.Posts == 1,
                "PDF failure preserves success and the saved order number");
            await Invoke("RetryInvoicePdfAsync");
            Check(js.Calls.Contains("create") && handler.Posts == 1 && handler.PdfRequests == 2,
                "Manual PDF recovery fetches only the invoice without saving another order");
            await Invoke("PrepareAndDeliverInvoiceAsync", 987);
            Check(js.Calls.Contains("showReserved") && js.Calls.Contains("download") &&
                model.InformationMessage.Contains("descargado") && handler.Posts == 1,
                "Successful PDF invokes opening and download, and displays the order number");
            await component.DisposeAsync();
        }
    }

    private sealed class LostResponseHandler(bool malformed) : HttpMessageHandler
    {
        public int Posts { get; private set; }
        public int PdfRequests { get; private set; }
        public Guid? RecoveredRequestId { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                Posts++;
                if (!malformed) throw new HttpRequestException("Response lost after commit");
                return Task.FromResult(Json("invalid JSON"));
            }
            string path = request.RequestUri!.AbsolutePath;
            if (path.Contains("/requests/"))
            {
                RecoveredRequestId = Guid.Parse(path.Split('/')[^1]);
                return Task.FromResult(Json("987"));
            }
            if (path.EndsWith("/tax")) return Task.FromResult(Json("{\"enabled\":true,\"rate\":0.15}"));
            if (path.EndsWith("/pdf"))
            {
                PdfRequests++;
                return Task.FromResult(PdfRequests == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) :
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF")) });
            }
            throw new Exception("Unexpected request " + path);
        }
        private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
            { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    }
    private sealed class Validator : IModelValidatorHub<CreateOrderViewModel>
    {
        public IEnumerable<ValidationError> Errors => [];
        public Task<bool> Validate(CreateOrderViewModel model) => Task.FromResult(true);
    }
    private sealed class JsProbe : IJSRuntime, IJSObjectReference
    {
        public List<string> Calls { get; } = [];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(identifier);
            object? result = identifier switch
            {
                "import" => this,
                "create" => "blob:invoice",
                "showReserved" or "download" => true,
                _ => default(TValue)
            };
            return ValueTask.FromResult((TValue)result!);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
