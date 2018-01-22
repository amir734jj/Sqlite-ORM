using System;
using System.Collections.Generic;
using System.Linq;
using AutoFixture;
using Sqlite.ORM.Interfaces;
using Xunit;

namespace Sqlite.ORM.Tests
{
    public class TypeUtilityTest
    {
        private readonly ITypeUtility _typeUtility;
        private readonly IFixture _fixture;
        
        public TypeUtilityTest()
        {
            _typeUtility = new TypeUtility();
            _fixture = new Fixture();
            _fixture.Register(ObjectId.GenerateNewId);
        }

        [Fact]
        public void Test__PropertyToDictionary()
        {
            // Arrange
            var actualPropertyDictionary = new Dictionary<string, Type>()
            {
                {"FirstName", typeof(string)},
                {"LastName", typeof(string)},
                {"Age", typeof(int)},
                {"Height", typeof(double)},
                {"Worth", typeof(float)},
                {"Weight", typeof(long)},
                {"DateOfBirth", typeof(DateTime)},
                {"Initial", typeof(char)},
                {"Parents.MotherName", typeof(string)},
                {"Parents.FatherName", typeof(string)},
                {"Parents.Status", typeof(bool)},
                {"ObjectIdNumber", typeof(ObjectId)},
            };
            
            // Assing
            var propertyDictionary = _typeUtility.TypeToDictionary(typeof(DummyTestClass));
            
            // Assert
            Assert.Equal(propertyDictionary, actualPropertyDictionary);
        }

        [Fact]
        public void Test__DefaultValue()
        {
            // Arrange
            var actualInstance = new DummyTestClass()
            {
                FirstName = null,
                LastName = null,
                Age = 0,
                Height = 0,
                Worth = 0,
                Weight = 0,
                DateOfBirth = DateTime.MinValue,
                Initial = '\0',
                Parents = new DummyNestedTestClass()
                {
                    MotherName = null,
                    FatherName = null,
                    Status = false
                }
            };

            // Act
            var expectedInstance = _typeUtility.GetDefaultOfType(typeof(DummyTestClass));
            
            // Assert
            Assert.Equal(expectedInstance, actualInstance);
        }
        
        [Fact]
        public void Test__SetPropertyValue()
        {
            // Arrange
            var expectedInstance = _typeUtility.GetDefaultOfType(typeof(DummyTestClass));
            var fixtureInstance = _fixture.Create<DummyTestClass>();
            
            var propertyDictionary = new Dictionary<string, object>()
            {
                {"FirstName", fixtureInstance.FirstName},
                {"LastName", fixtureInstance.LastName},
                {"Age", fixtureInstance.Age},
                {"Height", fixtureInstance.Height},
                {"Worth", fixtureInstance.Worth},
                {"Weight", fixtureInstance.Weight},
                {"DateOfBirth", fixtureInstance.DateOfBirth},
                {"Initial", fixtureInstance.Initial},
                {"Parents.MotherName", fixtureInstance.Parents.MotherName},
                {"Parents.FatherName", fixtureInstance.Parents.FatherName},
                {"Parents.Status", fixtureInstance.Parents.Status},
                {"ObjectIdNumber", fixtureInstance.ObjectIdNumber},
            };

            // Act
            foreach (var (key, value) in propertyDictionary)
            {
                _typeUtility.SetPropertyValue(key, expectedInstance, value);
            }
            
            // Assert
            Assert.Equal(expectedInstance, fixtureInstance);
        }
        
        [Fact]
        public void Test__GetPropertyValue()
        {
            // Arrange
            var fixtureInstance = _fixture.Create<DummyTestClass>();

            var expectedpropertyDictionary = new Dictionary<string, object>();
            var propertyDictionary = new Dictionary<string, object>()
            {
                {"FirstName", fixtureInstance.FirstName},
                {"LastName", fixtureInstance.LastName},
                {"Age", fixtureInstance.Age},
                {"Height", fixtureInstance.Height},
                {"Worth", fixtureInstance.Worth},
                {"Weight", fixtureInstance.Weight},
                {"DateOfBirth", fixtureInstance.DateOfBirth},
                {"Initial", fixtureInstance.Initial},
                {"Parents.MotherName", fixtureInstance.Parents.MotherName},
                {"Parents.FatherName", fixtureInstance.Parents.FatherName},
                {"Parents.Status", fixtureInstance.Parents.Status},
                {"ObjectIdNumber", fixtureInstance.ObjectIdNumber},
            };

            // Act
            foreach (var (key, _) in propertyDictionary)
            {
                expectedpropertyDictionary.Add(key, _typeUtility.GetPropertyValue(key, fixtureInstance, typeof(DummyTestClass)));
            }
            
            // Assert
            Assert.Equal(expectedpropertyDictionary, propertyDictionary);
        }
    }
}