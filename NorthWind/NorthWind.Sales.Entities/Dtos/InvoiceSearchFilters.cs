using System.Globalization;

namespace NorthWind.Sales.Entities.Dtos;

public record InvoiceSearchFilters(string CustomerId = "", string InvoiceNumber = "", string IssueDate = "", string Identification = "")
{
    public InvoiceSearchFilters Normalize() => new(CustomerId.Trim().ToUpperInvariant(), InvoiceNumber.Trim(), IssueDate.Trim(), Identification.Trim());
}

public static class SearchInputRules
{
    public static bool IsCodeCharacter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
    public static string CustomerCode(string value) => new(value.Where(IsCodeCharacter).Take(5).ToArray());
    public static string CustomerCedula(string value) => new(value.Where(c => c is >= '0' and <= '9').Take(10).ToArray());
    public static string Name(string value) => new(TextInputRules.CleanProductName(value).Take(50).ToArray());
    public static string NumericCode(string value, string previous) =>
        CustomerCedula(value);
    public static bool PositiveId(string value) => value.Length is > 0 and <= 10 &&
        value.All(c => c is >= '0' and <= '9') && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int id) && id > 0;
    public static Dictionary<string, string> ValidateLookup(bool products, string? id, string? name)
    {
        var errors = new Dictionary<string, string>();
        int rawIdLength = id?.Length ?? 0;
        bool invalidNumericId = products && id is not null && id.Any(c => c is < '0' or > '9');
        int rawNameLength = name?.Length ?? 0;
        bool invalidName = rawNameLength > 0 && !(products ? TextInputRules.IsProductName(name!) : TextInputRules.IsName(name!));
        id = id?.Trim() ?? "";
        name = name?.Trim() ?? "";
        if (invalidNumericId || rawIdLength > (products ? 10 : 5) || (rawIdLength > 0 && id.Length == 0) || (id != "" && (products
            ? !id.All(c => c is >= '0' and <= '9')
            : id != id.Trim() || id.Length > 5 || !id.All(IsCodeCharacter))))
            errors["Id"] = products ? "Ingrese hasta 10 dígitos para buscar un código de producto."
                : "El código del cliente admite hasta 5 letras o números.";
        if (rawNameLength > 50 || name.Any(char.IsControl) || invalidName)
            errors["Name"] = products
                ? "Ingrese hasta 50 caracteres válidos; el nombre debe contener letras o números."
                : "Ingrese hasta 50 caracteres: letras, espacios, puntos, guiones o apóstrofes; incluya al menos una letra.";
        return errors;
    }
    public static Dictionary<string, string> ValidateCustomerCedulaLookup(string? cedula, string? name)
    {
        var errors = new Dictionary<string, string>();
        cedula ??= "";
        name ??= "";
        if (cedula.Length > 10 || cedula.Any(c => c is < '0' or > '9'))
            errors["Identification"] = "La cédula admite únicamente hasta 10 dígitos.";
        if (name.Length > 50 || (name.Length > 0 && !TextInputRules.IsName(name)))
            errors["Name"] = "Ingrese hasta 50 caracteres: letras, espacios, puntos, guiones o apóstrofes; incluya al menos una letra.";
        return errors;
    }
    public static bool IsValidEcuadorianCedula(string value)
    {
        if (value.Length != 10 || !value.All(c => c is >= '0' and <= '9')) return false;
        int province = int.Parse(value[..2], CultureInfo.InvariantCulture);
        int third = value[2] - '0';
        if (province is < 1 or > 24 || third > 5) return false;
        int sum = 0;
        for (int index = 0; index < 9; index++)
        {
            int digit = value[index] - '0';
            if (index % 2 == 0)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
        }
        int checkDigit = (10 - sum % 10) % 10;
        return checkDigit == value[9] - '0';
    }
    public static Dictionary<string, string> ValidateInvoices(InvoiceSearchFilters filters, DateOnly today)
    {
        var errors = new Dictionary<string, string>();
        string customer = filters.CustomerId.Trim();
        if (filters.CustomerId.Length > 5 || customer.Length > 5 || !customer.All(IsCodeCharacter))
            errors["CustomerId"] = "El ID del cliente admite hasta 5 letras o números.";
        if (filters.InvoiceNumber.Length > 10 || !filters.InvoiceNumber.All(c => c is >= '0' and <= '9'))
            errors["InvoiceNumber"] = "Ingrese hasta 10 dígitos para buscar una factura.";
        if (filters.Identification.Length > 10 || !filters.Identification.All(c => c is >= '0' and <= '9'))
            errors["Identification"] = "La cédula admite únicamente hasta 10 dígitos.";
        if (filters.IssueDate != "" && (!DateOnly.TryParseExact(filters.IssueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date) || date > today))
            errors["IssueDate"] = "Seleccione una fecha válida que no sea futura.";
        return errors;
    }
}
