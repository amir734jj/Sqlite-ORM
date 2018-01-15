# SQLite-ORM
Simple SQLite ORM, easy and intuitive to use. This ORM follows code-first design, but you can use database-first design as long as database schema matches type conversions. The code does support complex types and custom serializer and de-serializer for custom types. The only dependency is:  [`Microsoft.Data.SQLite`](https://github.com/aspnet/Microsoft.Data.Sqlite).

[#Nuget](https://www.nuget.org/packages/Sqlite.ORM/)

```C#
// initialize new instance
var SqliteStorage = new SqliteStorage<DummyTestClass>();

// create dummy object
var obj = new DummyTestClass();

// store object
SqliteStorage.Add(obj);

// get count of objects
Console.WriteLine(1 == SqliteStorage.Count());

// given object instance with some property values filled, it then finds
// the object from database that matches those filled properties
Console.WriteLine(obj == SqliteStorage.Find(obj));
```

Defining custom types with custom serializer and deserializer:
```C#
new SqliteStorage<DummyTestClass>(customTypes: new Dictionary<Type, (Func<object, string> serializer, Func<string, object> deserializer)>()
{
    // add support for custom type
    {typeof(ObjectId), (obj => obj.ToString(), str => ObjectId.Parse(str))}
});
```

Interface:
```C#
// create table
void CreateTable();

// add method overloads
void Add(T obj);
void AddAll(IEnumerable<T> objects);

// find method overloads
T Find(Func<T, bool> filter);
T Find(T model);
T Find(Dictionary<string, object> keyValueDictionary);

// find all method overloads
IEnumerable<T> FindAll(Func<T, bool> filter);
IEnumerable<T> FindAll(Dictionary<string, object> keyValueDictionary, int limitCount = 100);
IEnumerable<T> FindAll(int limitCount = 100);
IEnumerable<T> FindAll(T model);

// find method overloads
void Delete(T model);
void Delete(List<T> models);
void Delete(Dictionary<string, object> keyValueDictionary, int limitCount = 100);

// delete all models or records in table
void DeleteAll();

// delete the whole table schema (be careful)
void DeleteTable();

// update method overloads
void Update(T source, T destination);
void Update(Dictionary<string, object> source, Dictionary<string, object> destination, int limitCount = 1);

// count of records in table
int Count();
```

- - - -

## Notes:
I started this project because I could not find a good ORM for SQL, similar to what [`LiteDB`](https://github.com/mbdavid/LiteDB) does for C# Mongo community. It is not the fastest ORM but it is very simple and intuitive. Please feel free to contribute to this project.


