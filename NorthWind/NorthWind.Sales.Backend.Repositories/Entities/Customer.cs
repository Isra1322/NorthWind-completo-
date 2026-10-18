using System.ComponentModel.DataAnnotations;

namespace NorthWind.Sales.Backend.Repositories.Entities;

public class Customer
{
    public string Id { get; set; } = null!;
    public decimal CurrentBalance { get; set; }
    [Required(ErrorMessage = "El nombre del cliente es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre admite hasta 100 caracteres.")]
    public string Name { get; set; } = string.Empty;
    [StringLength(250, ErrorMessage = "La dirección admite hasta 250 caracteres.")]
    public string? Address { get; set; }
    [StringLength(15, ErrorMessage = "La ciudad admite hasta 15 caracteres.")]
    public string? City { get; set; }
    [StringLength(15, ErrorMessage = "El país admite hasta 15 caracteres.")]
    public string? Country { get; set; }
    [StringLength(10, ErrorMessage = "El código postal admite hasta 10 caracteres.")]
    public string? PostalCode { get; set; }
    [StringLength(254, ErrorMessage = "El correo admite hasta 254 caracteres."), EmailAddress(ErrorMessage = "El correo del cliente no es válido.")]
    [RegularExpression(@"[^\s@]+@[^\s@]+\.[^\s@]+", ErrorMessage = "El correo debe incluir un dominio válido.")]
    public string? Email { get; set; }
    // General identification: no country or document type is assumed.
    [StringLength(30, ErrorMessage = "La identificación admite hasta 30 caracteres."), RegularExpression(@"[A-Za-z0-9][A-Za-z0-9\- ]{2,29}", ErrorMessage = "La identificación debe tener entre 3 y 30 caracteres: letras, números, espacios o guiones.")]
    public string? Identification { get; set; }
    [StringLength(25, ErrorMessage = "El teléfono admite hasta 25 caracteres."), RegularExpression(@"(?=(?:[^0-9]*[0-9]){6,15}[^0-9]*$)\+?[0-9][0-9 ()\-]{5,24}", ErrorMessage = "El teléfono debe contener entre 6 y 15 dígitos; puede incluir prefijo +, espacios, guiones y paréntesis.")]
    public string? Phone { get; set; }
}
