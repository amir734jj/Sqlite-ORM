# Sqlite-ORM
Simple Sqlite ORM, easy and intuitive to use. This ORM follows code-first design, but you can use database-first design as long as database schema matches type conversions. Code does support complex types and custom serializer and de-serilizer for custom types. Only dependency is:  [`Microsoft.Data.SQLite`](https://github.com/aspnet/Microsoft.Data.Sqlite)

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
