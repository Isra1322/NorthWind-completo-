using Microsoft.Extensions.DependencyInjection;
using NorthWind.Membership.Entities.Dtos.UserRegistration;
using NorthWind.Membership.Frontend.RazorViews.ViewModels.UserRegistration;
using NorthWind.Validation.Entities.Interfaces;

internal static class RegistrationValidationChecks
{
    public static async Task Run()
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddValidationService().AddValidators().AddViewsServices()
            .AddMembershipServices(client => client.BaseAddress = new Uri("http://localhost/"))
            .AddMembershipValidators().AddValidators();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var validators = serviceProvider.GetServices<IModelValidator<UserRegistrationDto>>().ToArray();
        Check(validators.Length == 2 && validators.Select(v => v.GetType()).Distinct().Count() == 2,
            "Repeated dependency registration keeps one instance per validator type");
        Check(!ReferenceEquals(serviceProvider.GetRequiredService<IValidationService<UserRegistrationDto>>(),
            serviceProvider.GetRequiredService<IValidationService<UserRegistrationDto>>()),
            "FluentValidation rule builders are not shared between validators");
        var model = serviceProvider.GetRequiredService<UserRegistrationViewModel>();
        var hub = model.Validator;
        await hub.Validate(model);
        var empty = hub.Errors.ToArray();
        Check(empty.Length == 5 && empty.GroupBy(e => e.PropertyName).All(g => g.Count() == 1),
            "Empty registration produces exactly one required error for each of the five fields");
        for (int i = 0; i < 4; i++) await hub.Validate(model);
        Check(hub.Errors.Count() == 5, "Repeated validation replaces errors instead of accumulating them");
        model.FirstName = "José";
        model.LastName = "Pérez";
        model.Email = "d";
        model.Password = model.PasswordConfirmation = "abc";
        await hub.Validate(model);
        var incomplete = hub.Errors.ToArray();
        Check(incomplete.Count(e => e.PropertyName == nameof(model.Email)) == 1 &&
            incomplete.Count(e => e.PropertyName == nameof(model.Password)) == 4 &&
            incomplete.DistinctBy(e => (e.PropertyName, e.Message)).Count() == incomplete.Length,
            "Invalid email appears once; incomplete password reports only four unmet requirements without duplicates");
        model.FirstName = "123";
        model.LastName = "456";
        await hub.Validate(model);
        Check(hub.Errors.Count(e => e.PropertyName == nameof(model.FirstName)) == 1 &&
            hub.Errors.Count(e => e.PropertyName == nameof(model.LastName)) == 1,
            "Each invalid name reports its character rule once");
        model.FirstName = "José";
        model.LastName = "Pérez";
        model.Email = "valid-check@northwind.com";
        model.Password = model.PasswordConfirmation = "Valid123!";
        Check(await hub.Validate(model) && !hub.Errors.Any(), "Correcting all fields clears all previous errors");
        var dtoHub = serviceProvider.GetRequiredService<IModelValidatorHub<UserRegistrationDto>>();
        foreach (string field in new[] { nameof(model.FirstName), nameof(model.LastName) })
        {
            foreach (string value in new[] { "Juan", "María José", "José Luis", "Ana-María", "O'Connor", "Pérez García", "Muñoz", "O'Neill", "Pérez-García", "J. Pérez" })
            {
                model.FirstName = model.LastName = value;
                var dto = new UserRegistrationDto(model.Email, model.Password, value, value);
                Check(!(await hub.ValidateField(model, field)).Any() && !(await dtoHub.ValidateField(dto, field)).Any(),
                    "Frontend and backend accept registration name: " + field + " = " + value);
            }
            foreach (string value in new[] { "12345", "----", "''''", "++++", "----''''", "María123", "Juan+", "Juan--Pérez", "Juan..Pérez", "Juan  Pérez", "'Juan", "Juan'", "Juan-", "Juan/@_{}[]" })
            {
                model.FirstName = model.LastName = value;
                var dto = new UserRegistrationDto(model.Email, model.Password, value, value);
                Check((await hub.ValidateField(model, field)).Count() == 1 && (await dtoHub.ValidateField(dto, field)).Count() == 1,
                    "Frontend and backend reject invalid registration name once: " + field + " = " + value);
            }
        }
        model.FirstName = "José";
        model.LastName = "Pérez";
        Check(hub is IDeferredFieldValidation timing && timing.DeferUntilBlur("Email") && !timing.DeferUntilBlur("FirstName") && !timing.DeferUntilBlur("Password"),
            "Only registration email defers editing errors until blur or submit");
        foreach (string email in new[] { "", " ", "oscar", "oscar@", "@gmail.com", "oscar@gmail", "oscar@@gmail.com", "oscar gmail@gmail.com", "oscar@gmail..com", "}}+{++}+'+", "a{b}@example.com", "a[b]@example.com", "a\\b@example.com", "\"a\"@example.com", ".a@example.com", "a..b@example.com", "a@-example.com", "a@example-.com", "----++++", "usuario@", "usuario", "usuario@@gmail.com", "usuario@gmail..com", "a\tb@gmail.com", "a\nb@gmail.com" })
        {
            model.Email = email;
            Check(!await hub.Validate(model) && hub.Errors.Count() == 1 && hub.Errors.Single().Message ==
                (string.IsNullOrWhiteSpace(email) ? "Ingrese su correo electrónico." : "Ingrese un correo electrónico válido."),
                "Registration rejects invalid email once in Spanish: " + email);
            Check((await dtoHub.ValidateField(new(email, model.Password, model.FirstName, model.LastName), "Email")).Count() == 1,
                "Backend rejects the same invalid email");
        }
        foreach (string email in new[] { "oscar@gmail.com", "maria.perez@hotmail.com", "juan_perez@outlook.com", "usuario123@empresa.com", "ventas+quito@empresa.com", "user-name@sub.empresa.com", "usuario@gmail.com", "nombre.apellido@outlook.com", "usuario+ventas@empresa.com", "_nombre@empresa.com", "user@xn--bcher-kva.de" })
        {
            model.Email = email;
            Check(await hub.Validate(model) && !(await hub.ValidateField(model, "Email")).Any(), "Registration accepts email: " + email);
        }
        model.Password = model.PasswordConfirmation = "Aa1!@#$_+-{}[]\\\"";
        Check(await hub.Validate(model), "Strong password symbols remain accepted with matching confirmation");
        foreach (string password in new[] { "ABC123!", "abc123!", "Validabc!", "Valid123", "Aa1!" })
        {
            model.Password = model.PasswordConfirmation = password;
            await hub.Validate(model);
            Check(hub.Errors.Count() == 1 && hub.Errors.Single().PropertyName == nameof(model.Password),
                "One omitted password requirement produces exactly one error: " + password);
        }
        model.Password = "Valid123!";
        model.PasswordConfirmation = "";
        await hub.Validate(model);
        Check(hub.Errors.Count() == 1 && hub.Errors.Single().Message == "Debe confirmar la contraseña.",
            "Empty password confirmation is required without an additional mismatch error");
        model.PasswordConfirmation = "Other123!";
        await hub.Validate(model);
        Check(hub.Errors.Count() == 1 && hub.Errors.Single().Message == "Las contraseñas no coinciden.",
            "Password mismatch validation remains active");
    }
}
