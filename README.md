# Sqlite-ORM
Simple Sqlite ORM

```C#
// initialize new instance
SqliteStorage = new SqliteStorage<DummyTestClass>();

// create dummy object
var obj = new DummyTestClass();

// store object
SqliteStorage.StoreModel(obj);

// get count of objects
Console.WriteLine(0 == SqliteStorage.GetCountOfModels());

// given object instance with some property values filled, it then finds
// the object from database that matches those filled properties
Console.WriteLine(obj == SqliteStorage.RetrieveModel(obj));
```
