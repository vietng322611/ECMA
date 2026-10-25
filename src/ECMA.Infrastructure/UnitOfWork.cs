using ECMA.Domain.Exceptions;
using ECMA.Domain.Interfaces;
using ECMA.Infrastructure.Data;
using ECMA.Infrastructure.Repositories;

namespace ECMA.Infrastructure;

public sealed class UnitOfWork(EcmaDbContext context) : IUnitOfWork
{
    public IUserRepository Users { get; } = new UserRepository(context);
    public IEventRepository Events { get; } = new EventRepository(context);
    public IRegistrationRepository Registrations { get; } = new RegistrationRepository(context);

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        new EfUnitOfWorkTransaction(await context.Database.BeginTransactionAsync(cancellationToken));

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException exception)
            when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_users_username" })
        {
            throw new DuplicateUsernameException(exception);
        }
    }

    public void Dispose() => context.Dispose();
}

internal sealed class EfUnitOfWorkTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) : IUnitOfWorkTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}