﻿using Thomas.Database.Configuration;
using Thomas.Database.Core.FluentApi;
using Thomas.Database.Core.QueryGenerator;

namespace Thomas.Database.Tests.SQLite
{
    public class SQLiteTests : IDatabaseProvider
    {
        public string ConnectionString => "Data Source=.\\database_test.db";

        //global setup
        [OneTimeSetUp]
        public void Initialize()
        {
            DbConfig.Clear();
            var tableBuilder = new TableBuilder();
            var table = tableBuilder.AddTable<User>(x => x.Id, keyAutoGenerated: true).AddFieldsAsColumns<User>().DbName("USERS");
            table.Column<User>(x => x.UserTypeId).DbName("USER_TYPE_ID");
            var table2 = tableBuilder.AddTable<UserNullableRecord>(x => x.Id, keyAutoGenerated: true).AddFieldsAsColumns<UserNullableRecord>().DbName("USERS");
            table2.Column<UserNullableRecord>(x => x.UserTypeId).DbName("USER_TYPE_ID");
            var table3 = tableBuilder.AddTable<UserNullableClass>(x => x.Id, keyAutoGenerated: true).AddFieldsAsColumns<UserNullableClass>().DbName("USERS");
            table3.Column<UserNullableClass>(x => x.UserTypeId).DbName("USER_TYPE_ID");
            tableBuilder.AddTable<UserType>(x => x.Id, keyAutoGenerated: false).AddFieldsAsColumns<UserType>().DbName("USER_TYPE");
            DbHub.AddDbBuilder(tableBuilder);
            DbConfig.Register(new DbSettings("db1", SqlProvider.Sqlite, ConnectionString));
        }

        [Test, Order(1)]
        public void SQLiteStoreProcedureNotSupportedException()
        {
            var dbContext = DbHub.Use("db1");
            Assert.Throws<SqLiteStoreProcedureNotSupportedException>(() => dbContext.Execute("EXEC_LIST"), "SQLite does not support stored procedures.");
        }

        [Test, Order(2)]
        public void DropIfExistsTable()
        {
            var dbContext = DbHub.Use("db1", buffered: false);
            dbContext.ExecuteBlock((db) =>
            {
                db.Execute("DROP TABLE IF EXISTS USERS");
                db.Execute("DROP TABLE IF EXISTS USER_TYPE");
            });
            
            Assert.Pass();
        }

        [Test, Order(3)]
        public void CreateTable()
        {
            var dbContext = DbHub.Use("db1", buffered: false);
            dbContext.ExecuteBlock((db) =>
            {
                db.Execute("CREATE TABLE USERS(ID INTEGER PRIMARY KEY, USER_TYPE_ID INTEGER NOT NULL, NAME TEXT, STATE INTEGER, SALARY REAL, BIRTHDAY TEXT, USERCODE TEXT, ICON BLOB)");
                db.Execute("CREATE TABLE USER_TYPE(ID INTEGER NOT NULL, NAME TEXT)");
            });
            Assert.Pass();
        }

        [Test, Order(4)]
        public void TruncateTable()
        {
            var dbContext = DbHub.Use("db1");
            dbContext.Truncate<User>();
            dbContext.Truncate<UserType>();
        }

        [Test, Order(4)]
        public void TruncateTableByString()
        {
            var dbContext = DbHub.Use("db1");
            dbContext.Truncate("USERS");
            dbContext.Truncate("USER_TYPE");
        }

        [Test, Order(5)]
        public void InsertUserType()
        {
            var dbContext = DbHub.Use("db1");
            dbContext.ExecuteBlock((db) =>
            {
                db.Insert(new UserType(1, "Administrator"));
                db.Insert(new UserType(2, "Operator"));
                db.Insert(new UserType(3, "Regular"));
            });
        }

        [Test, Order(6)]
        public void InsertDataAndReturnNewId()
        {
            var dbContext = DbHub.Use("db1");
            var icon = File.ReadAllBytes(Path.Combine(".", "Content", "ThomasIco.png"));
            var id = dbContext.Insert<User, int>(new User(0, 3, "Jean", true, 1340.5m, new DateTime(1997, 3, 21), Guid.NewGuid(), icon));
            Assert.That(Convert.ToInt32(id), Is.GreaterThan(0));
        }

