# Sqlite-ORM
Simple Sqlite ORM, easy and intuitive to use. This ORM follows code-first design, but you can use database-first design as long as database schema matches type conversions. Currently code does not support complex types. Only dependency is:  [`Microsoft.Data.SQLite`](https://github.com/aspnet/Microsoft.Data.Sqlite)

```C#
// initialize new instance
var SqliteStorage = new SqliteStorage<DummyTestClass>();

// create dummy object
var obj = new DummyTestClass();

// store object
SqliteStorage.StoreModel(obj);

// get count of objects
Console.WriteLine(1 == SqliteStorage.GetCountOfModels());

// given object instance with some property values filled, it then finds
// the object from database that matches those filled properties
Console.WriteLine(obj == SqliteStorage.RetrieveModel(obj));
```
