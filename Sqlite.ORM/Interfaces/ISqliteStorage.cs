using System;
using System.Collections.Generic;

namespace Sqlite.ORM.Interfaces
{
    public interface ISqliteStorage<T>
    {
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
        void Delete(Func<T, bool> filter);
        void Delete(List<T> models);
        void Delete(Dictionary<string, object> keyValueDictionary, int limitCount = 100);

        // delete all models or records in table
        void DeleteAll();

        // delete the whole table schema (be careful)
        void DeleteTable();

        // update method overloads
        void Update(Func<T, bool> filter, T destination);
        void Update(T source, T destination);
        void Update(Dictionary<string, object> source, Dictionary<string, object> destination, int limitCount = 1);

        // count of records in table
        int Count();
    }
}