using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TData.Cache")]
namespace TData.InternalCache
{
    internal sealed class DynamicQueryInfoCache
    {
        private static ConcurrentDictionary<int, ExpressionQueryItem> DynamicQueryStringDictionary = new ConcurrentDictionary<int, ExpressionQueryItem>();

        private DynamicQueryInfoCache() { }

        internal static void Set(in int key, ExpressionQueryItem value) => DynamicQueryStringDictionary.TryAdd(key, value);
        internal static bool TryGet(in int key, out ExpressionQueryItem meta) => DynamicQueryStringDictionary.TryGetValue(key, out meta);
        public static void Clear()
        {
            DynamicQueryStringDictionary.Clear();
        }
    }

    internal sealed class ExpressionQueryItem
    {
        public string Query { get; set; }
        public bool IsStaticQuery { get; set; }

        /// <summary>
        /// Hold parameter values on static queries
        /// </summary>
        public object[] ParameterValues { get; set; }

        public ExpressionQueryItem(in string query, in bool isStaticQuery, in object[] parameterValues)
        {
            Query = query;
            IsStaticQuery = isStaticQuery;
            ParameterValues = parameterValues;
        }
    }
}
