using System.Linq.Expressions;
using System.Reflection;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Reflection;

/// <summary>
/// Cached <see cref="MemberInfo"/> references for the EF Core <see cref="DbFunctions"/> members used by
/// the rex-to-LINQ translator.
/// </summary>
internal static class DbFunctionsMethods
{

    /// <summary>
    /// The <c>EF.Functions</c> property access that opens every <see cref="DbFunctionsExtensions"/> call.
    /// EF Core matches these calls on the <see cref="MethodInfo"/> alone, but the receiver still has to be
    /// present in the tree.
    /// </summary>
    internal static readonly Expression Functions =
        Expression.Property(null, typeof(EF).GetProperty(nameof(EF.Functions), BindingFlags.Public | BindingFlags.Static)!);

    // EF.Functions.Like(string, string)
    internal static readonly MethodInfo Like =
        typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.Like), [typeof(DbFunctions), typeof(string), typeof(string)])!;

    // EF.Functions.Like(string, string, string)
    internal static readonly MethodInfo LikeWithEscape =
        typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.Like), [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])!;

}
