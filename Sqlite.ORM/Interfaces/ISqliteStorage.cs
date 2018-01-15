using System;
using System.Collections.Generic;

namespace Sqlite.ORM.Interfaces
{
    public interface ISqliteStorage<T>
    {
        void CreateTable();
        void Add(T obj);
        void AddAll(IEnumerable<T> objects);
        T Find(T model);
        T Find(Dictionary<string, object> keyValueDictionary);
        IEnumerable<T> FindAll(Dictionary<string, object> keyValueDictionary, int limitCount = 100);
        IEnumerable<T> FindAll(int limitCount = 100);
        IEnumerable<T> FindAll(T model);
        void Delete(T model);
        void Delete(List<T> models);
        void Delete(Dictionary<string, object> keyValueDictionary, int limitCount = 100);
        void DeleteAll();
        void DeleteTable();
        int Count();
        void Update(T source, T destination);
        void Update(Dictionary<string, object> source, Dictionary<string, object> destination, int limitCount = 1);
    }
}