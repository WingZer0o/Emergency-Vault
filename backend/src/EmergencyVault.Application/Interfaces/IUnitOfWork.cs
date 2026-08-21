namespace EmergencyVault.Application.Interfaces;

/// <summary>
/// Lets a service wrap several repository/service calls - which may run against the same
/// underlying EF Core context via DI - in a single database transaction.
/// </summary>
public interface IUnitOfWork
{
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
    Task RollbackAsync(CancellationToken ct);
}
