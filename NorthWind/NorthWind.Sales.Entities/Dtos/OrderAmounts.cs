namespace NorthWind.Sales.Entities.Dtos;

public record OrderAmounts(decimal Subtotal, decimal DiscountAmount, decimal TaxableBase, decimal TaxAmount, decimal Total)
{
    // New orders have no automatic discount. Explicit discounts remain supported for legacy data.
    public const decimal DefaultDiscountRate = 0m;
    public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static OrderAmounts Calculate(IEnumerable<decimal> lineAmounts, decimal taxRate,
        decimal discountRate = DefaultDiscountRate, decimal flatDiscount = 0)
    {
        if (taxRate is < 0 or > 1 || decimal.Round(taxRate, 6) != taxRate)
            throw new ArgumentOutOfRangeException(nameof(taxRate));
        if (discountRate is < 0 or > 1 || flatDiscount < 0)
            throw new ArgumentOutOfRangeException(nameof(discountRate));
        decimal subtotal = Round(lineAmounts.Sum(Round));
        decimal discount = Math.Min(subtotal, Round(subtotal * discountRate + flatDiscount));
        decimal taxableBase = subtotal - discount;
        decimal tax = Round(taxableBase * taxRate);
        return new(subtotal, discount, taxableBase, tax, taxableBase + tax);
    }
}