        [Test, Order(7)]
        public void InsertData()
        {
            var dbContext = DbHub.Use("db1");
            var icon = File.ReadAllBytes(Path.Combine(".", "Content", "ThomasIco.png"));
            dbContext.Insert(new User(0, 2, "Peter", false, 3350.99m, new DateTime(1989, 5, 17), Guid.NewGuid(), icon));
            dbContext.Insert(new User(0, 2, "Jean", true, 1346.23m, new DateTime(1989, 5, 17), Guid.NewGuid(), icon));
            dbContext.Insert(new User(0, 1, "John", true, 6344.98m, new DateTime(1989, 5, 17), Guid.NewGuid(), icon));
            Assert.Pass();
        }

        [Test, Order(8)]
        public void UpdateData()
        {
            var dbContext = DbHub.Use("db1");
            var icon = File.ReadAllBytes(Path.Combine(".", "Content", "rocket.png"));
            dbContext.Update(new User(1, 3, "Paul", false, 3350.99m, new DateTime(1989, 5, 17), Guid.NewGuid(), icon));
            var user = dbContext.FetchOne<User>(x => x.Id == 1);
            Assert.That(user.Name, Is.EqualTo("Paul"));
            Assert.That(icon.Length, Is.EqualTo(user.Icon.Length));
        }

        [Test, Order(9)]
        public void DeleteData()
        {
            var dbContext = DbHub.Use("db1");
            var user = dbContext.FetchOne<User>(x => x.Id == 1);
            if (user == null)
                Assert.Fail("User not found");
            else
            {
                dbContext.Delete(user);
                Assert.Pass();
            }
        }

        #region data types
        [Test]
        public void GuidTest()
        {
            var dbContext = DbHub.Use("db1");
            var param = new { Value = Guid.NewGuid() };
            var data = dbContext.ExecuteScalar<Guid>($"SELECT $Value", param);
            Assert.That(param.Value, Is.EqualTo(data));

            var data2 = dbContext.FetchOne<SimpleGuidRecord>($"SELECT $Value as Value", param);
            Assert.That(param.Value, Is.EqualTo(data2.Value));
        }

        [Test]
        public void TimeSpanTest()
        {
            var dbContext = DbHub.Use("db1");
            var param = new { Value = TimeSpan.FromSeconds(100) };
            var data = dbContext.ExecuteScalar<TimeSpan>($"SELECT $Value", param);
            Assert.That(param.Value, Is.EqualTo(data));

            var data2 = dbContext.FetchOne<SimpleTimeSpanRecord>($"SELECT $Value as Value", param);
            Assert.That(param.Value, Is.EqualTo(data2.Value));

            var data3 = dbContext.FetchOne<SimpleTimeSpanRecord>($"SELECT $Value as Value", new { Value = "00:10:00" });
            Assert.That(new TimeSpan(0, 10, 0), Is.EqualTo(data3.Value));

            Assert.Throws<TimeSpanConversionException>(() => dbContext.FetchOne<SimpleTimeSpanRecord>($"SELECT 'some string value' as Value"));
        }

        [Test]
        public void NullableValueTypeTest()
        {
            var dbContext = DbHub.Use("db1");
            var param = new { Value = (int?)null };
            var data = dbContext.ExecuteScalar<int?>($"SELECT $Value", param);
            Assert.That(param.Value, Is.EqualTo(data));
            Assert.Throws<DbNullToValueTypeException>(() => dbContext.ExecuteScalar<int>($"SELECT $Value", param));
        }

        [Test]
        public void NullableNonValueTypeTest()
        {
            var dbContext = DbHub.Use("db1");
            var param = new { Value = (string?)null };
            var data = dbContext.ExecuteScalar<string>($"SELECT $Value", param);
            Assert.That(param.Value, Is.EqualTo(data));

            data = dbContext.ExecuteScalar<string?>($"SELECT $Value", param);
            Assert.That(param.Value, Is.EqualTo(data));
        }

        [Test]
        public void NullableRecordFieldsTest()
        {
            var dbContext = DbHub.Use("db1");
            var user = new UserNullableRecord(0, 1, "Sample", null, null, null, null, null);
            var id = dbContext.Insert<UserNullableRecord, int>(user);
            var output = dbContext.FetchOne<UserNullableRecord>(x => x.Id == id);
            Assert.That(user.State, Is.EqualTo(output.State));
            Assert.That(user.Salary, Is.EqualTo(output.Salary));
            Assert.That(user.Birthday, Is.EqualTo(output.Birthday));
            Assert.That(user.UserCode, Is.EqualTo(output.UserCode));
        }

