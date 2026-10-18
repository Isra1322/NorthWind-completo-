using System.Net;
using Microsoft.AspNetCore.Components.Forms;
using System.Resources;
using NorthWind.Membership.Frontend.RazorViews.ViewModels.UserRegistration;
using NorthWind.Membership.Frontend.RazorViews.WebApiGateways;
using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Validation.Entities.ValueObjects;

internal static class MembershipFormChecks
{
    public static async Task Run()
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        using var handler = new RegistrationHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var model = new UserRegistrationViewModel(new MembershipGateway(client), new ValidatorProbe())
        {
            FirstName = "José", LastName = "Pérez", Email = "model-check@invalid.example",
            Password = "Valid123!", PasswordConfirmation = "Valid123!"
        };
        var original = model.FormContext;
        original.NotifyFieldChanged(new(model, nameof(model.Email)));
        var previousErrors = new ValidationMessageStore(original);
        previousErrors.Add(new FieldIdentifier(model, nameof(model.Email)), "Previous error");
        var first = model.Register();
        Check(model.IsRegistering && handler.Requests == 1, "Register marks the model as busy while the backend response is pending");
        await model.Register();
        Check(handler.Requests == 1, "Overlapping registration calls send only one request");
        handler.Response.SetResult(new HttpResponseMessage(HttpStatusCode.OK));
        await first;
        Check(new[] { model.FirstName, model.LastName, model.Email, model.Password, model.PasswordConfirmation }.All(string.IsNullOrEmpty),
            "Successful response clears all five model values, including both passwords");
        Check(model.InformationMessage == "Usuario registrado correctamente." && !model.IsRegistering,
            "Successful response preserves the confirmation and releases the busy flag");
        Check(!ReferenceEquals(original, model.FormContext) && !model.FormContext.IsModified() && !model.FormContext.GetValidationMessages().Any(),
            "Successful response replaces EditContext and discards previous validation and modification state");
        model.FormContext.NotifyFieldChanged(new(model, nameof(model.FirstName)));
        Check(model.InformationMessage == "", "Editing a new draft clears an obsolete success message");

        using var failingHandler = new RegistrationHandler();
        using var failingClient = new HttpClient(failingHandler) { BaseAddress = new Uri("http://localhost/") };
        var failing = new UserRegistrationViewModel(new MembershipGateway(failingClient), new ValidatorProbe())
        {
            FirstName = "José", LastName = "Pérez", Email = "retained@invalid.example",
            Password = "Valid123!", PasswordConfirmation = "Valid123!"
        };
        var failureContext = failing.FormContext;
        var pendingFailure = failing.Register();
        failingHandler.Response.SetResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        try { await pendingFailure; throw new Exception("Expected backend failure"); }
        catch (HttpRequestException) { }
        Check(failing.FirstName == "José" && failing.LastName == "Pérez" && failing.Email == "retained@invalid.example" &&
            failing.Password == "Valid123!" && failing.PasswordConfirmation == "Valid123!" && ReferenceEquals(failureContext, failing.FormContext),
            "Failed response preserves all values and the original EditContext");
        Check(!failing.IsRegistering && failing.InformationMessage == "" && failingHandler.Requests == 1,
            "Failed response produces no success or automatic second request");
        var resources = new ResourceManager("NorthWind.Membership.Frontend.RazorViews.Resources.UserRegistrationMessages", typeof(UserRegistrationViewModel).Assembly);
        Check(resources.GetString("EmailLabel") == "Correo electrónico" && resources.GetString("PasswordLabel") == "Contraseña" &&
            resources.GetString("PasswordConfirmationLabel") == "Confirmar contraseña" &&
            resources.GetString("ConfirmPasswordErrorMessage") == "Las contraseñas no coinciden.",
            "Localized registration labels and validation message contain correct Spanish characters");
        Check(typeof(UserRegistrationViewModel).Assembly.GetName().Version == new Version(2, 1, 0, 0),
            "Tests load Membership.Frontend.RazorViews version 2.1.0 with registration limits");
    }

    private sealed class RegistrationHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public TaskCompletionSource<HttpResponseMessage> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Response.Task;
        }
    }

    private sealed class ValidatorProbe : IModelValidatorHub<UserRegistrationViewModel>
    {
        public IEnumerable<ValidationError> Errors => [];
        public Task<bool> Validate(UserRegistrationViewModel model) => Task.FromResult(true);
    }
}
