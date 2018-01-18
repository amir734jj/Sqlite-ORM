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
        private readonly List<Type> _leafTypes;
        
        /// <summary>
        /// Initialize a constructor
        /// </summary>
        public TypeUtility()
        {
            _leafTypes = new List<Type>();    
        }

        /// <summary>
        /// Adds a leaf type to list
        /// </summary>
        public void AddLeafType(Type type)
        {
            _leafTypes.Add(type);
        }
        
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

                if (propertyType.IsSystemType() || _leafTypes.Contains(propertyType))
                {
                    retVal.Add(property.Name, propertyType);
                }
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
        
        /// <summary>
        /// Gets T from IEnumerable<T>
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public Type GetAnyElementType(Type type)
        {
            // Type is Array
            // short-circuit if you expect lots of arrays 
            if (type.IsArray)
                return type.GetElementType();

            // type is IEnumerable<T>;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (IEnumerable<>))
                return type.GetGenericArguments()[0];

            // type implements/extends IEnumerable<T>;
            var enumType = type.GetInterfaces()
                .Where(t => t.IsGenericType && 
                            t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .Select(t => t.GenericTypeArguments[0]).FirstOrDefault();
            return enumType ?? type;
        }
    }
}