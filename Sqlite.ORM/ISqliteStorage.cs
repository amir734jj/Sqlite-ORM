using System.Collections.Generic;

namespace Sqlite.ORM
{
    public interface ISqliteStorage<T>
    {
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
    }
}