        [Test]
        public void NullableClassFieldsTest()
        {
            var dbContext = DbHub.Use("db1");
            var user = new UserNullableClass { Name = "Sample 2" };
            var id = dbContext.Insert<UserNullableClass, int>(user);
            var output = dbContext.FetchOne<UserNullableClass>(x => x.Id == id);
            Assert.That(user.State, Is.EqualTo(output.State));
            Assert.That(user.Salary, Is.EqualTo(output.Salary));
            Assert.That(user.Birthday, Is.EqualTo(output.Birthday));
            Assert.That(user.UserCode, Is.EqualTo(output.UserCode));
            Assert.That(user.Icon, Is.EqualTo(output.Icon));
        }

        #endregion

        #region queries

        [Test]
        [TestCase("some value")]
        [TestCase("lorem ipsum dolor sit amet, consectetur adipiscing elit.")]
        [TestCase("lorem ipsum dolor sit amet, consectetur adipiscing elit. Integer nec odio. Praesent libero. Sed cursus ante dapibus diam. Sed nisi. Nulla quis sem at nibh elementum imperdiet.")]
        [TestCase("")]
        public void SingleValue(string stringValue)
        {
            var dbContext = DbHub.Use("db1");
            var value = dbContext.ExecuteScalar<string>($"SELECT '{stringValue}'");
            Assert.That(value, Is.EqualTo(stringValue));
        }

        [Test]
        [TestCase("some value")]
        [TestCase("lorem ipsum dolor sit amet, consectetur adipiscing elit.")]
        [TestCase("lorem ipsum dolor sit amet, consectetur adipiscing elit. Integer nec odio. Praesent libero. Sed cursus ante dapibus diam. Sed nisi. Nulla quis sem at nibh elementum imperdiet.")]
        [TestCase("")]
        [TestCase(null)]
        public void SingleByParamValue(string stringValue)
        {
            var dbContext = DbHub.Use("db1");
            var value = dbContext.ExecuteScalar<string>($"SELECT @Value", new { Value = stringValue });
            Assert.That(value, Is.EqualTo(stringValue));
        }


        [Test]
        [TestCase("E")]
        [TestCase("A")]
        public void ComplexQuery(string filter)
        {
            var dbContext = DbHub.Use("db1");
            var result = dbContext.FetchList<User>(x => (x.State &&
                                         x.Name.Contains(filter) &&
                                         x.Birthday < DateTime.Now) ||
                                         SqlExpression.Between<User>(x => x.Birthday, new DateTime(1950, 1, 1), DateTime.MaxValue) &&
                                         (x.Salary % 2) > 0);
            Assert.IsNotEmpty(result);
        }

        [Test]
        [TestCase(1, 10)]
        [TestCase(1, 100)]
        [TestCase(1, 500)]
        public void QueryWithArraysParameters(int start, int count)
        {
            var list = Enumerable.Range(start, count).ToArray();
            var dbContext = DbHub.Use("db1");
            var people = dbContext.FetchList<User>(x => list.Contains(x.Id));
            Assert.IsNotEmpty(people);
        }

        [Test]
        public void QueryWithExists()
        {
            var dbContext = DbHub.Use("db1");
            var result = dbContext.FetchList<User>(x => SqlExpression.Exists<User, UserType>((user, userType) => user.UserTypeId == userType.Id));
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void QueryWithLike()
        {
            var dbContext = DbHub.Use("db1");
            var icon = File.ReadAllBytes(Path.Combine(".", "Content", "ThomasIco.png"));
            dbContext.Insert(new User(0, 2, "John", false, 3350.99m, new DateTime(1989, 5, 17), Guid.NewGuid(), icon));
            User result = dbContext.FetchOne<User>(x => x.Name.Contains('o') && x.Name.EndsWith("n") && x.Name.StartsWith("J"));
            Assert.That(result, Is.Not.Null);
        }

        [Test]
        public void QueryNullAndNotNull()
        {
            var dbContext = DbHub.Use("db1");
            var icon = File.ReadAllBytes(Path.Combine(".", "Content", "ThomasIco.png"));
            var user = new UserNullableRecord(0, 2, "John", null, 351.94m, new DateTime(1996, 7, 28), Guid.NewGuid(), icon);
            dbContext.Insert(user);
            var result = dbContext.FetchOne<UserNullableRecord>(x => x.State == null && x.Name != null && x.Name == "John");
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo(user.Name));
            Assert.That(result.State, Is.Null);
            Assert.That(result.Salary, Is.EqualTo(user.Salary));
            Assert.That(result.Birthday, Is.EqualTo(user.Birthday));
        }

