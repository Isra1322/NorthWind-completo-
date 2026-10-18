namespace NorthWind.Sales.Backend.DataContexts.EFCore.Configurations;

internal sealed class InvoiceVersionConfiguration : IEntityTypeConfiguration<Repositories.Entities.InvoiceVersion>
{
    public void Configure(EntityTypeBuilder<Repositories.Entities.InvoiceVersion> builder)
    {
        builder.ToTable("InvoiceVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ModifiedBy).HasMaxLength(256).IsRequired();
        builder.Property(x => x.SnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.PdfContent).HasColumnType("varbinary(max)").IsRequired();
        builder.HasIndex(x => new { x.InvoiceId, x.VersionNumber }).IsUnique();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}
