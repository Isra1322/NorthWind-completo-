using System.Transactions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NorthWind.Membership.Backend.Core.Interfaces.Common;
using NorthWind.Membership.Entities.Dtos.UserRegistration;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces;
using NorthWind.Sales.Entities.Dtos;
using NorthWind.Sales.Entities.Dtos.CreateOrder;
using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Exceptions.Entities.Exceptions;

internal static class FormSearchChecks
{
    public static async Task Run(ServiceProvider provider, string connectionString)
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var validator = services.GetRequiredService<IModelValidatorHub<CreateOrderDto>>();
        foreach (string address in new[] { "Calle Loja 101", "Av. 12 de Noviembre", "Av. Los Shyris N.º 25-10", "Calle Bolívar #123" })
            Check(TextInputRules.IsAddress(address), "Real address accepted: " + address);
        foreach (string value in new[] { "+", "   ", "@#$", "Calle<script>" })
            Check(!TextInputRules.IsAddress(value), "Invalid address rejected");
        foreach (string value in new[] { "Puebla+++", "529455644864", "---", "   " })
            Check(!TextInputRules.IsPlace(value), "Invalid city/country rejected");
        foreach (string value in new[] { "José", "María-José", "O’Connor", "J. Ñüñez" })
            Check(TextInputRules.IsName(value), "Accents and appropriate name punctuation accepted");
        foreach (string value in new[] { "Ana12", "Ana+", "Ana@", "   ", "---" })
            Check(!TextInputRules.IsName(value), "Invalid personal name rejected");
        await validator.Validate(new("ALFKI", "+", "Puebla+++", "529455644864", "01A", [new(1, 35, 1)]));
        foreach (string field in new[] { "ShipAddress", "ShipCity", "ShipCountry", "ShipPostalCode" })
            Check(validator.Errors.Any(e => e.PropertyName == field), "Direct API DTO validation rejects " + field);

