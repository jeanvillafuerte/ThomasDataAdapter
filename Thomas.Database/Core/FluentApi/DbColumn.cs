﻿using System.Reflection;

namespace Thomas.Database.Core.FluentApi
{
    public class DbColumn
    {
        public bool Autogenerated { get; set; }
        public string Name { get; set; }
        public string DbName { get; set; }
        public string FullDbName
        {
            get
            {
                if (string.IsNullOrEmpty(DbName))
                    return Name;

                return $"{DbName} AS {Name}";
            }
        }
        public PropertyInfo Property { get; set; }

        //cast to specific type in database 
        public string DbType { get; set; }
        public bool RequireConversion { get; set; }

        //TODO: remove them

        //to specify the type of the column in the database that will read or write a large object
        //public bool IsLargeObject { get; set; }
        //public bool Required { get; set; }
        //public int MaxLength { get; set; }
        //public int MinLength { get; set; }
    }
}