        [Test]
        public async Task ComplexQueryAsync()
        {
            var dbContext = DbHub.Use("db1");
            string filterName = "A";
            var result = await dbContext.FetchListAsync<User>(x => (x.State &&
                                         x.Name.Contains(filterName) &&
                                         x.Birthday < DateTime.Now) ||
                                         SqlExpression.Between<User>(x => x.Birthday, new DateTime(1950, 1, 1), DateTime.MaxValue) &&
                                         (x.Salary % 2) > 0);

            Assert.IsNotEmpty(result);
        }

        [Test]
        public void QueryWithSelectors()
        {
            var dbContext = DbHub.Use("db1");
            var result = dbContext.FetchList<User>(x => x.Id > 0, x => new { x.Id, x.Name });
            Assert.That(result, Is.Not.Empty);
            Assert.That(result[0].Id, Is.GreaterThan(0));
            Assert.That(result[0].Name, Is.Not.Null);
            Assert.That(result[0].State, Is.EqualTo(default(bool)));
            Assert.That(result[0].Salary, Is.EqualTo(default(decimal)));
            Assert.That(result[0].Birthday, Is.EqualTo(default(DateTime)));
            Assert.That(result[0].UserCode, Is.EqualTo(default(Guid)));
        }

        [Test]
        [TestCase(1, 10)]
        [TestCase(1, 100)]
        [TestCase(1, 500)]
        public async Task QueryWithArraysParametersAsync(int start, int count)
        {
            var list = Enumerable.Range(start, count).ToArray();
            var dbContext = DbHub.Use("db1");
            var people = await dbContext.FetchListAsync<User>(x => list.Contains(x.Id));
            Assert.IsNotEmpty(people);
        }

        [Test]
        public async Task QueryWithExistsAsync()
        {
            var dbContext = DbHub.Use("db1");
            var result = await dbContext.FetchListAsync<User>(x => SqlExpression.Exists<User, UserType>((user, userType) => user.UserTypeId == userType.Id));
            Assert.IsNotEmpty(result);
        }


        [Test]
        public void ToListByQueryTextTest()
        {
            var dbContext = DbHub.Use("db1");
            var users = dbContext.FetchList<User>("SELECT * FROM USERS");
            Assert.That(users, Is.Not.Empty);
        }

        [Test]
        public void ToSingleByQueryTextTest()
        {
            var dbContext = DbHub.Use("db1");
            var icon = File.ReadAllBytes(Path.Combine(".", "Content", "ThomasIco.png"));
            var id = dbContext.Insert<User, int>(new User(0, 2, "Jean", true, 1340.5m, new DateTime(1997, 3, 21), Guid.NewGuid(), icon));
            var user = dbContext.FetchOne<User>("SELECT * FROM USERS WHERE ID = $Id", new { Id = id });
            Assert.That(user, Is.Not.Null);
        }

        [Test]
        public void ToListByExpressionTest()
        {
            var dbContext = DbHub.Use("db1");
            var icon = File.ReadAllBytes(Path.Combine(".", "Content", "ThomasIco.png"));
            dbContext.Insert(new User(0, 2, "Jean", true, 1340.5m, new DateTime(1997, 3, 21), Guid.NewGuid(), icon));
            var users = dbContext.FetchList<User>();
            Assert.That(users, Is.Not.Empty);
        }


        [Test]
        public void ToSingleByExpressionTest()
        {
            var dbContext = DbHub.Use("db1");
            var icon = File.ReadAllBytes(Path.Combine(".", "Content", "ThomasIco.png"));
            var id = dbContext.Insert<User, int>(new User(0, 2, "Jean", true, 1340.5m, new DateTime(1997, 3, 21), Guid.NewGuid(), icon));
            var user = dbContext.FetchOne<User>(x => x.Id == id);
            Assert.That(user, Is.Not.Null);
        }

        [Test]
        public void TryFetchListByTextTest()
        {
            var dbContext = DbHub.Use("db1");
            var result = dbContext.TryFetchList<User>("SELECT * FROM USERS");
            Assert.That(result.Success, Is.True);
            Assert.That(result.Result, Is.Not.Empty);
        }

        [Test]
        public void TryFetchOneByQueryTextTest()
        {
            var dbContext = DbHub.Use("db1");
            var icon = File.ReadAllBytes(Path.Combine(".", "Content", "ThomasIco.png"));
            var id = dbContext.Insert<User, int>(new User(0, 2, "Jean", true, 1340.5m, new DateTime(1997, 3, 21), Guid.NewGuid(), icon));
            var result = dbContext.TryFetchOne<User>("SELECT * FROM USERS WHERE ID = $Id", new { Id = id });
            Assert.That(result.Success, Is.True);
            Assert.That(result.Result, Is.Not.Null);
        }

        #endregion
    }
}
