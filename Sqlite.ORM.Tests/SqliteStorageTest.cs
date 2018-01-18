using System;
using Xunit;
using Sqlite.ORM;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using Sqlite.ORM.Interfaces;

// this should stop running tests in parallel
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Sqlite.ORM.Tests
{
    [Collection("Sequential")]
    public class SqliteStorageTest : IDisposable
    {
        private Fixture DataFixture { get; set; }
        private ISqliteStorage<DummyTestClass> SqliteStorage { get; set; }

        public SqliteStorageTest()
        {
            DataFixture = new Fixture();
            DataFixture.Register(ObjectId.GenerateNewId);
            
            SqliteStorage = new SqliteStorage<DummyTestClass>(customTypes: new Dictionary<Type, (Func<object, string> serializer, Func<string, object> deserializer)>()
            {
                // add support for custom type
                {typeof(ObjectId), (obj => obj.ToString(), str => ObjectId.Parse(str))}
            });
            
            // delete table
            SqliteStorage.DeleteTable();

            // re-create the table
            SqliteStorage.CreateTable();

            // clean-up, delete all models
            Dispose();
        }
        
        [Fact]
        public void Test__Store()
        {
            // Assign
            var obj = DataFixture.Create<DummyTestClass>();

            // Act
            //SqliteStorage.Add(obj);

            // Assert
            Assert.Equal(1, SqliteStorage.Count());
        }

        [Fact]
        public void Test__Store__Aync()
        {
            Task.Run(() => Test__Store());
        }

        [Fact]
        public void Test__Store__Retrieve()
        {
            // Assign
            var obj = DataFixture.Create<DummyTestClass>();

            // Act
            SqliteStorage.Add(obj);
            var newObj = SqliteStorage.Find(obj);

            // Assert
            Assert.Equal(obj, newObj);
        }

        [Fact]
        public void Test__Store__Retrieve__Aync()
        {
            Task.Run(() => Test__Store__Retrieve());
        }

        [Fact]
        public void Test__Delete()
        {
            // Assign
            var obj = DataFixture.Create<DummyTestClass>();
            
            // Act
            SqliteStorage.Add(obj);
            SqliteStorage.DeleteAll();

            // Assert
            Assert.Equal(0, SqliteStorage.Count());
        }

        [Fact]
        public void Test__Delete__Aync()
        {
            Task.Run(() => Test__Delete());
        }

        [Fact]
        public void Test__Count()
        {
            // Assign
            const int countOfModels = 1000;
            var objects = DataFixture.CreateMany<DummyTestClass>(countOfModels).ToList();

            // Act
            SqliteStorage.AddAll(objects);

            // Assert
            Assert.Equal(countOfModels, SqliteStorage.Count());
        }

        [Fact]
        public void Test__Count__Aync()
        {
            Task.Run(() => Test__Count());
        }
        
        [Fact]
        public void Test__DeleteModels()
        {
            // Assign
            const int countOfModels = 10;
            var objects = DataFixture.CreateMany<DummyTestClass>(countOfModels).ToList();

            // Act
            SqliteStorage.AddAll(objects);
            SqliteStorage.Delete(objects);

            // Assert
            Assert.Equal(0, SqliteStorage.Count());
        }

        [Fact]
        public void Test__DeleteModels__Aync()
        {
            Task.Run(() => Test__DeleteModels());
        }
        
        [Fact]
        public void Test__RetrieveAll()
        {
            // Assign
            const int countOfModels = 10;
            var objects = DataFixture.CreateMany<DummyTestClass>(countOfModels).ToList();

            // Act
            SqliteStorage.AddAll(objects);

            // Assert
            Assert.Equal(objects, SqliteStorage.FindAll());
        }

        [Fact]
        public void Test__RetrieveAll__Aync()
        {
            Task.Run(() => Test__RetrieveAll());
        }
        
        [Fact]
        public void Test__Update()
        {
            // Assign
            const int countOfModels = 10;
            var objects = DataFixture.CreateMany<DummyTestClass>(countOfModels).ToList();
            var source = objects.First();
            var destination = objects.Last();
                
            // Act
            SqliteStorage.AddAll(objects);
            SqliteStorage.Update(source, destination);

            // Assert
            Assert.Equal(2, SqliteStorage.FindAll(destination).Count());
        }

        [Fact]
        public void Test__Update__Aync()
        {
            Task.Run(() => Test__Update());
        }

        /// <inheritdoc />
        /// <summary>
        /// Cleanup after each test run
        /// </summary>
        public void Dispose()
        {
            SqliteStorage.DeleteAll();
        }
    }
}
