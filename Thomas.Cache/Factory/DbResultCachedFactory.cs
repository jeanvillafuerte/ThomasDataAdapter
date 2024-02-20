﻿using Thomas.Cache.MemoryCache;
using Thomas.Database;
using Thomas.Database.Configuration;

namespace Thomas.Cache.Factory
{
    public static class CachedDbFactory
    {
        public static ICachedDatabase CreateContext(string signature)
        {
            var config = DbConfigurationFactory.Get(signature);
            var database = new DatabaseBase(config);
            return new CachedDatabase(DbDataCache.Instance, database, config.CultureInfo, config.SQLValues);
        }
    }
}
