namespace NorthWind.Sales.Backend.Repositories.Entities;

public class DomainLog
{
    public int Id { get; set; }
    public DateTime CreatedDate { get; set; }
    public string Information { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}
