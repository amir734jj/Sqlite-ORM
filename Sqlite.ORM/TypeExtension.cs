using System;

namespace Sqlite.ORM
{
    /// <summary>
    /// Extension to type class
    /// </summary>
    internal static class TypeExtension
    {
        /// <summary>
        /// Checks if type is a system type of class defined type
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static bool IsSystemType(this Type x) => x.Namespace == "System";

        /// <summary>
        /// Creates raw object using activator
        /// </summary>
        /// <returns></returns>
        public static object CreateRawObjectFromType(this Type x) => Activator.CreateInstance(x);
    }
}