namespace NorthWind.Sales.Backend.DataContexts.EFCore.Options;

public class DBOptions
{
    public const string SectionKey = nameof(DBOptions);
    public string ConnectionString { get; set; } = null!;
    public string DomainLogsConnectionString { get; set; } = string.Empty;
}