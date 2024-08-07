﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Thomas.Database.Core
{
    public interface IDbOperationResultAsync
    {
        /// <summary>
        /// Execute a script and return the result as a DbOpAsyncResult
        /// </summary>
        /// <param name="script">Sql text</param>
        /// <param name="parameters">Object containing param values matching script tokens</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns></returns>
        Task<DbOpAsyncResult> ExecuteOpAsync(string script, object parameters, CancellationToken cancellationToken);
        Task<DbOpAsyncResult<T>> ToSingleOpAsync<T>(string script, object parameters, CancellationToken cancellationToken);
        Task<DbOpAsyncResult<List<T>>> ToListOpAsync<T>(string script, object parameters, CancellationToken cancellationToken);
        Task<DbOpAsyncResult<Tuple<List<T1>, List<T2>>>> ToTupleOpAsync<T1, T2>(string script, object parameters, CancellationToken cancellationToken);
        Task<DbOpAsyncResult<Tuple<List<T1>, List<T2>, List<T3>>>> ToTupleOp<T1, T2, T3>(string script, object parameters, CancellationToken cancellationToken);
        Task<DbOpAsyncResult<Tuple<List<T1>, List<T2>, List<T3>, List<T4>>>> ToTupleOp<T1, T2, T3, T4>(string script, object parameters, CancellationToken cancellationToken);
        Task<DbOpAsyncResult<Tuple<List<T1>, List<T2>, List<T3>, List<T4>, List<T5>>>> ToTupleOp<T1, T2, T3, T4, T5>(string script, object parameters, CancellationToken cancellationToken);
        Task<DbOpAsyncResult<Tuple<List<T1>, List<T2>, List<T3>, List<T4>, List<T5>, List<T6>>>> ToTupleOp<T1, T2, T3, T4, T5, T6>(string script, object parameters, CancellationToken cancellationToken);
        Task<DbOpAsyncResult<Tuple<List<T1>, List<T2>, List<T3>, List<T4>, List<T5>, List<T6>, List<T7>>>> ToTupleOp<T1, T2, T3, T4, T5, T6, T7>(string script, object parameters, CancellationToken cancellationToken);
    }
}
