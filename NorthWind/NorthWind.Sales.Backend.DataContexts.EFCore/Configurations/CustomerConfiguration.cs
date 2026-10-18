namespace NorthWind.Sales.Backend.DataContexts.EFCore.Configurations;

internal class CustomerConfiguration : IEntityTypeConfiguration<Repositories.Entities.Customer>
{
    public void Configure(EntityTypeBuilder<Repositories.Entities.Customer> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).IsRequired().HasMaxLength(5).IsFixedLength();
        builder.Property(c => c.CurrentBalance).HasPrecision(12, 2);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Address).HasMaxLength(250);
        builder.Property(c => c.City).HasMaxLength(15);
        builder.Property(c => c.Country).HasMaxLength(15);
        builder.Property(c => c.PostalCode).HasMaxLength(10);
        builder.Property(c => c.Email).HasMaxLength(254);
        builder.Property(c => c.Identification).HasMaxLength(30);
        builder.Property(c => c.Phone).HasMaxLength(25);
        builder.ToTable("Customers", table => table.HasCheckConstraint("CK_Customers_Name_NotBlank", "LEN(LTRIM(RTRIM(REPLACE(REPLACE(REPLACE([Name], CHAR(9), ''), CHAR(10), ''), CHAR(13), '')))) > 0"));
        builder.ToTable("Customers", table => table.HasCheckConstraint("CK_Customers_Email_Format", "[Email] IS NULL OR ([Email] LIKE N'%_@_%._%' AND [Email] NOT LIKE N'%[ ' + CHAR(9) + CHAR(10) + CHAR(13) + N']%' AND LEN([Email]) - LEN(REPLACE([Email], N'@', N'')) = 1)"));
        builder.ToTable("Customers", table => table.HasCheckConstraint("CK_Customers_Identification_Format", "[Identification] IS NULL OR (LEN([Identification]) BETWEEN 3 AND 30 AND [Identification] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Za-z0-9 -]%' AND LEFT([Identification], 1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Za-z0-9]')"));
        builder.ToTable("Customers", table => table.HasCheckConstraint("CK_Customers_Phone_Format", "[Phone] IS NULL OR ([Phone] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^0-9 ()+-]%' AND LEFT([Phone], 1) LIKE N'[+0-9]' AND CHARINDEX(N'+', [Phone]) IN (0, 1) AND LEN([Phone]) - LEN(REPLACE([Phone], N'+', N'')) <= 1 AND LEN(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE([Phone], N'+', N''), N' ', N''), N'(', N''), N')', N''), N'-', N'')) BETWEEN 6 AND 15)"));
        builder.HasMany<Order>().WithOne().HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasData(
            new Repositories.Entities.Customer { Id = "ALFKI", Name = "Comercial Alfa", CurrentBalance = 0 },
            new Repositories.Entities.Customer { Id = "ANATR", Name = "Distribuidora Anatra", CurrentBalance = 0 },
            new Repositories.Entities.Customer { Id = "BONAP", Name = "Comercial Bonaparte", CurrentBalance = 0 });
    }
}
