﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Thomas.Cache;
using Thomas.Cache.Factory;
using Thomas.Database;
using Thomas.Database.Configuration;
using Thomas.Tests.Performance.Legacy.Setup;

namespace Thomas.Tests.Performance.Legacy
{
    class Program
    {
        public static string TableName { get; set; }
        public static bool CleanData { get; set; }
        public static IDatabase Database2 { get; set; }
        public static ICachedDatabase CachedResultDatabase { get; set; }

        static void Main(string[] args)
        {
            WriteStep("Starting setup...");
            Setup(out var rows);
            WriteStep("Completed Setup...", true);

            var timer = new System.Diagnostics.Stopwatch();
            timer.Start();

            WriteStep("Starting tests database1...");
            RunTestsDatabase("db1", "db1", rows);
            WriteStep("Completed tests database1...", true);

            WriteStep("Starting tests database2...");
            RunTestsDatabase("db2", "db2", rows);
            WriteStep("Completed tests database2...", true);

            WriteStep("Starting tests database2 (result cached)...");
            RunTestsCachedDatabase("db2", "db2 (cached)", rows);
            WriteStep("Completed tests database2 (result cached)...", true);

            WriteStep("Starting tests database2 (async)...");
            RunTestsDatabaseAsync("db2", "db2 (async)", rows);
            WriteStep("Completed tests database2 (async)...", true);

            WriteStep("Dropping tables...");
            DropTables();
            WriteStep("Dropped tables.");

            timer.Stop();
            WriteStep($"Total time: {timer.Elapsed.TotalSeconds} seconds.", true);
        }

        static void Setup(out int rowsGenerated)
        {
            var builder = new ConfigurationBuilder();

            builder.AddInMemoryCollection().AddJsonFile("dbsettings.json", true);

            var configuration = builder.Build();
            var db1 = "db1";
            var db2 = "db2";
            var cnx1 = configuration["connection1"];
            var cnx2 = configuration["connection2"];
            var len = configuration["rows"];
            TableName = $"Person_{DateTime.Now:yyyyMMddhhmmss}";
            CleanData = bool.Parse(configuration["cleanData"]);

            DbConfigurationFactory.Register(new DbSettings(db1, SqlProvider.SqlServer, cnx1));
            DbConfigurationFactory.Register(new DbSettings(db2, SqlProvider.SqlServer, cnx2));

            rowsGenerated = int.Parse(len);
            DataBaseManager.LoadDatabases(rowsGenerated, TableName);            
            Database2 = DbFactory.GetDbContext(db2);
            CachedResultDatabase = CachedDbFactory.GetDbContext(db2);
        }

        static void RunTestsDatabase(string db, string databaseName, int rows)
        {
            Task.WaitAll(
                Task.Run(() => new Tests.Expression(databaseName).Execute(db, TableName, rows)),
                Task.Run(() => new Tests.Single(databaseName).Execute(db, TableName, rows)),
                Task.Run(() => new Tests.List(databaseName).Execute(db, TableName, rows)),
                Task.Run(() => new Tests.Tuple(databaseName).Execute(db, TableName, rows)),
                Task.Run(() => new Tests.Procedures(databaseName).Execute(db, TableName, rows)),
                Task.Run(() => new Tests.Error(databaseName).Execute(db, TableName, rows))
                );
        }

        static void RunTestsCachedDatabase(string db, string databaseName, int rows)
        {
            Task.WaitAll(
                Task.Run(() => new Tests.Expression(databaseName).ExecuteCachedDatabase(db, TableName, rows)),
                Task.Run(() => new Tests.Single(databaseName).ExecuteCachedDatabase(db, TableName, rows)),
                Task.Run(() => new Tests.List(databaseName).ExecuteCachedDatabase(db, TableName, rows)),
                Task.Run(() => new Tests.Tuple(databaseName).ExecuteCachedDatabase(db, TableName, rows)),
                Task.Run(() => new Tests.Procedures(databaseName).ExecuteCachedDatabase(db, TableName, rows))
                //Task.Run(() => new Tests.Error(databaseName).ExecuteCachedDatabase(db, TableName, rows))
                );

            CachedDbFactory.GetDbContext(db).Clear();
        }

        static void RunTestsDatabaseAsync(string db, string databaseName, int rows)
        {
            Task.WaitAll(
                 Task.Run(() => new Tests.Expression(databaseName).ExecuteAsync(db, TableName, rows)),
                 Task.Run(() => new Tests.Single(databaseName).ExecuteAsync(db, TableName, rows)),
                 Task.Run(() => new Tests.List(databaseName).ExecuteAsync(db, TableName, rows)),
                 Task.Run(() => new Tests.Tuple(databaseName).ExecuteAsync(db, TableName, rows)),
                 Task.Run(() => new Tests.Procedures(databaseName).ExecuteAsync(db, TableName, rows)),
                 Task.Run(() => new Tests.Error(databaseName).ExecuteAsync(db, TableName, rows))
                 );
        }

        static void DropTables()
        {
            DbFactory.GetDbContext("db1").Execute($"DROP TABLE {TableName}", null, noCacheMetadata: true);
            DbFactory.GetDbContext("db2").Execute($"DROP TABLE {TableName}", null, noCacheMetadata: true);
            CachedResultDatabase.Clear();
            DbBase.Clear();
        }

        static void WriteStep(string message, bool includeBlankLine = false)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            if (includeBlankLine)
                Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