        var read = services.GetRequiredService<ISalesReadInputPort>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        async Task<int> Count(string sql, params (string Name, object Value)[] parameters)
        {
            await using var command = new SqlCommand(sql, connection);
            foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        var all = await read.FilterInvoices(new(), 1);
        Check(all.TotalCount == await Count("SELECT COUNT(*) FROM Orders") && all.Items.Count <= 10,
            "Invoice list retains server pagination and real SQL count");
        Check(all.Items.SequenceEqual(all.Items.OrderByDescending(x => x.OrderDate).ThenByDescending(x => x.Id)),
            "Invoice ordering remains newest first");
        foreach (var filters in new[] { new InvoiceSearchFilters(Identification: "0910"),
            new InvoiceSearchFilters(Identification: "0910000017"), new InvoiceSearchFilters(InvoiceNumber: "7"),
            new InvoiceSearchFilters(InvoiceNumber: "0"), new InvoiceSearchFilters(Identification: "0000000000") })
        {
            var result = await read.FilterInvoices(filters, 1);
            int expected = await Count("""
                SELECT COUNT(*) FROM Orders o WHERE (@number='' OR CONVERT(varchar(20),o.Id) LIKE '%'+@number+'%')
                AND (@identification='' OR EXISTS(SELECT 1 FROM Customers c WHERE c.Id=o.CustomerId AND c.Identification LIKE '%'+@identification+'%'))
                """, ("@number", filters.InvoiceNumber), ("@identification", filters.Identification));
            Check(result.TotalCount == expected, "Partial invoice/identification filters match SQL Server exactly");
        }
        var sample = all.Items.First();
        string day = sample.OrderDate.ToString("yyyy-MM-dd");
        foreach (string identification in new[] { "", sample.CustomerIdentification is { Length: > 0 } value && value.All(char.IsDigit) ? value[..Math.Min(4, value.Length)] : "0910" })
        {
            var result = await read.FilterInvoices(new(InvoiceNumber: sample.Id.ToString()[..1], IssueDate: day, Identification: identification), 1);
            int expected = await Count("""
                SELECT COUNT(*) FROM Orders o WHERE CONVERT(varchar(20),o.Id) LIKE '%'+@number+'%'
                AND OrderDate>=@start AND OrderDate<DATEADD(day,1,@start)
                AND (@identification='' OR EXISTS(SELECT 1 FROM Customers c WHERE c.Id=o.CustomerId AND c.Identification LIKE '%'+@identification+'%'))
                """, ("@number", sample.Id.ToString()[..1]), ("@start", sample.OrderDate.Date), ("@identification", identification));
            Check(result.TotalCount == expected && result.Items.All(x => x.OrderDate.Date == sample.OrderDate.Date),
                "Combined date, identification and invoice filters include stored times within the selected day");
        }
        var products = await read.SearchProducts("1", null, 1);
        Check(products.Items.All(x => x.Id.Contains('1') && x.UnitsInStock > 0) && products.Items.Count <= 8,
            "Partial product codes preserve stock filtering and page size");
        var customers = await read.SearchCustomersByIdentification("091", null, 1);
        Check(customers.Items.All(x => x.Identification!.Contains("091")) && customers.Items.Count <= 5,
            "Customer lookup uses real identification and preserves page size");
        var namedProducts = await read.SearchProducts(null, "Chef Anton's", 1);
        Check(namedProducts.Items.Count > 0 && namedProducts.Items.All(x => x.Name.Contains("Chef Anton's") && x.UnitsInStock > 0),
            "SQL product search preserves partial apostrophe names and available stock");
        string customerTerm = new(customers.Items.First().Name.TakeWhile(TextInputRules.IsLetter).ToArray());
        Check(customerTerm.Length > 0, "Existing customer provides a real partial name");
        var namedCustomers = await read.SearchCustomersByIdentification(null, customerTerm, 1);
        Check(namedCustomers.Items.Count > 0, "SQL customer search finds a real partial name");
        bool rejected = false;
        try { await read.FilterInvoices(new(Identification: "091@"), 1); }
        catch (ValidationException) { rejected = true; }
        Check(rejected, "Backend rejects invalid filter characters");

        var membership = services.GetRequiredService<IMembershipService>();
        await using var usersConnection = new SqlConnection(connectionString.Replace("NorthWindDB", "NorthWindUsersDB"));
        await usersConnection.OpenAsync();
        async Task<int> UsersCount()
        {
            await using var command = new SqlCommand("SELECT COUNT(*) FROM AspNetUsers", usersConnection);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        int usersBefore = await UsersCount();
        using (var rollback = new TransactionScope(TransactionScopeOption.RequiresNew,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted }, TransactionScopeAsyncFlowOption.Enabled))
        {
            string email = "check-" + Guid.NewGuid().ToString("N")[..16] + "@example.invalid";
            var dto = new UserRegistrationDto(email, "Valid123!", "José", "Pérez");
            Check(!(await membership.Register(dto)).HasError, "New user registration still works within rollback-only transaction");
            var duplicate = await membership.Register(dto);
            Check(duplicate.HasError && duplicate.ErrorValue.Count() == 1 &&
                duplicate.ErrorValue.Single().Message == "Este correo electrónico ya está registrado.",
                "Duplicate email returns exactly one Spanish backend error");
            Check(await membership.GetUserByCredentials(new(email, "Valid123!")) is not null &&
                await membership.GetUserByCredentials(new(email, "Wrong123!")) is null,
                "Login still accepts correct credentials and rejects incorrect ones");
            var weak = await membership.Register(new("weak-" + Guid.NewGuid().ToString("N")[..16] + "@example.invalid", "abc", "José", "Pérez"));
            Check(weak.HasError && weak.ErrorValue.Count() >= 4 && weak.ErrorValue.All(e => e.Message.StartsWith("La contraseña")),
                "Existing password policies remain enabled with specific Spanish errors");
        }
        Check(await UsersCount() == usersBefore, "Test users rolled back; existing accounts unchanged");
        await LiveSearchChecks.Run();
    }
}
