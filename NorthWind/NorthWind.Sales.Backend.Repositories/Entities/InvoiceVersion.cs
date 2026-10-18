namespace NorthWind.Sales.Backend.Repositories.Entities;

public sealed class InvoiceVersion
{
    public long Id { get; set; }
    public int InvoiceId { get; set; }
    public int VersionNumber { get; set; }
    public DateTime ModifiedAt { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public byte[] PdfContent { get; set; } = [];
}
