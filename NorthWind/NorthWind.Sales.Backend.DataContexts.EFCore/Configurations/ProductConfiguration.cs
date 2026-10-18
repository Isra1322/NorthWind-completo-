namespace NorthWind.Sales.Backend.DataContexts.EFCore.Configurations;

internal class ProductConfiguration : IEntityTypeConfiguration<Repositories.Entities.Product>
{
    public void Configure(EntityTypeBuilder<Repositories.Entities.Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(60);
        builder.Property(p => p.UnitPrice).HasPrecision(8, 2);
        builder.Property(p => p.RowVersion).IsRowVersion();
        builder.HasMany<Repositories.Entities.OrderDetail>().WithOne()
            .HasForeignKey(d => d.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasData(
            new Repositories.Entities.Product { Id = 1, Name = "Chai", UnitsInStock = 100 },
            new Repositories.Entities.Product { Id = 2, Name = "Chang", UnitsInStock = 0 },
            new Repositories.Entities.Product { Id = 3, Name = "Aniseed Syrup", UnitsInStock = 100 },
            new Repositories.Entities.Product { Id = 4, Name = "Chef Anton's Cajun Seasoning", UnitsInStock = 100 },
            new Repositories.Entities.Product { Id = 5, Name = "Chef Anton's Gumbo Mix", UnitsInStock = 5 },
            new Repositories.Entities.Product { Id = 10, Name = "Ikura", UnitsInStock = 10 });
    }
}
