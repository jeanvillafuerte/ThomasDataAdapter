﻿using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using Thomas.Database;
using Thomas.Database.SqlServer;


namespace Thomas.Tests.Performance.Benchmark
{
    [BenchmarkCategory("ORM")]
    public class Setup
    {
        protected IThomasDb service;

        protected string TableName;

        protected bool CleanData;

        public void Start()
        {

            var builder = new ConfigurationBuilder();

            builder.AddInMemoryCollection().AddJsonFile("dbsettings.json", true);

            var configuration = builder.Build();

            var str = configuration["connection"];
            var len = configuration["rows"];

            CleanData = bool.Parse(configuration["cleanData"]);

            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddThomasSqlDatabase((options) => new ThomasDbStrategyOptions()
            {
                StringConnection = str
            });

            var serviceProvider = serviceCollection.BuildServiceProvider();

            service = serviceProvider.GetService<IThomasDb>();

            SetDataBase(service, int.Parse(len));
        }

        void SetDataBase(IThomasDb service, int length)
        {
            TableName = $"Person_{DateTime.Now.ToString("yyyyMMddhhmmss")}";

            string tableScriptDefinition = @$"IF (OBJECT_ID('{TableName}') IS NULL)
                                                BEGIN
																	
	                                                CREATE TABLE {TableName}
													(
		                                                Id			INT PRIMARY KEY IDENTITY(1,1),
		                                                UserName	VARCHAR(25),
		                                                FirstName	VARCHAR(500),
		                                                LastName	VARCHAR(500),
		                                                BirthDate	DATE,
		                                                Age			SMALLINT,
		                                                Occupation	VARCHAR(300),
		                                                Country		VARCHAR(240),
		                                                Salary		DECIMAL(20,2),
		                                                UniqueId	UNIQUEIDENTIFIER,
		                                                [State]		BIT,
		                                                LastUpdate	DATETIME
	                                                )

                                                END";

            var result = service.Execute(tableScriptDefinition, false);

            if (!result.Success)
            {
                throw new Exception(result.ErrorMessage);
            }

            string data = @$"SET NOCOUNT ON
							DECLARE @IDX INT = 0
							WHILE @IDX <= {length}
							BEGIN
								INSERT INTO {TableName} (UserName, FirstName, LastName, BirthDate, Age, Occupation, Country, Salary, UniqueId, [State], LastUpdate)
								VALUES ( REPLICATE('A',25), REPLICATE('A',500), REPLICATE('A',500), '1988-01-01', 33, REPLICATE('A',300), REPLICATE('A',240), ROUND(RAND() * 10000, 2), NEWID(), ROUND(RAND(), 0), DATEADD(DAY, ROUND(RAND() * -12, 0), GETDATE()))
								SET @IDX = @IDX + 1;
							END";

            var dataResult = service.Execute(data, false);

            if (!dataResult.Success)
            {
                throw new Exception(dataResult.ErrorMessage);
            }
        }

        protected void Clean()
        {
            if (CleanData)
            {
                service.Execute($"DROP TABLE {TableName}");
            }
        }
    }
}
