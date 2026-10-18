namespace NorthWind.Sales.Backend.DataContexts.EFCore.DataContexts;

internal class NorthWindContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<Repositories.Entities.OrderDetail> OrderDetails { get; set; }
    public DbSet<Repositories.Entities.Customer> Customers { get; set; }
    public DbSet<Repositories.Entities.Product> Products { get; set; }
    public DbSet<Repositories.Entities.InvoiceVersion> InvoiceVersions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=NorthWindDB");

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
}
