using NullGuard;
using System.ComponentModel;
using System.Reflection;

namespace Hspi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal static class EnumHelper
    {
        /// <summary>
        /// Returns the value of the DescriptionAttribute if the specified Enum value has one.
        /// If not, returns the ToString() representation of the Enum value.
        /// </summary>
        /// <param name="value">The Enum to get the description for</param>
        /// <returns></returns>
        public static string GetDescription(System.Enum value)
        {
            FieldInfo fi = value.GetType().GetField(value.ToString());
            DescriptionAttribute[] attributes = (DescriptionAttribute[])fi.GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (attributes.Length > 0)
                return attributes[0].Description;
            else
                return value.ToString();
        }
    }
}