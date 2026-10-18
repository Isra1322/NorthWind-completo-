using System.Transactions;
using NorthWind.Transactions.Entities.Interfaces;

namespace NorthWind.Transactions.Entities.Services;

internal class DomainTransaction : IDomainTransaction, IDisposable
{
    private TransactionScope? transactionScope;

    public void BeginTransaction()
    {
        if (transactionScope is not null)
            throw new InvalidOperationException("Ya existe una transacción activa.");
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Las transacciones distribuidas requieren Windows.");

        TransactionManager.ImplicitDistributedTransactions = true;
        transactionScope = new TransactionScope(
            TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);
    }

    public void CommitTransaction()
    {
        if (transactionScope is null)
            throw new InvalidOperationException("No existe una transacción activa.");

        transactionScope.Complete();
        Dispose();
    }

    public void RollbackTransaction() => Dispose();

    public void Dispose()
    {
        var scope = transactionScope;
        transactionScope = null;
        scope?.Dispose();
    }
}
