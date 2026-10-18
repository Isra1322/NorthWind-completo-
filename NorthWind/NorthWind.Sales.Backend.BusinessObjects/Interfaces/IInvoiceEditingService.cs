using NorthWind.Sales.Entities.Dtos;

namespace NorthWind.Sales.Backend.BusinessObjects.Interfaces;

public interface IInvoiceEditingService
{
    Task<InvoiceVersionInfo> UpdateAsync(int invoiceId, UpdateInvoiceDto update, string userName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvoiceVersionInfo>> GetVersionsAsync(int invoiceId, CancellationToken cancellationToken = default);
    Task<InvoiceVersionDto?> GetVersionAsync(int invoiceId, int versionNumber, CancellationToken cancellationToken = default);
    Task<InvoiceVersionDto?> GetCurrentVersionAsync(int invoiceId, CancellationToken cancellationToken = default);
    Task<NorthWind.Sales.Entities.Dtos.LookupItem?> GetProductForEditAsync(int productId, CancellationToken cancellationToken = default);
}
