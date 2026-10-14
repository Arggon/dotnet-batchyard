using Microsoft.Data.SqlClient;
using Payments.Application.Mediator;

namespace Payments.Data;

/// <summary>One connection per request scope; commands run in the transaction the TransactionBehavior opens.</summary>
public sealed class PaymentsDb(string connectionString) : IUnitOfWork, IAsyncDisposable
{
    private SqlConnection? _connection;

    public SqlTransaction? Transaction { get; private set; }

    public async Task<SqlConnection> ConnectionAsync(CancellationToken ct)
    {
        if (_connection is null)
        {
            _connection = new SqlConnection(connectionString);
            await _connection.OpenAsync(ct);
        }

        return _connection;
    }

    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        var connection = await ConnectionAsync(cancellationToken);
        Transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (Transaction is not null)
        {
            await Transaction.CommitAsync(cancellationToken);
            await Transaction.DisposeAsync();
            Transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (Transaction is not null)
        {
            await Transaction.RollbackAsync(cancellationToken);
            await Transaction.DisposeAsync();
            Transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Transaction is not null)
        {
            await Transaction.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
