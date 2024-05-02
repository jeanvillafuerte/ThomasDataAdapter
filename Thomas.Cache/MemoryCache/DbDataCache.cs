﻿using System.Collections.Concurrent;

namespace Thomas.Cache.MemoryCache
{
    internal sealed class DbDataCache : IDbDataCache
    {
        private static DbDataCache instance;
        public static DbDataCache Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DbDataCache();
                }
                return instance;
            }
        }

        private DbDataCache() { }

        private static ConcurrentDictionary<ulong, IQueryResult> CacheObject { get; set; } = new();

        public void AddOrUpdate(ulong key, IQueryResult data) => CacheObject.AddOrUpdate(key, data, (k, v) => data);

        public static bool TryGetValue(ulong key, out IQueryResult? data) => CacheObject.TryGetValue(key, out data);

        public bool TryGet<T>(ulong key, out QueryResult<T>? result)
        {
            if (CacheObject.TryGetValue(key, out IQueryResult? data))
            {
                if (data is QueryResult<T> convertedValue)
                {
                    result = convertedValue;
                    return true;
                }
            }

            result = null;
            return false;
        }

        public void Clear(ulong hash) => CacheObject.TryRemove(hash, out var _);

        public void Clear() => CacheObject.Clear();
    }

}

