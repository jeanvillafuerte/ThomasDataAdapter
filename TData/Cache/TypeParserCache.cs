using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using static TData.DatabaseCommand;

namespace TData.InternalCache
{
    internal abstract class CacheTypeHash
    {
        internal static HashSet<Type> CachedTypes = new HashSet<Type>();
    }

    internal sealed class TypeParserCache<T> : CacheTypeHash
    {
        internal static ConcurrentDictionary<int, ParserDelegate<T>> TypeParserDictionary = new ConcurrentDictionary<int, ParserDelegate<T>>();
        
        private TypeParserCache() { }

        internal static void Set(in int key, in ParserDelegate<T> value)
        {
            TypeParserDictionary.TryAdd(key, value);

            lock (CachedTypes)
            {
              CachedTypes.Add(typeof(T));
            }
        }

        internal static bool TryGet(in int key, out ParserDelegate<T> properties) => TypeParserDictionary.TryGetValue(key, out properties);
        internal static void Clear()
        {
            TypeParserDictionary.Clear();
        }
    }
}
