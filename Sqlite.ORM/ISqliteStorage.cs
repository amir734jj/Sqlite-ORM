using System.Collections.Generic;

namespace Sqlite.ORM
{
    public interface ISqliteStorage<T>
    {
        void StoreModel(T obj);
        void StoreModels(IEnumerable<T> objects);
        T RetrieveModel(Dictionary<string, object> keyValueDictionary);
        List<T> RetrieveModels(Dictionary<string, object> keyValueDictionary, int limitCount = 100);
        List<T> RetrieveAllModels(int limitCount = 100);
        void DeleteModels(Dictionary<string, object> keyValueDictionary);
        void DeleteAllModels();
        int GetCountOfModels();
    }
}