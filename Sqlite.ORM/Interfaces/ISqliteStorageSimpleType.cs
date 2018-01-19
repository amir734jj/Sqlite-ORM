using System;
using System.Collections.Generic;

namespace Sqlite.ORM.Interfaces
{
    public interface ISqliteStorageSimpleType
    {
        // create table
        void CreateTable();

        // add method overloads
        void Add(object obj);

        // count of records in table
        int Count();
    }
}