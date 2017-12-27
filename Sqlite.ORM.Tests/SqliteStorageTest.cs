using System;
using Xunit;
using Sqlite.ORM;
using Ploeh.AutoFixture;
using System.Collections.Generic;
using System.Linq;

// this should stop running tests in parallel
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Sqlite.ORM.Tests
{
    public class DummyTestClass : IEquatable<DummyTestClass>
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
        public double Height { get; set; }
        public float Worth { get; set; }
        public long Weight { get; set; }

        public bool Equals(DummyTestClass other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(FirstName, other.FirstName) && string.Equals(LastName, other.LastName)
                   && Age == other.Age && Height.Equals(other.Height) && Worth.Equals(other.Worth) && Weight == other.Weight;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((DummyTestClass) obj);
        }
    }
    
    [Collection("Sequential")]
    public class SqliteStorageTest : IDisposable
    {
        private Fixture DataFixture { get; set; }
        private ISqliteStorage<DummyTestClass> SqliteStorage { get; set; }

        public SqliteStorageTest()
        {
            DataFixture = new Fixture();
            SqliteStorage = new SqliteStorage<DummyTestClass>();
            
            // delete table
            SqliteStorage.DeleteTable();

            // re-create the table
            SqliteStorage.CreateTable();
            
            Dispose();
        }
        
        [Fact]
        public void Test__Store()
        {
            // Assign
            var obj = DataFixture.Create<DummyTestClass>();

            // Act
            SqliteStorage.StoreModel(obj);

            // Assert
            Assert.Equal(1, SqliteStorage.GetCountOfModels());
        }

        [Fact]
        public void Test__Store__Retrieve()
        {
            // Assign
            var obj = DataFixture.Create<DummyTestClass>();

            // Act
            SqliteStorage.StoreModel(obj);
            var newObj = SqliteStorage.RetrieveModel(obj);

            // Assert
            Assert.Equal(obj, newObj);
        }

        [Fact]
        public void Test__Delete()
        {
            // Assign
            var obj = DataFixture.Create<DummyTestClass>();
            
            // Act
            SqliteStorage.StoreModel(obj);
            SqliteStorage.DeleteAllModels();

            // Assert
            Assert.Equal(0, SqliteStorage.GetCountOfModels());
        }

        [Fact]
        public void Test__Count()
        {
            // Assign
            const int countOfModels = 1000;
            var objects = DataFixture.CreateMany<DummyTestClass>(countOfModels).ToList();

            // Act
            SqliteStorage.StoreModels(objects);

            // Assert
            Assert.Equal(countOfModels, SqliteStorage.GetCountOfModels());
        }
        
        [Fact]
        public void Test__RetrieveAll()
        {
            // Assign
            const int countOfModels = 10;
            var objects = DataFixture.CreateMany<DummyTestClass>(countOfModels).ToList();

            // Act
            SqliteStorage.StoreModels(objects);

            // Assert
            Assert.Equal(objects, SqliteStorage.RetrieveAllModels());
        }

        /// <inheritdoc />
        /// <summary>
        /// Cleanup after each test run
        /// </summary>
        public void Dispose()
        {
            SqliteStorage.DeleteAllModels();
        }
    }
}
