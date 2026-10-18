namespace NorthWind.Sales.Backend.Repositories.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public short UnitsInStock { get; set; }
    public decimal UnitPrice { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
