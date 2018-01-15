using System;
using System.Collections.Generic;

namespace Sqlite.ORM.Interfaces
{
    public interface ITypeUtility
    {
        void AddLeafType(Type type);
        Dictionary<string, Type> TypeToDictionary(Type type);
        object GetPropertyValue(string name, object obj, Type type);
        void SetPropertyValue(string compoundProperty, object target, object value);
        object GetDefaultOfType(Type type);
    }
}