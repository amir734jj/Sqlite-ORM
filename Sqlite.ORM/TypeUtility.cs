using System;
using System.Collections.Generic;
using System.Linq;
using Sqlite.ORM.Interfaces;

namespace Sqlite.ORM
{
    /// <summary>
    /// Type utility class, this a stand-alone class
    /// </summary>
    public class TypeUtility : ITypeUtility
    {
        /// <summary>
        /// Converst type to dictionary of property name/property type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public Dictionary<string, Type> TypeToDictionary(Type type)
        {
            if (type.IsSystemType()) throw new InvalidOperationException("Cannot convert system type to dictionary");
            
            var retVal = new Dictionary<string, Type>();
            foreach (var property in type.GetProperties())
            {
                var propertyType = property.PropertyType;
                
                if (propertyType.IsSystemType()) retVal.Add(property.Name, propertyType);
                else
                {
                    foreach (var (complexName, complexType) in TypeToDictionary(propertyType))
                    {
                        retVal.Add($"{property.Name}.{complexName}", complexType);
                    }
                }
            }

            return retVal;
        }

        /// <summary>
        /// Gets nested property values of object given the following format:
        ///     Parent.Name.FirstName
        /// </summary>
        /// <param name="compoundProperty"></param>
        /// <param name="obj"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public object GetPropertyValue(string compoundProperty, object obj, Type type)
        {
            while (true)
            {
                var parts = compoundProperty.Split('.').ToList();
                var currentPart = parts.FirstOrDefault();
                var info = type.GetProperty(currentPart);

                if (info == null) return null;

                if (compoundProperty.IndexOf('.') > -1)
                {
                    parts.Remove(currentPart);
                    compoundProperty = string.Join('.', parts);
                    obj = info.GetValue(obj, null);
                    type = info.PropertyType;
                }
                else
                {
                    return info.GetValue(obj, null);
                }
            }
        }
        
        /// <summary>
        /// Sets nested property values of object given the following format:
        ///     Parent.Name.FirstName
        /// </summary>
        /// <param name="compoundProperty"></param>
        /// <param name="target"></param>
        /// <param name="value"></param>
        public void SetPropertyValue(string compoundProperty, object target, object value)
        {
            var bits = compoundProperty.Split('.');
            
            foreach (var bit in bits.SkipLast(1)) {
                var propertyToGet = target.GetType().GetProperty(bit);
                target = propertyToGet.GetValue(target, null);
            }
            
            var propertyToSet = target.GetType().GetProperty(bits.Last());
            propertyToSet.SetValue(target, value, null);
        }
        
        /// <summary>
        /// Returns default value given type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public object GetDefaultOfType(Type type)
        {
            var obj = Activator.CreateInstance(type);

            foreach (var propertyInfo in type.GetProperties())
            {
                var propertyValue = propertyInfo.GetValue(obj, null);
                
                if (!propertyInfo.PropertyType.IsSystemType() && propertyValue == null)
                {
                    propertyInfo.SetValue(obj, GetDefaultOfType(propertyInfo.PropertyType), null);
                }
            }

            return obj;
        }
    }
}