using System;
using System.Collections.Generic;

namespace Apache.Calcite.EntityFrameworkCore.Utilities
{

    internal static class TypeExtensions
    {

        public static Type UnwrapNullableType(this Type type)
        {
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        /// <summary>
        /// Returns whether a value of this type can be null.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsNullableType(this Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
        }

        /// <summary>
        /// Returns the nullable form of this type, which for a reference type is itself.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type MakeNullable(this Type type)
        {
            return type.IsNullableType() ? type : typeof(Nullable<>).MakeGenericType(type);
        }

        /// <summary>
        /// Returns the element type of a sequence type, or <see langword="null"/> where the type is
        /// not a sequence.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type? TryGetSequenceType(this Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            foreach (var candidate in type.GetInterfaces())
                if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return candidate.GetGenericArguments()[0];

            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? type.GetGenericArguments()[0]
                : null;
        }

        /// <summary>
        /// Returns the element type of a sequence type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static Type GetSequenceType(this Type type)
        {
            return type.TryGetSequenceType() ?? throw new ArgumentException($"The type '{type}' is not a sequence.", nameof(type));
        }

        public static bool IsInteger(this Type type)
        {
            type = type.UnwrapNullableType();

            return type == typeof(int)
                || type == typeof(long)
                || type == typeof(short)
                || type == typeof(byte)
                || type == typeof(uint)
                || type == typeof(ulong)
                || type == typeof(ushort)
                || type == typeof(sbyte)
                || type == typeof(char);
        }

        public static Type UnwrapEnumType(this Type type)
        {
            return type.IsEnum ? Enum.GetUnderlyingType(type) : type;
        }
    }

}
