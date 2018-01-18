using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Sqlite.ORM.Interfaces;

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
        public const string IdColumnName = "___Id";

        private const string DefaultDatabaseName = "db.sqlite";
        private static readonly string DefaultDatabaseFolderPath = Environment.CurrentDirectory;
        public static readonly string DefaultDataBaseStoragePath = Path.Combine(DefaultDatabaseFolderPath, DefaultDatabaseName);
    }
    
    public static class DataType
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
            Blob,
            Table
        }

        /// <summary>
        /// Map between C# data types and Sqlite data types, used for creating a database table
        /// </summary>
        public static readonly Dictionary<Type, SqliteTypes> DataTypeToSqliteTypeDictionary = new Dictionary<Type, SqliteTypes>()
        {
            { typeof(int), SqliteTypes.Integer },
            { typeof(long), SqliteTypes.Numeric },
            { typeof(float), SqliteTypes.Real },
            { typeof(double), SqliteTypes.Real },
            { typeof(string), SqliteTypes.Text },
            { typeof(DateTime), SqliteTypes.Text },
            { typeof(char), SqliteTypes.Text },
            { typeof(bool), SqliteTypes.Text },
            { typeof(List<>), SqliteTypes.Table }
        };
    }

    /// <summary>
    /// SqliteStorgae factory class
    /// </summary>
    public class SqliteStorageFactory
    {
        /// <summary>
        /// Instantiates a generic SqliteStorge class
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static SqliteStorage<object> SqliteStorageGeneric(Type type)
        {
            var listType = typeof(SqliteStorage<>);
            var constructedListType = listType.MakeGenericType(type);
            var instance = Activator.CreateInstance(constructedListType, new object[] { });

            return instance as SqliteStorage<object>;
        }
    }

    /// <inheritdoc>
    ///     <cref></cref>
    /// </inheritdoc>
    /// <summary>
    /// Data access layer to store models into Sqlite database,
    /// creates a table
    /// </summary>
    public class SqliteStorage<T> : ISqliteStorage<T>, IDisposable
    {
        private string _dataBaseStoragePath;
        private SqliteConnection _sqliteConnection;
        private string _tableName;
        private Dictionary<string, Type> _modelProperties;
        private List<string> _modelPropertiesNames;
        private List<SqliteTransaction> _transactions;
        private ITypeUtility _typeUtility;
        private Dictionary<Type, (Func<object, string> serializer, Func<string, object> deserializer)> _customTypes;
        private Dictionary<string, (Type type, SqliteStorage<object> sqliteStorage)> _referencedTables;
        private bool _initialized;
        
        // this is used to make ORM thread safe
        private static readonly object _lock = new object();
        
        #region CONSTRUCTORS

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dataBaseStoragePath"></param>
        /// <param name="customTypes"></param>
        public SqliteStorage(string dataBaseStoragePath = null, Dictionary<Type, (Func<object, string> serializer, Func<string, object> deserializer)> customTypes = null)
        {
            // set database storgae path
            _dataBaseStoragePath = dataBaseStoragePath ?? Configuration.DefaultDataBaseStoragePath;
            
            // initialize custom type dictionary
            _customTypes = customTypes ?? new Dictionary<Type, (Func<object, string> serializer, Func<string, object> deserializer)>();
            
            // set table name
            _tableName = Configuration.TableNamePrefix + typeof(T).Name.ToUpper() + Configuration.TableNameSufix;
            
            // create a type utility instance
            _typeUtility = new TypeUtility();
            
            // initialize transactions list
            _transactions = new List<SqliteTransaction>();
            
            // referenced tables
            _referencedTables = new Dictionary<string, (Type type, SqliteStorage<object> sqliteStorage)>();
            
            // initialize table
            Initialize();
            
            Console.WriteLine($"Connection string: {_sqliteConnection.ConnectionString}");
        }

        /// <summary>
        /// Initialize
        /// </summary>
        private void Initialize()
        {
            if (_initialized) return;
            
            // add custom types to type utility so it is aware
            foreach (var (type, _) in _customTypes)
            {
                // this is needed so recursive algorithm would not try to digest the custom class to find system type
                _typeUtility.AddLeafType(type);
                
                // default to text always
                DataType.DataTypeToSqliteTypeDictionary.TryAdd(type, DataType.SqliteTypes.Text);
            };
                        
            // set model properties
            _modelProperties = _typeUtility.TypeToDictionary(typeof(T));
            
            // set model properties names
            _modelPropertiesNames = _modelProperties.Keys.ToList();

            // connect to database
            Connect();
            
            // create table
            CreateTable();

            // set flag to initialized
            _initialized = true;
        }
        
        #endregion

        #region INTERNAL
        
        /// <summary>
        /// Creates connection string and connects to database
        /// </summary>
        private void Connect()
        {
            var connectionString = new SqliteConnectionStringBuilder {
                DataSource = _dataBaseStoragePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private            
            };
            
            lock (_lock)
            {
                _sqliteConnection = new SqliteConnection(connectionString.ToString());
                _sqliteConnection.Open();
            }
        }

        /// <summary>
        /// Gets Sqlite type, best matching the propertyType
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private DataType.SqliteTypes GetSqliteType(Type type)
        {
            if (DataType.DataTypeToSqliteTypeDictionary.ContainsKey(type))
            {
                return DataType.DataTypeToSqliteTypeDictionary[type];
            }

            if (DataType.DataTypeToSqliteTypeDictionary.All(x => type.GetGenericTypeDefinition() != type))
            {
                throw new Exception("Type is not supported, sorry!");
            }
                
            return DataType.DataTypeToSqliteTypeDictionary.FirstOrDefault(x => type.GetGenericTypeDefinition() == type).Value;
        }

        /// <summary>
        /// Creates table given model type
        /// </summary>
        public void CreateTable()
        {
            var schema = string.Join(',', _modelProperties.Select(x =>
            {
                var sqliteType = GetSqliteType(x.Value);

                if (sqliteType.Equals(DataType.SqliteTypes.Table))
                {
                    // create generic instance of self
                    var nestedTable = SqliteStorageFactory.SqliteStorageGeneric(x.Value);
                    
                    // add generic instance to list
                    _referencedTables.Add(x.Key, (x.Value, nestedTable));

                    // use type Text for reference property
                    sqliteType = DataType.SqliteTypes.Table;
                }
                
                return $"`{x.Key}` {sqliteType}";
            }));

            var commandText = $@"
                    CREATE TABLE IF NOT EXISTS
                        '{_tableName}' ('{Configuration.IdColumnName}' INTEGER PRIMARY KEY AUTOINCREMENT, {schema} );
                    ";

            CreateAndExecuteNonQueryCommand(commandText);
        }
        
        #endregion

        #region FUNCTIONALITIES

        /// <summary>
        /// Run SQL commands directly against database
        /// </summary>
        /// <param name="commandText"></param>
        public object DirectCommand(string commandText)
        {
            lock (_lock)
            {
                var command = _sqliteConnection.CreateCommand();
                return ExecuteScalar(command);
            }
        }
        
        /// <summary>
        /// Stores model into database
        /// </summary>
        /// <param name="obj"></param>
        public void Add (T obj)
        {
            var propertiesSchema = string.Join(',', _modelPropertiesNames.Select(x => $"`{x}`"));
            var propertiesValues = ObjectToStatementClause(obj);
            
            var commandText = $@"
                    INSERT INTO {_tableName}
                        ({propertiesSchema})
                    VALUES
                        ({propertiesValues});
                    ";

            CreateAndExecuteNonQueryCommand(commandText);
            
            foreach (var (propertyName, (propertyType, sqliteStorage)) in _referencedTables)
            {
                sqliteStorage.AddAll((IEnumerable<object>) _typeUtility.GetPropertyValue(propertyName, obj, propertyType));
            }
        }


        /// <summary>
        /// Stores list of models into database
        /// </summary>
        /// <param name="objects"></param>
        public void AddAll(IEnumerable<T> objects)
        {
            var propertiesSchema = string.Join(',', _modelPropertiesNames.Select(x => $"`{x}`"));
            var propertiesValues = string.Join(',', objects.Select(obj => $"( {ObjectToStatementClause(obj)}) "));

            var commandText = $@"
                    INSERT INTO {_tableName}
                        ({propertiesSchema})
                    VALUES
                        {propertiesValues};
                    ";

            CreateAndExecuteNonQueryCommand(commandText);
        }

        /// <summary>
        /// Applies filter statement on FindAll and returns first or default
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public T Find(Func<T, bool> filter)
        {
            return FindAll().Where(filter).FirstOrDefault();
        }
        
        /// <summary>
        /// Applies filter statement on FindAll and returns list
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public IEnumerable<T> FindAll(Func<T, bool> filter)
        {
            return FindAll().Where(filter);
        }
        
        /// <summary>
        /// Retrieves a model from database
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public T Find(T model)
        {
            return Find(ConvertModelToDictionary(model));
        }

        /// <summary>
        /// Given key value pair of property and property values, it returns an object
        /// </summary>
        /// <param name="keyValueDictionary"></param>
        /// <returns></returns>
        public T Find(Dictionary<string, object> keyValueDictionary)
        {
            return FindAll(keyValueDictionary, 1).FirstOrDefault();
        }
        
        /// <summary>
        /// Retrieves all model from database
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public IEnumerable<T> FindAll(T model)
        {
            return FindAll(ConvertModelToDictionary(model));
        }
        
        /// <summary>
        /// Retrieves all models
        /// </summary>
        /// <returns></returns>
        public IEnumerable<T> FindAll(int limitCount = Configuration.LimitCount)
        {
            return FindAll(new Dictionary<string, object>());
        }
        
        /// <summary>
        /// Given key value pair of property and property values, it returns an object
        /// </summary>
        /// <param name="keyValueDictionary"></param>
        /// <param name="limitCount"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public IEnumerable<T> FindAll(Dictionary<string, object> keyValueDictionary, int limitCount = Configuration.LimitCount)
        {
            // if argument is null, then use empty dictionary
            keyValueDictionary = keyValueDictionary ?? new Dictionary<string, object>();
            
            if (!CheckModelKeyValueDictionary(keyValueDictionary))
            {
                throw new ArgumentException($"There are keys in given the dictionary that do not exist in model");
            }
            
            var propertiesSchema = string.Join(',', _modelPropertiesNames.Select(x => $"`{x}`"));
            var propertiesValues = DictionaryToAndClause(keyValueDictionary);
            
            
            var commandText = $@"
                    SELECT {propertiesSchema}
                    FROM {_tableName}
                    {(keyValueDictionary.Count > 0 ? $"WHERE {propertiesValues}" : $"{string.Empty}") }
                    LIMIT {limitCount};
                    ";

            var command = CreateCommand(commandText);
            var reader = ExecuteReader(command);
            
            var retVal = new List<T>();

            while (reader.Read())
            {
                var obj = (T) _typeUtility.GetDefaultOfType(typeof(T));
                
                // fill object properties using the reader
                SetObjectPropertiesFromReader(obj, obj.GetType(), reader);
                
                retVal.Add(obj);
            }
            
            // clean-up, necessary and important
            command.Dispose();
            reader.Dispose();
            Dispose();

            return retVal;
        }
        
        /// <summary>
        /// Updates a model given source of type of filter and destination
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        public void Update(Func<T, bool> filter, T destination)
        {
            Update(ConvertModelToDictionary(Find(filter)), ConvertModelToDictionary(destination));
        }
        
        /// <summary>
        /// Updates a model given source and destination
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        public void Update(T source, T destination)
        {
            Update(ConvertModelToDictionary(source), ConvertModelToDictionary(destination));
        }

        /// <summary>
        /// Updates data structure
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        /// <param name="limitCount"></param>
        /// <exception cref="ArgumentException"></exception>
        public void Update(Dictionary<string, object> source, Dictionary<string, object> destination, int limitCount = 1)
        {
            if (!(CheckModelKeyValueDictionary(source) && CheckModelKeyValueDictionary(destination) && destination.Count > 0))
            {
                throw new ArgumentException($"There are keys in given the dictionary that do not exist in model");
            }
            
            var propertiesSchema = string.Join(',', _modelPropertiesNames.Select(x => $"`{x}`"));
            var originalPropertiesValues = DictionaryToAndClause(source);
            var newPropertiesValues = DictionaryToJoinClause(destination);
            
            var commandText = $@"
                    UPDATE {_tableName}
                    SET {newPropertiesValues}
                    WHERE {Configuration.IdColumnName} IN (
                        SELECT {Configuration.IdColumnName}
                        FROM {_tableName}
                        {(source.Count > 0 ? $"WHERE {originalPropertiesValues}" : $"{string.Empty}") }
                        LIMIT {limitCount}
                    );
            ";

            CreateAndExecuteNonQueryCommand(commandText);
        }

        /// <summary>
        /// Deletes object model from database given filter function
        /// </summary>
        /// <param name="filter"></param>
        public void Delete(Func<T, bool> filter)
        {
            Delete(Find(filter));
        }
        
        /// <summary>
        /// Deletes object model from database
        /// </summary>
        /// <param name="model"></param>
        public void Delete(T model)
        {
            Delete(ConvertModelToDictionary(model));
        }
        
        /// <summary>
        /// Deletes all models from list of models
        /// </summary>
        /// <param name="models"></param>
        public void Delete(List<T> models)
        {
            models.ForEach(x => Delete(ConvertModelToDictionary(x)));
        }

        /// <summary>
        /// Deletes all models from database, optionally we can specify the limit
        /// </summary>
        /// <param name="keyValueDictionary"></param>
        /// <param name="limitCount"></param>
        public void Delete(Dictionary<string, object> keyValueDictionary, int limitCount = Configuration.LimitCount)
        {
            var propertiesValues = DictionaryToAndClause(keyValueDictionary);
            
            var commandText = $@"
                    DELETE FROM {_tableName}
                    WHERE {Configuration.IdColumnName} IN
                    (
                        SELECT {Configuration.IdColumnName}
                        FROM {_tableName}
                        WHERE {propertiesValues}
                        LIMIT {limitCount}
                    );";
            
            CreateAndExecuteNonQueryCommand(commandText);
        }
        
        /// <summary>
        /// Deletes all models from table
        /// </summary>
        public void DeleteAll()
        {
            var commandText = $@"
                    DELETE FROM {_tableName};
                    ";
            
            CreateAndExecuteNonQueryCommand(commandText);
        }

        /// <summary>
        /// Deletes actual table
        /// </summary>
        public void DeleteTable()
        {
            var commandText = $@"
                    DROP TABLE {_tableName};
                    ";
            
            CreateAndExecuteNonQueryCommand(commandText);
        }

        /// <summary>
        /// Get number of models in database
        /// </summary>
        /// <returns></returns>
        public int Count()
        {
            var commandText = $@"
                    SELECT COUNT (*)
                    FROM {_tableName};
                    ";

            var command = CreateCommand(commandText);

            var retVal = Convert.ToInt32(ExecuteScalar(command));
            
            command.Dispose();
            
            return retVal;
        }
        
        #endregion
        
        #region HELPERS

        /// <summary>
        /// Executes a reader
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private SqliteDataReader ExecuteReader(SqliteCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            
            lock (_lock)
            {
                return command.ExecuteReader();
            }
        }
        
        /// <summary>
        /// Executes a command
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private object ExecuteScalar(SqliteCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            
            lock (_lock)
            {
                return command.ExecuteScalar();
            }
        }

        private string ObjectToStatementClause(T obj)
        {
            return string.Join(',', _modelProperties.Select(x =>
                $"'{DataConverterUponQuery(_typeUtility.GetPropertyValue(x.Key, obj, typeof(T)), x.Value)}'"));
        }

        /// <summary>
        /// Dictionary to "AND" clause
        /// </summary>
        /// <param name="dictionary"></param>
        /// <returns></returns>
        private string DictionaryToAndClause(Dictionary<string, object> dictionary)
        {
            return DictionaryToClause(dictionary, "AND");
        }
        
        /// <summary>
        /// Dictionary to "AND" clause
        /// </summary>
        /// <param name="dictionary"></param>
        /// <returns></returns>
        private string DictionaryToJoinClause(Dictionary<string, object> dictionary)
        {
            return DictionaryToClause(dictionary, ",");
        }

        /// <summary>
        /// Creates a "SET" clause from key/value dictionary representation of an object
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="joinBy"></param>
        /// <returns></returns>
        private string DictionaryToClause(Dictionary<string, object> dictionary, string joinBy)
        {
            return string.Join(joinBy, dictionary.Select(keyValuePair => 
                $" `{keyValuePair.Key}` = '{DataConverterUponQuery(keyValuePair.Value, _modelProperties[keyValuePair.Key])}' "));
        }
        
        /// <summary>
        /// Checks of all properties in key value dictionary do exist in model
        /// </summary>
        /// <param name="keyValueDictionary"></param>
        /// <returns></returns>
        private bool CheckModelKeyValueDictionary(Dictionary<string, object> keyValueDictionary)
        {
            return keyValueDictionary.All(propertyName => _modelPropertiesNames.Contains(propertyName.Key));
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
            foreach (var (propertyName, propertyType) in _modelProperties)
            {
                var value = reader[propertyName] ?? GetDefaultOfType(propertyType);
                
                _typeUtility.SetPropertyValue(propertyName, obj, DataConverterUponRetrieve(value, propertyType));
            }
        }

        /// <summary>
        /// Helper function that creats SQL commands
        /// </summary>
        /// <param name="commandText"></param>
        /// <returns></returns>
        private SqliteCommand CreateCommand(string commandText)
        {
            lock (_lock)
            {
                if (_sqliteConnection.State != ConnectionState.Open)
                {
                    _sqliteConnection.Open();
                }
                
                return new SqliteCommand(commandText, _sqliteConnection);
            }
        }

        /// <summary>
        /// Helper function that creats SQL commands
        /// </summary>
        /// <param name="commandText"></param>
        /// <returns></returns>
        private void CreateAndExecuteNonQueryCommand(string commandText)
        {
            lock (_lock)
            {
                if (_sqliteConnection.State != ConnectionState.Open)
                {
                    _sqliteConnection.Open();
                }

                var transaction = _sqliteConnection.BeginTransaction();
                var command = _sqliteConnection.CreateCommand();
                command.Transaction = transaction;

                _transactions.Add(transaction);

                try
                {
                    command.CommandText = commandText;
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    throw new ArgumentException(e?.Message + commandText);
                }
                finally
                {
                    transaction.Commit();
                    transaction.Dispose();
                    command.Dispose();
                    Dispose();
                }
            }
        }
        
        /// <summary>
        /// Converts model object to key/value dictionary of property name/property value
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private Dictionary<string, object> ConvertModelToDictionary(T obj)
        {
            return _modelProperties.ToDictionary(x => x.Key, y => _typeUtility.GetPropertyValue(y.Key, obj, typeof(T)));
        }
        

        /// <inheritdoc />
        /// <summary>
        /// Clean up, close the connection
        /// </summary>
        public void Dispose()
        {
            _sqliteConnection.Close();
        }

        /// <summary>
        /// Rollback last operation, does not include read operations
        /// </summary>
        /// <returns></returns>
        public void RollbackLastOperation()
        {
            _transactions.Last().Rollback();
        }
        
        /// <summary>
        /// Converts property value just before store to database
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private object DataConverterUponQuery(object value, Type type)
        {
            // if type is in custom types then use custom serilizer
            if (_customTypes.ContainsKey(type)) return _customTypes[type].serializer(value);
            
            if (value == null && type == typeof(string))
            {
                return string.Empty;
            }

            // see this: https://stackoverflow.com/a/11912432/1834787
            if (value is char || value is string)
            {
                value = new Regex("[']").Replace(value.ToString(), "''");
            }

            return value;
        }

        /// <summary>
        /// Converts property value just before retrieve
        /// </summary>
        /// <param name="data"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private object DataConverterUponRetrieve(object data, Type type)
        {
            // if type is in custom types then use custom deserilizer
            if (_customTypes.ContainsKey(type)) return _customTypes[type].deserializer(data as string);
            
            // this was needed otherwise code thorws an exception
            switch (data)
            {
                case null:
                    return null;
                case long _ when type == typeof(int):
                    return Convert.ToInt32(data);
                case double _ when type == typeof(float):
                    return Convert.ToSingle(data);
                case string _ when type == typeof(DateTime):
                    return DateTime.Parse((string) data);
                case string _ when type == typeof(char):
                    return Convert.ToChar(data);
                case string _ when type == typeof(bool):
                    return Convert.ToBoolean(data);
            }

            return data;
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

        #endregion
    }
}
