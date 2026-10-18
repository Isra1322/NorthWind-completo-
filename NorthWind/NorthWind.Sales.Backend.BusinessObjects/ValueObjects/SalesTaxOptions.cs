namespace NorthWind.Sales.Backend.BusinessObjects.ValueObjects;

public class SalesTaxOptions
{
    public bool Enabled { get; set; }
    public decimal? Rate { get; set; }
    public decimal EffectiveRate => !Enabled ? 0 : Rate is > 0 and <= 1 && decimal.Round(Rate.Value, 6) == Rate.Value
        ? Rate.Value : throw new InvalidOperationException("Configure SalesTax:Rate mayor que 0 y hasta 1, con hasta seis decimales, antes de aplicar IVA.");
}
