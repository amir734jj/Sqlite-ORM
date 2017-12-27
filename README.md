# Sqlite-ORM
Simple Sqlite ORM, easy and intuitive to use. 

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

----

Interface:

```C#
void CreateTable();
void StoreModel(T obj);
void StoreModels(IEnumerable<T> objects);
T RetrieveModel(T model);
T RetrieveModel(Dictionary<string, object> keyValueDictionary);
IEnumerable<T> RetrieveModels(Dictionary<string, object> keyValueDictionary, int limitCount = 100);
IEnumerable<T> RetrieveAllModels(int limitCount = 100);
void DeleteModel(T model);
void DeleteModels(List<T> models);
void DeleteModel(Dictionary<string, object> keyValueDictionary);
void DeleteModels(Dictionary<string, object> keyValueDictionary, int limitCount = 100);
void DeleteAllModels();
void DeleteTable();
int GetCountOfModels();
```
