using Microsoft.Extensions.DependencyInjection;
using NorthWind.Membership.Entities.Dtos.UserLogin;
using NorthWind.Membership.Frontend.RazorViews.ViewModels.UserRegistration;
using NorthWind.Sales.Entities.Dtos;
using NorthWind.Sales.Entities.Dtos.CreateOrder;
using NorthWind.Sales.Frontend.BusinessObjects.Interfaces;
using NorthWind.Sales.Frontend.Views.ViewModels.CreateOrder;
using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Validation.Entities.Abstractions;

internal static class ProgressiveValidationChecks
{
    public static async Task Run()
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        var services = new ServiceCollection();
        services.AddLogging().AddValidationService().AddValidators().AddViewsServices()
            .AddMembershipServices(client => client.BaseAddress = new Uri("http://localhost/"));
        services.AddSingleton<ICreateOrderGateway>(new GatewayProbe());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var registration = scope.ServiceProvider.GetRequiredService<UserRegistrationViewModel>();
        using (var component = new NorthWind.RazorComponents.Validators.ModelValidator<UserRegistrationViewModel>())
        {
            var context = registration.FormContext;
            var type = component.GetType();
            type.GetProperty("CurrentEditContext")!.SetValue(component, context);
            type.GetProperty("Validator")!.SetValue(component, registration.Validator);
            type.GetMethod("OnParametersSet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(component, null);
            var field = new Microsoft.AspNetCore.Components.Forms.FieldIdentifier(registration, nameof(registration.Email));
            string Css() => Microsoft.AspNetCore.Components.Forms.EditContextFieldClassExtensions.FieldCssClass(context, field);
            Check(Css() == "", "Untouched registration field has no validity styling");
            registration.Email = "incompleto";
            context.NotifyFieldChanged(field);
            Check(Css() == "modified", "Incomplete email stays neutral while editing");
            await component.ValidateFieldAsync(() => registration.Email);
            Check(Css().Split(' ').Contains("invalid"), "Blur marks incomplete email invalid");
            registration.Email = "ventas+quito@empresa.com";
            context.NotifyFieldChanged(field);
            Check(Css() == "modified" && !context.GetValidationMessages(field).Any(), "Editing clears stale errors without premature valid styling");
            await component.ValidateFieldAsync(() => registration.Email);
            Check(Css().Split(' ').Contains("valid"), "Valid email receives valid styling after complete validation");
            registration.Email = "";
        }
        var nameErrors = (await registration.Validator.ValidateField(registration, nameof(registration.FirstName))).ToArray();
        Check(nameErrors.Length == 1 && nameErrors[0].PropertyName == nameof(registration.FirstName),
            "Blur validation selects only the requested registration field");
        registration.Password = "abc";
        var passwordErrors = (await registration.Validator.ValidateField(registration, nameof(registration.Password))).ToArray();
        Check(passwordErrors.Length == 4 && passwordErrors.All(e => e.PropertyName == nameof(registration.Password)),
            "Password field selection preserves all unmet security rules without unrelated errors");
        var order = scope.ServiceProvider.GetRequiredService<CreateOrderViewModel>();
        foreach (string field in new[] { nameof(order.CustomerId), nameof(order.ShipAddress), nameof(order.ShipCity), nameof(order.ShipCountry), nameof(order.OrderDetails) })
        {
            var errors = (await order.Validator.ValidateField(order, field)).ToArray();
            Check(errors.Length == 1 && errors[0].PropertyName == field,
                "Empty order field reports only its required error: " + field);
        }
        order.ShipCity = "123";
        var cityErrors = (await order.Validator.ValidateField(order, nameof(order.ShipCity))).ToArray();
        Check(cityErrors.Length == 1 && cityErrors[0].Message.Contains("letras"), "Nonempty invalid city retains its format rule");
        order.ShipCity = "Quito";
        Check(!(await order.Validator.ValidateField(order, nameof(order.ShipCity))).Any(), "Correcting one field clears that field independently");
        var deliveryValidator = scope.ServiceProvider.GetRequiredService<IModelValidatorHub<CreateOrderDto>>();
        foreach (string field in new[] { nameof(order.ShipCity), nameof(order.ShipCountry) })
        {
            foreach (string value in new[] { "", " ", "Ab", "----", "....", "''''", "++++", "12345", "Ecuador123", "Quito+++", "--''..", "@@@", "-Quito", "'Quito", "Quito-", "Quito'", "San--José", "San -José", "Baños de Agua Santa" })
            {
                order.ShipCity = order.ShipCountry = value;
                var errors = (await order.Validator.ValidateField(order, field)).ToArray();
                var backendErrors = (await deliveryValidator.ValidateField(new("ALFKI", "Calle Loja 101", value, value, "001", [new(1, 35, 1)]), field)).ToArray();
                Check(errors.Length == 1 && backendErrors.Length == 1,
                    "Frontend/backend reject invalid or overlength delivery place once: " + field + " = " + value);
            }
            foreach (string value in new[] { "Ambato", "Quito", "Santo Domingo", "Baños", "Ecuador", "Costa Rica", "Reino Unido", "Timor-Leste", "L'Aquila", "L’Haÿ", new string('a', 15) })
            {
                order.ShipCity = order.ShipCountry = value;
                Check(!(await order.Validator.ValidateField(order, field)).Any() &&
                    !(await deliveryValidator.ValidateField(new("ALFKI", "Calle Loja 101", value, value, "001", [new(1, 35, 1)]), field)).Any(),
                    "Frontend/backend accept valid delivery place: " + field + " = " + value);
            }
        }
        order.ShipCity = "Quito";
        order.ShipCountry = "Ecuador";
        order.OrderDetails.Add(new() { ProductId = 1, UnitPrice = 35, UnitsInStock = 1, Quantity = 0 });
        var quantityErrors = (await order.Validator.ValidateField(order, "OrderDetails[0].Quantity")).ToArray();
        Check(quantityErrors.Length == 1 && quantityErrors[0].PropertyName == "OrderDetails[0].Quantity",
            "Field selection resolves nested product quantities and retains positive-quantity validation");
        order.OrderDetails[0].Quantity = 2;
        quantityErrors = (await order.Validator.ValidateField(order, "OrderDetails[0].Quantity")).ToArray();
        Check(quantityErrors.Length == 1 && quantityErrors[0].Message.Contains("stock"), "Nested field validation preserves stock limits");
        var login = scope.ServiceProvider.GetRequiredService<IModelValidatorHub<UserCredentialsDto>>();
        var loginErrors = (await login.ValidateField(new("", ""), "Email")).ToArray();
        Check(loginErrors.Length == 1 && loginErrors[0].PropertyName == "Email", "Empty login email has only its required error");
        Check(!await order.Validator.Validate(order), "Full-submit validation still rejects an incomplete order");
        var detailValidator = new DetailProbeValidator(scope.ServiceProvider.GetRequiredService<IValidationService<DetailProbe>>());
        var parentValidator = new ParentProbeValidator(scope.ServiceProvider.GetRequiredService<IValidationService<ParentProbe>>(), detailValidator);
        var parent = new ParentProbe();
        var detailErrors = (await parentValidator.ValidateField(parent, "Items[0].Quantity")).ToArray();
        Check(detailErrors.Length == 1 && detailValidator.PriceChecks == 0,
            "Nested field selection does not execute rules for unrelated product fields");
        await parentValidator.Validate(parent);
        Check(detailValidator.PriceChecks == 1, "Full-submit validation still executes every nested product rule");
    }
    private sealed class DetailProbe { public int Quantity { get; set; } public decimal Price { get; set; } }
    private sealed class ParentProbe { public List<DetailProbe> Items { get; } = [new()]; }
    private sealed class DetailProbeValidator : AbstractModelValidator<DetailProbe>
    {
        public int PriceChecks { get; private set; }
        public DetailProbeValidator(IValidationService<DetailProbe> service) : base(service)
        {
            AddRuleFor(item => item.Quantity).GreaterThan(0, "Quantity required");
            AddRuleFor(item => item.Price).Must(price => { PriceChecks++; return price > 0; }, "Price required");
        }
    }
    private sealed class ParentProbeValidator : AbstractModelValidator<ParentProbe>
    {
        public ParentProbeValidator(IValidationService<ParentProbe> service, IModelValidator<DetailProbe> detailValidator) : base(service) =>
            AddRuleForEach(parent => parent.Items).SetValidator(detailValidator);
    }
    private sealed class GatewayProbe : ICreateOrderGateway
    {
        public Task<int> CreateOrderAsync(CreateOrderDto order) => throw new NotSupportedException();
        public Task<TaxSettings> GetTaxSettingsAsync() => Task.FromResult(new TaxSettings(false, 0));
        public Task<SearchPage> SearchAsync(bool products, string id, string name, int page) => throw new NotSupportedException();
        public Task<InvoiceDto?> GetInvoiceAsync(int id) => throw new NotSupportedException();
        public Task<byte[]> GetInvoicePdfAsync(int id) => throw new NotSupportedException();
        public Task<InvoicePage> FilterInvoicesAsync(InvoiceSearchFilters filters, int page) => throw new NotSupportedException();
    }
}
