using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Sqlite.ORM.Constants;
using Sqlite.ORM.Interfaces;

namespace Sqlite.ORM
{
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
        public static ISqliteStorageSimpleType SqliteStorageGeneric(Type type)
        {
            var nestedType = new TypeUtility().GetAnyElementType(type);
            var listType = typeof(SqliteStorage<>);
            var constructedListType = listType.MakeGenericType(nestedType);
            
            // the true there will tell constructor that this is a reference table
            dynamic instance = Activator.CreateInstance(constructedListType, null, null, true);

            return instance as ISqliteStorageSimpleType;
        }
    }

    /// <inheritdoc>
    ///     <cref></cref>
    /// </inheritdoc>
    /// <summary>
    /// Data access layer to store models into Sqlite database,
    /// creates a table
    /// </summary>
    public class SqliteStorage<T> : ISqliteStorage<T>, ISqliteStorageSimpleType, IDisposable where T : class
    {
        private string _dataBaseStoragePath;
        private SqliteConnection _sqliteConnection;
        private string _tableName;
        private Dictionary<string, Type> _modelProperties;
        private List<string> _modelPropertiesNames;
        private List<SqliteTransaction> _transactions;
        private ITypeUtility _typeUtility;
        private Dictionary<Type, (Func<object, string> serializer, Func<string, object> deserializer)> _customTypes;
        private Dictionary<string, (Type type, ISqliteStorageSimpleType sqliteStorage)> _referencedTables;
        private bool _initialized;
        private bool _isReferenceType;
        private Dictionary<string, Func<T, string>> _simplifiedProperties;
        
        // this is used to make ORM thread safe
        private static readonly object _lock = new object();
        
        #region CONSTRUCTORS

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dataBaseStoragePath"></param>
        /// <param name="customTypes"></param>
        /// <param name="isReferenceType"></param>
        public SqliteStorage(string dataBaseStoragePath = null, Dictionary<Type, (Func<object, string> serializer, Func<string, object> deserializer)> customTypes = null, bool isReferenceType = false)
        {
            // sets if table is referenced type
            _isReferenceType = isReferenceType;
            
            // set database storgae path
            _dataBaseStoragePath = dataBaseStoragePath ?? SqliteStorageConfiguration.DefaultDataBaseStoragePath;
            
            // initialize custom type dictionary
            _customTypes = customTypes ?? new Dictionary<Type, (Func<object, string> serializer, Func<string, object> deserializer)>();
            
            // set table name
            _tableName = SqliteStorageConfiguration.TableName(typeof(T));
            
            // create a type utility instance
            _typeUtility = new TypeUtility();
            
            // initialize transactions list
            _transactions = new List<SqliteTransaction>();
            
            // referenced tables
            _referencedTables = new Dictionary<string, (Type type, ISqliteStorageSimpleType sqliteStorage)>();
            
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
                DataTypes.DataTypeToSqliteTypeDictionary.TryAdd(type, DataTypes.SqliteTypes.Text);
            };
    
            // create properties dictionary and handle reference properties
            HandlePropertiesAndReferenceTypes();

            // connect to database
            Connect();
            
            // create table
            CreateTable();

            // set flag to initialized
            _initialized = true;
        }

        /// <summary>
        /// Handle properties array and filters out reference properties
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void HandlePropertiesAndReferenceTypes()
        {
            // set model properties
            _modelProperties = _typeUtility.TypeToDictionary(typeof(T));
            var modelPropertiesClone = new Dictionary<string, Type>(_modelProperties);
            _simplifiedProperties = new Dictionary<string, Func<T, string>>();
            
            foreach (var (propertyName, propertyType) in _modelProperties)
            {
                if (DataTypes.DataTypeToSqliteTypeDictionary.ContainsKey(propertyType))
                {
                    _simplifiedProperties.Add(propertyName, (instance) => DataConverterUponQuery(_typeUtility.GetPropertyValue(propertyName, instance, typeof(T)), propertyType).ToString());
                }
                
                // Gets Sqlite type, best matching the propertyType
                if (DataTypes.DataTypeToSqliteTypeDictionary.Any(x => propertyType.GetGenericTypeDefinition() == x.Key))
                {
                    // remove from property from system or atomic types
                    modelPropertiesClone.Remove(propertyName);
                    
                    // create generic instance of self
                    var nestedTable = SqliteStorageFactory.SqliteStorageGeneric(propertyType);
                    
                    // add generic instance to list
                    _referencedTables.Add(propertyName, (propertyType, nestedTable));
                }
                else
                {
                    throw new Exception("Type is not supported, sorry!");
                }
            }

            if (_referencedTables.Any())
            {
                _simplifiedProperties.Add(SqliteStorageConfiguration.PrimaryKeyColumnName(typeof(T)), instance => _typeUtility.HashObjectRandomly(instance));
            }

            // set the actual to clone
            _modelProperties = modelPropertiesClone;
            
            // set model properties names
            _modelPropertiesNames = _modelProperties.Keys.ToList();
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
                Cache = SqliteCacheMode.Private,       
            };
            
            lock (_lock)
            {
                _sqliteConnection = new SqliteConnection(connectionString.ToString());
                _sqliteConnection.Open();
            }
        }

        /// <summary>
        /// Creates table given model type
        /// </summary>
        public void CreateTable()
        {
            var schemaList = _modelProperties.ToList();

            // if there is a reference table, then add primary key to it
            if (_referencedTables.Any())
            {
                schemaList.Add(new KeyValuePair<string, Type>(SqliteStorageConfiguration.PrimaryKeyColumnName(typeof(T)), typeof(string)));
            }

            // if this instance is a reference class, then add foriegn key to it as well
            if (_isReferenceType)
            {
                schemaList.Add(new KeyValuePair<string, Type>(SqliteStorageConfiguration.ForeignKeyColumnName(typeof(T)), typeof(string)));
            }
            
            var schema = string.Join(',', schemaList.Select(x => $"`{x.Key}` {DataTypes.DataTypeToSqliteTypeDictionary[x.Value]}"));

            var commandText = $@"
                    CREATE TABLE IF NOT EXISTS
                        '{_tableName}' ('{SqliteStorageConfiguration.IdColumnName}' INTEGER PRIMARY KEY AUTOINCREMENT, {schema} );
                    ";

            CreateAndExecuteNonQueryCommand(commandText);
        }

        #endregion

        #region ForwardingInstanceMethods

        public void Add(object obj) => Add(obj as T);
        public void AddAll(IEnumerable<object> objects, string foreignKey = null) => AddAll(objects as IEnumerable<T>, foreignKey);
        public object Find(Func<object, bool> filter) => Find(filter as Func<T, bool>) as IEnumerable<T>;
        public object Find(object model) => Find(model as T);
        object ISqliteStorage<object>.Find(Dictionary<string, object> keyValueDictionary) => Find(keyValueDictionary);
        public IEnumerable<object> FindAll(Func<object, bool> filter) => Find(filter as Func<T, bool>) as IEnumerable<object>;
        IEnumerable<object> ISqliteStorage<object>.FindAll(Dictionary<string, object> keyValueDictionary, int limitCount) => FindAll(keyValueDictionary, limitCount);
        IEnumerable<object> ISqliteStorage<object>.FindAll(int limitCount) => FindAll(limitCount);
        public IEnumerable<object> FindAll(object model) => FindAll(model as T);
        //public void Delete(object model) => Delete(model as T);
        //public void Delete(Func<object, bool> filter) => Delete(filter as Func<T, bool>);
        //public void Delete(List<object> models) => Delete(models as List<T>);
        
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
            var hashcode = string.Empty;

            // if there is a reference table, then add primary key to it
            if (_referencedTables.Any())
            {
                hashcode = _typeUtility.HashObjectRandomly(obj);
            }

            var propertiesSchema = FieldsToStatementClause(_simplifiedProperties.Keys);
            var propertiesValues = ValuesToStatementClause(_simplifiedProperties.Values.Select(x => x(obj)));
            
            var commandText = $@"""
                    INSERT INTO {_tableName}
                        ({propertiesSchema})
                    VALUES
                        ({propertiesValues});
                    """;

            // add parent object
            CreateAndExecuteNonQueryCommand(commandText);
            
            // add nested object, i.e. foreach nested referenced property do a add all
            foreach (var (propertyName, (propertyType, sqliteStorage)) in _referencedTables)
            {
                if (_typeUtility.GetPropertyValue(propertyName, obj, typeof(T)) is IEnumerable<object> value)
                {
                    sqliteStorage.AddAll(value, foreignKey: hashcode);    
                }
            }
        }


        /// <summary>
        /// Stores list of models into database
        /// </summary>
        /// <param name="objects"></param>
        /// <param name="foreignKey"></param>
        public void AddAll(IEnumerable<T> objects, string foreignKey = null)
        {
            var schema = _modelPropertiesNames;
            
            // if there is a reference table, then add primary key to it
            if (_isReferenceType)
            {
                schema.Add(SqliteStorageConfiguration.ForeignKeyColumnName(typeof(T)));
            }

            string FormatValues(T obj)
            {
                var tokens = _simplifiedProperties.Values.Select(x => x(obj)).ToList();

                // this is the foriegn key
                if (foreignKey != null)
                {
                    tokens.Add(foreignKey);
                }
                      
                // add nested object, i.e. foreach nested referenced property do a add all
                foreach (var (propertyName, (propertyType, sqliteStorage)) in _referencedTables)
                {
                    if (_typeUtility.GetPropertyValue(propertyName, obj, typeof(T)) is IEnumerable<object> value)
                    {
                        sqliteStorage.AddAll(value, foreignKey: _typeUtility.HashObjectRandomly(obj));    
                    }
                }

                return ValuesToStatementClause(tokens);
            }

            var propertiesSchema = FieldsToStatementClause(_simplifiedProperties.Keys);
            var propertiesValues = string.Join(',', objects.Select(obj => $"( {FormatValues(obj)}) "));

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
        public IEnumerable<T> FindAll(int limitCount = SqliteStorageConfiguration.LimitCount)
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
        public IEnumerable<T> FindAll(Dictionary<string, object> keyValueDictionary, int limitCount = SqliteStorageConfiguration.LimitCount)
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


            var hashcode = _referencedTables.Any() ? reader[SqliteStorageConfiguration.PrimaryKeyColumnName(typeof(T))].ToString() : null;
            
            // build up nested enumerable properties
            foreach (var (propertyName, (propertyType, sqliteStorage)) in _referencedTables)
            {
                if (_typeUtility.GetPropertyValue(propertyName, obj, typeof(T)) is IEnumerable<object> value)
                {
                    sqliteStorage.FindAll(hashcode);
                }
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
                    command.CommandText = commandText.Trim('"');;
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
        /// Converts list of fields to statement clause
        /// </summary>
        /// <param name="fields"></param>
        /// <returns></returns>
        private static string FieldsToStatementClause(IEnumerable<string> fields)
        {
            return string.Join(',', fields.Select(x => $"`{x}`"));
        }
        
        /// <summary>
        /// Converts list of values to statement clause
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        private static string ValuesToStatementClause(IEnumerable<string> values)
        {
            return string.Join(',', values.Select(x => $"'{x}'"));
        }

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
