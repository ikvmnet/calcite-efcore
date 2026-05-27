using System;
using System.Collections.Generic;
using System.Reflection;

namespace Apache.Calcite.EntityFrameworkCore.Core.Reflection
{

    /// <summary>
    /// Cached <see cref="MethodInfo"/> references for <see cref="EqualityComparer{T}"/> and <see cref="IEquatable{T}"/>
    /// used when emitting dynamic record types.
    /// </summary>
    static class EqualityComparerMethods
    {

        /// <summary>Open-generic <c>IEquatable&lt;T&gt;.Equals(T)</c>.</summary>
        internal static readonly MethodInfo IEquatableEquals =
            typeof(IEquatable<>).GetMethod(nameof(IEquatable<object>.Equals))!;

        static readonly Type OpenEqualityComparer = typeof(EqualityComparer<>);

        /// <summary>
        /// Returns the <c>EqualityComparer&lt;<paramref name="type"/>&gt;.Default</c> getter.
        /// </summary>
        internal static MethodInfo Default(Type type) =>
            OpenEqualityComparer.MakeGenericType(type)
                .GetProperty(nameof(EqualityComparer<int>.Default))!.GetGetMethod()!;

        /// <summary>
        /// Returns <c>EqualityComparer&lt;<paramref name="type"/>&gt;.Equals(<paramref name="type"/>, <paramref name="type"/>)</c>.
        /// </summary>
        internal static MethodInfo Equals(Type type) =>
            OpenEqualityComparer.MakeGenericType(type)
                .GetMethod(nameof(EqualityComparer<int>.Equals), [type, type])!;

    }

}
