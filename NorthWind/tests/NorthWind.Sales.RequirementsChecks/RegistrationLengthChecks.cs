using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NorthWind.Membership.Backend.AspNetIdentity;
using NorthWind.Membership.Entities.Dtos.UserRegistration;
using NorthWind.Membership.Frontend.RazorViews.ViewModels.UserRegistration;
using NorthWind.Validation.Entities.ValueObjects;

internal static class RegistrationLengthChecks
{
    public static async Task Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }

        var frontendServices = new ServiceCollection().AddLogging();
        frontendServices.AddValidationService().AddValidators()
            .AddMembershipServices(client => client.BaseAddress = new Uri("http://localhost/"))
            .AddMembershipValidators();
        using var frontend = frontendServices.BuildServiceProvider();
        using var frontendScope = frontend.CreateScope();
        var model = frontendScope.ServiceProvider.GetRequiredService<UserRegistrationViewModel>();
        model.FirstName = new string('A', 25);
        model.LastName = new string('B', 25);
        model.Email = new string('a', 38) + "@example.com";
        model.Password = model.PasswordConfirmation = "Valid123!";
        Check(model.Email.Length == 50 && await model.Validator.Validate(model),
            "Frontend accepts names of 25 characters and a valid email of 50");
        foreach (var field in new[] { "FirstName", "LastName", "Email" })
        {
            var property = model.GetType().GetProperty(field)!;
            string original = (string)property.GetValue(model)!;
            property.SetValue(model, "A" + original);
            Check(!await model.Validator.Validate(model) &&
                model.Validator.Errors.Count() == 1 && model.Validator.Errors.Single().PropertyName == field,
                "Frontend rejects one character above the limit without duplicate errors: " + field);
            Check((await model.Validator.ValidateField(model, field)).Count() == 1,
                "Progressive validation preserves the length error: " + field);
            property.SetValue(model, original);
        }
        model.FirstName = model.LastName = "   ";
        Check(!await model.Validator.Validate(model) && model.Validator.Errors.Count() == 2,
            "Whitespace-only names are rejected once per field");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "sales-appsettings.json")).Build();
        var backendServices = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration);
        backendServices.AddNorthWindSalesServices(_ => { }, _ => { },
            options => options.ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=NorthWindUsersDB;Integrated Security=true;TrustServerCertificate=true",
            options => { options.SecurityKey = new string('x', 96); options.ValidIssuer = "NorthWindSales"; options.ValidAudience = "NorthWindSalesClient"; });
        using var backend = backendServices.BuildServiceProvider();
        using var scope = backend.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MembershipContext>();
        int count = await db.Users.CountAsync();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            string email = Guid.NewGuid().ToString("N") + "length@example.com";
            Check(email.Length == 50, "Integration email is exactly 50 characters");
            async Task<IResult> Register(UserRegistrationDto dto)
            {
                const string prefix = "NorthWind.Membership.Backend.Core.Interfaces.UserRegistration.";
                var inputType = Type.GetType(prefix + "IUserRegistrationInputPort, NorthWind.Membership.Backend.Core", true)!;
                var outputType = Type.GetType(prefix + "IUserRegistrationOutputPort, NorthWind.Membership.Backend.Core", true)!;
                await (Task)inputType.GetMethod("Handle")!.Invoke(scope.ServiceProvider.GetRequiredService(inputType), [dto])!;
                return (IResult)outputType.GetProperty("Result")!.GetValue(scope.ServiceProvider.GetRequiredService(outputType))!;
            }
            var valid = new UserRegistrationDto(email, "Valid123!", new string('A', 25), new string('B', 25));
            foreach (string name in new[] { "12345", "----", "''''", "++++", "Juan--Pérez", "Juan..Pérez", "Juan  Pérez", "Juan'" })
            {
                var response = await Register(new(email, valid.Password, name, name));
                Check(response is IStatusCodeHttpResult { StatusCode: 400 } &&
                    response is IValueHttpResult { Value: ProblemDetails problem } &&
                    problem.Extensions["errors"] is IEnumerable<ValidationError> nameErrors &&
                    nameErrors.Count() == 2 && nameErrors.Select(e => e.PropertyName).Distinct().Count() == 2,
                    "Direct registration endpoint rejects invalid first and last names once per field: " + name);
            }
            foreach (string invalidEmail in new[] { "", "oscar", "oscar@", "@gmail.com", "oscar@gmail", "oscar@@gmail.com", "oscar gmail@gmail.com", "oscar@gmail..com", "}}+{++}+'+" })
            {
                var response = await Register(new(invalidEmail, valid.Password, valid.FirstName, valid.LastName));
                Check(response is IStatusCodeHttpResult { StatusCode: 400 } &&
                    response is IValueHttpResult { Value: ProblemDetails problem } &&
                    problem.Extensions["errors"] is IEnumerable<ValidationError> emailErrors &&
                    emailErrors.Count() == 1 && emailErrors.Single().Message ==
                        (invalidEmail.Length == 0 ? "Ingrese su correo electrónico." : "Ingrese un correo electrónico válido."),
                    "Backend rejects invalid email once in Spanish: " + invalidEmail);
            }
            foreach (var (field, dto) in new[]
            {
                ("FirstName", new UserRegistrationDto(email, valid.Password, new string('A', 26), valid.LastName)),
                ("LastName", new UserRegistrationDto(email, valid.Password, valid.FirstName, new string('B', 26))),
                ("Email", new UserRegistrationDto("a" + email, valid.Password, valid.FirstName, valid.LastName))
            })
            {
                var result = await Register(dto);
                Check(result is IStatusCodeHttpResult { StatusCode: 400 } &&
                    result is IValueHttpResult { Value: ProblemDetails details } &&
                    details.Extensions["errors"] is IEnumerable<ValidationError> errors &&
                    errors.Count() == 1 && errors.Single().PropertyName == field,
                    "Backend rejects excessive length with a controlled validation response: " + field);
            }
            Check(await db.Users.CountAsync() == count, "Invalid registrations insert no users");
            Check(await Register(valid) is IStatusCodeHttpResult { StatusCode: 200 },
                "Registration succeeds with all three fields at their exact limits");
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<MembershipUser>>();
            var user = await manager.FindByEmailAsync(email.ToUpperInvariant());
            Check(user is not null && user.FirstName.Length == 25 && user.LastName.Length == 25 &&
                user.Email == email && user.NormalizedEmail == manager.NormalizeEmail(email) &&
                user.NormalizedEmail!.Length == 50, "SQL persists exact limits and normalized email lookup works");
            Check(await manager.FindByNameAsync(email) is not null &&
                await manager.CheckPasswordAsync(user!, valid.Password), "Identity login works with Email as UserName");
            var duplicate = await Register(valid);
            Check(duplicate is IStatusCodeHttpResult { StatusCode: 400 } &&
                duplicate is IValueHttpResult { Value: ProblemDetails duplicateProblem } &&
                duplicateProblem.Extensions["errors"] is IEnumerable<ValidationError> duplicateErrors &&
                duplicateErrors.Count() == 1 && duplicateErrors.Single().Message == "Este correo electrónico ya está registrado.",
                "Duplicate email is rejected with exactly one Spanish message");
            var alias = new UserRegistrationDto("ventas+" + Guid.NewGuid().ToString("N")[..12] + "@empresa.com", valid.Password, "Ana", "Pérez");
            Check(await Register(alias) is IStatusCodeHttpResult { StatusCode: 200 }, "Backend registration accepts plus aliases");
            var bypass = await manager.CreateAsync(new MembershipUser
            {
                UserName = "guard@example.com", Email = "guard@example.com",
                FirstName = new string('A', 26), LastName = "Valid"
            }, valid.Password);
            Check(!bypass.Succeeded && bypass.Errors.Any(e => e.Code == "FirstName"),
                "Direct UserManager callers cannot bypass length validation");
            await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Check(await db.Users.CountAsync() == count, "All test users rolled back; existing users preserved");
    }
}
