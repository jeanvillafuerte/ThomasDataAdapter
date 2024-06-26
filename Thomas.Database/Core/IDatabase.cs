﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Thomas.Database.Core;
using Thomas.Database.Core.WriteDatabase;

namespace Thomas.Database
{
    public interface IDatabase : IWriteOnlyDatabase, IDbOperationResult, IDbOperationResultAsync, IDbResulSet, IDbResultSetAsync, IDbSetExpression
    {
        int Execute(in string script, in object? parameters = null, in bool noCacheMetadata = false);

        (Action, IEnumerable<List<T>>) FetchData<T>(string script, object? parameters = null, int batchSize = 1000);

        void ExecuteBlock(Action<IDatabase> func);
        Task ExecuteBlockAsync(Func<IDatabase, Task> func);
        Task ExecuteBlockAsync(Func<IDatabase, CancellationToken, Task> func, CancellationToken cancellationToken);

        bool ExecuteTransaction(Func<IDatabase, TransactionResult> func);
        T ExecuteTransaction<T>(Func<IDatabase, T> func);
        TransactionResult Commit();
        TransactionResult Rollback();

        Task<T> ExecuteTransactionAsync<T>(Func<IDatabase, CancellationToken, Task<T>> func, CancellationToken cancellationToken);
        Task<bool> ExecuteTransaction(Func<IDatabase, CancellationToken, Task<TransactionResult>> func, CancellationToken cancellationToken);
        Task<TransactionResult> CommitAsync();
        Task<TransactionResult> RollbackAsync();
    }
}
