using System;
using System.Collections.Generic;

namespace Sqlite.ORM.Constants
{
    public static class DataTypes
    {
        /// <summary>
        /// Sqlite supported types
        /// </summary>
        public enum SqliteTypes
        {
            Text,
            Numeric,
            Integer,
            Real,
            Blob,
            Table
        }

        /// <summary>
        /// Map between C# data types and Sqlite data types, used for creating a database table
        /// </summary>
        public static readonly Dictionary<Type, SqliteTypes> DataTypeToSqliteTypeDictionary = new Dictionary<Type, SqliteTypes>()
        {
            { typeof(int), SqliteTypes.Integer },
            { typeof(long), SqliteTypes.Numeric },
            { typeof(float), SqliteTypes.Real },
            { typeof(double), SqliteTypes.Real },
            { typeof(string), SqliteTypes.Text },
            { typeof(DateTime), SqliteTypes.Text },
            { typeof(char), SqliteTypes.Text },
            { typeof(bool), SqliteTypes.Text },
            { typeof(List<>), SqliteTypes.Table }
        };
    }
}