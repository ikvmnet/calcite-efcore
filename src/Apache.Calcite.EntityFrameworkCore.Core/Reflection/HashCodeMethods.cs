using System;
using System.Reflection;

namespace Apache.Calcite.EntityFrameworkCore.Core.Reflection
{

    /// <summary>
    /// Cached <see cref="MethodInfo"/> references for <see cref="HashCode"/> used when emitting dynamic record types.
    /// </summary>
    static class HashCodeMethods
    {

        /// <summary>Open-generic <c>HashCode.Add&lt;T&gt;(T)</c>.</summary>
        internal static readonly MethodInfo Add =
            typeof(HashCode).GetMethod(nameof(HashCode.Add), 1, [Type.MakeGenericMethodParameter(0)])!;

        /// <summary><c>HashCode.ToHashCode()</c>.</summary>
        internal static readonly MethodInfo ToHashCode =
            typeof(HashCode).GetMethod(nameof(HashCode.ToHashCode))!;

    }

}
