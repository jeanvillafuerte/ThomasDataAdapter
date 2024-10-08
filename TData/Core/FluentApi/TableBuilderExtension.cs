﻿using System;
using System.Linq;
using System.Linq.Expressions;

namespace TData.Core.FluentApi
{
    public static class TableBuilderExtension
    {
        public static DbTable Schema(this DbTable table, string schema)
        {
            table.Schema = schema;
            return table;
        }

        internal static DbTable AddFieldsAsColumns(this DbTable table, Type type)
        {
            TableBuilder.AddColumns(table, table.Key?.Name, type);
            return table;
        }

        public static DbTable AddFieldsAsColumns<T>(this DbTable table)
        {
            TableBuilder.AddColumns(table, table.Key?.Name, typeof(T));
            return table;
        }

        public static DbTable Key(this DbTable table, string keyName)
        {
            table.Key = table.Columns.First(x => x.Name == keyName);
            return table;

        }
        public static DbTable DbName(this DbTable table, string dbName)
        {
            table.DbName = dbName;
            return table;
        }

        public static DbTable ExcludeColumn<T>(this DbTable table, Expression<Func<T, object>> expression)
        {
            var memberExpression = TableBuilder.EnsureSelectedMember(expression);
            var columnName = memberExpression.Member.Name;
            var node = table.Columns.Find(new DbColumn { Name = columnName });
            table.Columns.Remove(node);
            return table;
        }

        public static DbColumn Column<T>(this DbTable table, Expression<Func<T, object>> expression)
        {
            var memberExpression = TableBuilder.EnsureSelectedMember(expression);
            return table.Columns.First(x => x.Name == memberExpression.Member.Name);
        }

        /// <summary>
        /// Original Db Name of the column in the database, used to map the column in SELECT queries or generated INSERT/UPDATE operations 
        /// </summary>
        /// <param name="dbName">Db Column Name</param>
        /// <returns></returns>
        public static DbColumn DbName(this DbColumn dbColumn, string dbName)
        {
            dbColumn.DbName = dbName;
            return dbColumn;
        }

        public static DbColumn DbType(this DbColumn dbColumn, string dbType)
        {
            dbColumn.DbType = dbType;
            return dbColumn;
        }

        public static DbColumn Scale(this DbColumn dbColumn, int scale)
        {
            dbColumn.Scale = scale;
            return dbColumn;
        }

        public static DbColumn Precision(this DbColumn dbColumn, int precision)
        {
            dbColumn.Precision = precision;
            return dbColumn;
        }

        public static DbColumn IsAutoGenerated(this DbColumn dbColumn, bool autoGenerated)
        {
            dbColumn.AutoGenerated = autoGenerated;
            return dbColumn;
        }

        public static DbColumn Required(this DbColumn dbColumn)
        {
            dbColumn.Required = true;
            return dbColumn;
        }

        public static DbColumn Size(this DbColumn dbColumn, int size)
        {
            dbColumn.Size = size;
            return dbColumn;
        }

    }
}
