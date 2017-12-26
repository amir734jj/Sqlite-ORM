using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Sqlite.ORM
{
    /// <summary>
    /// Configurations for ORM
    /// </summary>
    internal static class Configuration
    {
        public const string TableNamePrefix = "DATA__";
        public const string TableNameSufix = "__TABLE";
        public static readonly string NewLine = Environment.NewLine;
        public const int LimitCount = 100;
    }
    
    internal static class DataType
    {
        /// <summary>
        /// Sqlite supported types
        /// </summary>
        public enum SqliteTypes
        {
            Text,
            Numeric,
            Integer,
            Real,
            Blob
        }

        /// <summary>
        /// Map between C# data types and Sqlite data types, used for creating a database table
        /// </summary>
        public static readonly Dictionary<Type, SqliteTypes> DataTypeToSqliteTypeDictionary = new Dictionary<Type, SqliteTypes>()
        {
            { typeof(double), SqliteTypes.Real },
            { typeof(float), SqliteTypes.Real },
            { typeof(long), SqliteTypes.Numeric },
            { typeof(int), SqliteTypes.Integer },
            { typeof(string), SqliteTypes.Text },
            { typeof(byte[]), SqliteTypes.Blob },
        };
    }
    

    /// <summary>
    /// Data access layer to store models into Sqlite database,
    /// creates a table
    /// </summary>
    public class SqliteStorage<T> : ISqliteStorage<T>, IDisposable
    {
        private string DataBaseStoragePath { get; set; }
        private SqliteConnection SqliteConnection { get; set; }
        private string TableName { get; set; }
        private List<PropertyInfo> ModelProperties { get; set; }
        private List<string> ModelPropertiesNames { get; set; }
        public SqliteStorage() : this(typeof(T).Name) { }
        public List<SqliteTransaction> Transactions { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dataBaseStoragePath"></param>
        public SqliteStorage(string dataBaseStoragePath)
        {
            // set table name
            TableName = Configuration.TableNamePrefix + typeof(T).Name.ToUpper() + Configuration.TableNameSufix;

            // set database storgae path
            DataBaseStoragePath = dataBaseStoragePath;
            
            // initialize transactions list
            Transactions = new List<SqliteTransaction>();
            
            // set model properties
            ModelProperties = typeof(T).GetProperties().ToList();
            
            // set model properties names
            ModelPropertiesNames = ModelProperties.Select(x => x.Name).ToList();

            // connect to database
            Connect();
            
            // create table
            CreateTable();
            
            Console.WriteLine($"Connection string: {SqliteConnection.ConnectionString}");
        }

        /// <summary>
        /// Creates connection string and connects to database
        /// </summary>
        private void Connect()
        {
            var connectionString = new SqliteConnectionStringBuilder {
                DataSource = DataBaseStoragePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private            
            };

            SqliteConnection = new SqliteConnection(connectionString.ToString());
            SqliteConnection.Open();
        }

        
        /// <summary>
        /// Creates table given model type
        /// </summary>
        private void CreateTable()
        {
            var schema = string.Join(',', ModelProperties.Select(x =>
                $"'{x.Name}' {DataType.DataTypeToSqliteTypeDictionary[x.PropertyType]}"));

            var commandText = $@"
                    CREATE TABLE IF NOT EXISTS
                        '{TableName}' ( {schema} );
                    ";

            CreateAndExecuteNonQueryCommand(commandText);
        }
        
        /// <summary>
        /// Stores model into database
        /// </summary>
        /// <param name="obj"></param>
        public void StoreModel (T obj)
        {
            var propertiesSchema = string.Join(',', ModelPropertiesNames);
            var propertiesValues = string.Join(',', ModelProperties.Select(x =>
                $"'{DataConverterUponStore(obj.GetType().GetProperty(x.Name).GetValue(obj, null), x.PropertyType)}'"));
            
            var commandText = $@"
                    INSERT INTO {TableName}
                        ({propertiesSchema})
                    VALUES
                        ({propertiesValues});
                    ";

            CreateAndExecuteNonQueryCommand(commandText);
        }


        /// <summary>
        /// Stores list of models into database
        /// </summary>
        /// <param name="objects"></param>
        public void StoreModels(IEnumerable<T> objects)
        {
            var propertiesSchema = string.Join(',', ModelPropertiesNames);
            var propertiesValues = string.Join(',', objects.Select(obj => {
                return '(' + string.Join(',', ModelProperties.Select(x =>
                    $"'{DataConverterUponStore(obj.GetType().GetProperty(x.Name).GetValue(obj, null), x.PropertyType)}'")) + ')';
            }));

            var commandText = $@"
                    INSERT INTO {TableName}
                        ({propertiesSchema})
                    VALUES
                        {propertiesValues};
                    ";

            CreateAndExecuteNonQueryCommand(commandText);
        }


        /// <summary>
        /// Retrieves a model from database
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public T RetrieveModel(T model)
        {
            return RetrieveModel(ConvertModelToDictionary(model));
        }

        /// <summary>
        /// Given key value pair of property and property values, it returns an object
        /// </summary>
        /// <param name="keyValueDictionary"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public T RetrieveModel(Dictionary<string, object> keyValueDictionary)
        {
            if (keyValueDictionary == null || keyValueDictionary.Count == 0 || !CheckModelKeyValueDictionary(keyValueDictionary))
            {
                throw new ArgumentException($"There are keys in given the dictionary that do not exist in model");
            }
            
            var propertiesSchema = string.Join(',', ModelPropertiesNames);
            var propertiesValues = string.Join("AND", keyValueDictionary.Select(keyValuePair => $" {keyValuePair.Key} = '{keyValuePair.Value}' "));
            
            var commandText = $@"
                    SELECT {propertiesSchema}
                    FROM {TableName}
                    WHERE {propertiesValues}
                    LIMIT 1;
                    ";

            var command = CreateCommand(commandText);
            var reader = command.ExecuteReader();
            
            var obj = CreateRawObject();

            if (!reader.Read()) return obj;
            
            // fill object properties using the reader
            SetObjectPropertiesFromReader(obj, obj.GetType(), reader);

            command.Dispose();
            Dispose();
            
            return obj;
        }

        /// <summary>
        /// Given key value pair of property and property values, it returns an object
        /// </summary>
        /// <param name="keyValueDictionary"></param>
        /// <param name="limitCount"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public List<T> RetrieveModels(Dictionary<string, object> keyValueDictionary, int limitCount = Configuration.LimitCount)
        {
            if (keyValueDictionary == null || keyValueDictionary.Count == 0 || !CheckModelKeyValueDictionary(keyValueDictionary))
            {
                throw new ArgumentException($"There are keys in given the dictionary that do not exist in model");
            }
            
            var propertiesSchema = string.Join(',', ModelPropertiesNames);
            var propertiesValues = string.Join("AND", keyValueDictionary.Select(keyValuePair => $" {keyValuePair.Key} = '{keyValuePair.Value}' "));
            
            var commandText = $@"
                    SELECT {propertiesSchema}
                    FROM {TableName}
                    WHERE {propertiesValues}
                    LIMIT {limitCount};
                    ";

            var command = CreateCommand(commandText);
            var reader = command.ExecuteReader();
            
            var retVal = new List<T>();

            if (!reader.Read()) return retVal;

            while (reader.Read())
            {
                var obj = CreateRawObject();
                
                // fill object properties using the reader
                SetObjectPropertiesFromReader(obj, obj.GetType(), reader);
                
                retVal.Add(obj);
            }
            
            command.Dispose();
            Dispose();
            
            return retVal;
        }

        /// <summary>
        /// Retrieves all models
        /// </summary>
        /// <returns></returns>
        public List<T> RetrieveAllModels(int limitCount = Configuration.LimitCount)
        {
            var propertiesSchema = string.Join(',', ModelPropertiesNames);

            var commandText = $@"
                    SELECT {propertiesSchema}
                    FROM {TableName}
                    LIMIT {limitCount};
                    ";

            var command = CreateCommand(commandText);
            var reader = command.ExecuteReader();

            var retVal = new List<T>();

            if (!reader.Read()) return retVal;

            while (reader.Read())
            {
                var obj = CreateRawObject();
                
                // fill the object properties using the reader
                SetObjectPropertiesFromReader(obj, obj.GetType(), reader);
                
                retVal.Add(obj);
            }
            
            command.Dispose();
            Dispose();
            
            return retVal;
        }

        public void DeleteModel(T models)
        {
            DeleteModel(ConvertModelToDictionary(models));
        }

        /// <summary>
        /// Deletes all models from database
        /// </summary>
        /// <param name="keyValueDictionary"></param>
        public void DeleteModel(Dictionary<string, object> keyValueDictionary)
        {
            var propertiesValues = string.Join("AND", keyValueDictionary.Select(keyValuePair => $"{keyValuePair.Key} = '{keyValuePair.Value}'"));
            
            var commandText = $@"
                    DELETE FROM {TableName}
                    WHERE {propertiesValues}
                    LIMIT 1;
                    ";
            
            CreateAndExecuteNonQueryCommand(commandText);
        }
        
        
        /// <summary>
        /// Deletes all models from list of models
        /// </summary>
        /// <param name="models"></param>
        public void DeleteModels(List<T> models)
        {
            models.ForEach(x => DeleteModel(ConvertModelToDictionary(x)));
        }

        /// <summary>
        /// Deletes all models from database, optionally we can specify the limit
        /// </summary>
        /// <param name="keyValueDictionary"></param>
        public void DeleteModels(Dictionary<string, object> keyValueDictionary, int limitCount = Configuration.LimitCount)
        {
            var propertiesValues = string.Join("AND", keyValueDictionary.Select(keyValuePair => $"{keyValuePair.Key} = '{keyValuePair.Value}'"));
            
            var commandText = $@"
                    DELETE FROM {TableName}
                    WHERE {propertiesValues}
                    LIMIT {limitCount};
                    ";
            
            CreateAndExecuteNonQueryCommand(commandText);
        }
        
        /// <summary>
        /// Deletes all models from table
        /// </summary>
        public void DeleteAllModels()
        {
            var commandText = $@"
                    DELETE FROM {TableName};
                    ";
            
            CreateAndExecuteNonQueryCommand(commandText);
        }

        /// <summary>
        /// Get number of models in database
        /// </summary>
        /// <returns></returns>
        public int GetCountOfModels()
        {
            var commandText = $@"
                    SELECT COUNT (*)
                    FROM {TableName};
                    ";

            return Convert.ToInt32(CreateCommand(commandText).ExecuteScalar());
        }
        
        
        #region HELPERS
        
        /// <summary>
        /// Checks of all properties in key value dictionary do exist in model
        /// </summary>
        /// <param name="keyValueDictionary"></param>
        /// <returns></returns>
        private bool CheckModelKeyValueDictionary(Dictionary<string, object> keyValueDictionary)
        {
            return keyValueDictionary.All(propertyName => ModelPropertiesNames.Contains(propertyName.Key));
        }

        /// <summary>
        /// Creates raw object using activator
        /// </summary>
        /// <returns></returns>
        private static T CreateRawObject()
        {
            return Activator.CreateInstance<T>();
        }

        
        /// <summary>
        /// Sets object properties using reader
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="objectType"></param>
        /// <param name="reader"></param>
        private void SetObjectPropertiesFromReader(T obj, Type objectType, IDataRecord reader)
        {
            foreach (var property in ModelProperties)
            {
                var value = reader[property.Name] ?? GetDefaultOfType(property.PropertyType);
                
                objectType.GetProperty(property.Name).SetValue(obj, DataConverterUponRetrieve(value, property.PropertyType), null);
            }
        }

        /// <summary>
        /// Helper function that creats SQL commands
        /// </summary>
        /// <param name="commandText"></param>
        /// <returns></returns>
        private SqliteCommand CreateCommand(string commandText)
        {
            if (SqliteConnection.State != ConnectionState.Open)
            {
                SqliteConnection.Open();
            }

            return new SqliteCommand(commandText, SqliteConnection);
        }

        /// <summary>
        /// Helper function that creats SQL commands
        /// </summary>
        /// <param name="commandText"></param>
        /// <returns></returns>
        private void CreateAndExecuteNonQueryCommand(string commandText)
        {
            if (SqliteConnection.State != ConnectionState.Open)
            {
                SqliteConnection.Open();
            }

            var transaction = SqliteConnection.BeginTransaction();
            var command = SqliteConnection.CreateCommand();
            command.Transaction = transaction;
            
            Transactions.Add(transaction);
            
            try
            {             
                command.CommandText = commandText;
                command.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                throw new ArgumentException(e?.Message);
            }
            finally
            {
                transaction.Commit();
                transaction.Dispose();
                command.Dispose();
                
                Dispose();
            }
        }
        
        /// <summary>
        /// Converts model object to key/value dictionary of property name/property value
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private Dictionary<string, object> ConvertModelToDictionary(T obj)
        {
            var objectType = obj.GetType();

            return ModelProperties
                .Where(x => objectType.GetProperty(x.Name).GetValue(obj, null) != null)
                .ToDictionary(x => x.Name, x => objectType.GetProperty(x.Name).GetValue(obj, null));
        }
        

        /// <inheritdoc />
        /// <summary>
        /// Clean up, close the connection
        /// </summary>
        public void Dispose()
        {
            // SqliteConnection.Close();
        }
        
        #endregion

        #region STATIC_HELPERS

        /// <summary>
        /// Returns default value given type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static object GetDefaultOfType(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        /// <summary>
        /// Converts property value just before store to database
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static object DataConverterUponStore(object value, Type type)
        {
            if (value == null && type == typeof(string))
            {
                return string.Empty;
            }

            return value;
        }

        /// <summary>
        /// Converts property value just before retrieve
        /// </summary>
        /// <param name="data"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static object DataConverterUponRetrieve(object data, Type type)
        {
            if (data == null)
            {
                return null;
            }

            // this was needed otherwise code thorws an exception
            return data is long ? Convert.ToInt32(data) : data;
        }
        
        #endregion
    }
}
