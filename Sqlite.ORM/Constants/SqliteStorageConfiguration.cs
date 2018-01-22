using System;
using System.Collections.Generic;
using System.IO;

namespace Sqlite.ORM.Constants
{
    /// <summary>
    /// Configurations for ORM
    /// </summary>
    public static class SqliteStorageConfiguration
    {
        public static readonly string NewLine = Environment.NewLine;
        public const int LimitCount = 100;
        public const string IdColumnName = "_Id_";
        public static readonly Func<Type, string> PrimaryKeyColumnName = x => $"Primary{x.Name}{IdColumnName}";
        public static readonly Func<Type, string> ForeignKeyColumnName = x => $"Foreign{x.Name}{IdColumnName}";
        public static readonly Func<Type, string> TableName = x => $"DATA_{x.Name}_TABLE";
        
        private const string DefaultDatabaseName = "db.sqlite";
        private static readonly string DefaultDatabaseFolderPath = Environment.CurrentDirectory;
        public static readonly string DefaultDataBaseStoragePath = Path.Combine(DefaultDatabaseFolderPath, DefaultDatabaseName);
    }
}