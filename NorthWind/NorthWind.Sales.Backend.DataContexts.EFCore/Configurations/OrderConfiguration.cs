namespace NorthWind.Sales.Backend.DataContexts.EFCore.Configurations;

internal class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasIndex(o => o.RequestId).IsUnique().HasFilter("[RequestId] IS NOT NULL");
        builder.Property(o => o.CustomerId).IsRequired().HasMaxLength(5).IsFixedLength();
        builder.Property(o => o.ShipAddress).IsRequired().HasMaxLength(60);
        builder.Property(o => o.ShipCity).HasMaxLength(15);
        builder.Property(o => o.ShipCountry).HasMaxLength(15);
        builder.Property(o => o.ShipPostalCode).HasMaxLength(10).IsRequired(false);
        builder.Property(o => o.Subtotal).HasPrecision(18, 2);
        builder.Property(o => o.TaxRate).HasPrecision(7, 6);
        builder.Property(o => o.TaxAmount).HasPrecision(18, 2);
        builder.Property(o => o.Total).HasPrecision(18, 2);
        builder.Property(o => o.Discount).HasConversion<double>().HasColumnType("float");
    }
}
