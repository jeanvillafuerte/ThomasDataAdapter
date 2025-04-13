using System.Collections.Concurrent;
using static TData.Core.Provider.DatabaseProvider;

namespace TData.Cache
{
    internal sealed class BulkOperationCache<T>
    {      
        internal static readonly ConcurrentDictionary<int, BulkOperationDelegate<T>> BulkOperationMetadataCache = new ConcurrentDictionary<int, BulkOperationDelegate<T>>();
        private BulkOperationCache() { }


        internal static void Set(in int key, in BulkOperationDelegate<T> value)
        {
            BulkOperationMetadataCache.TryAdd(key, value);
        }

        internal static bool TryGet(in int key, out BulkOperationDelegate<T> properties) => BulkOperationMetadataCache.TryGetValue(key, out properties);
        internal static void Clear()
        {
            BulkOperationMetadataCache.Clear();
        }
    }
}
