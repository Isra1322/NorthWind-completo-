namespace NorthWind.Sales.Backend.DataContexts.EFCore.DataContexts;

internal class NorthWindSalesContext(IOptions<DBOptions> dbOptions) : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<Repositories.Entities.OrderDetail> OrderDetails { get; set; }
    public DbSet<Repositories.Entities.Customer> Customers { get; set; }
    public DbSet<Repositories.Entities.Product> Products { get; set; }
    public DbSet<Repositories.Entities.InvoiceVersion> InvoiceVersions { get; set; }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateCustomers();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateCustomers();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateCustomers()
    {
        foreach (var entry in ChangeTracker.Entries<Repositories.Entities.Customer>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified))
            System.ComponentModel.DataAnnotations.Validator.ValidateObject(entry.Entity,
                new System.ComponentModel.DataAnnotations.ValidationContext(entry.Entity), validateAllProperties: true);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSqlServer(dbOptions.Value.ConnectionString);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
}
