using System.Reflection;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Reflection
{

    /// <summary>
    /// Cached <see cref="MethodInfo"/> references for <see cref="string"/> methods used by
    /// the rex-to-LINQ translator.
    /// </summary>
    internal static class StringMethods
    {

        // string.Concat(string, string)
        internal static readonly MethodInfo Concat2 =
            typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!;

        // string.TrimStart()
        internal static readonly MethodInfo TrimStart =
            typeof(string).GetMethod(nameof(string.TrimStart), System.Type.EmptyTypes)!;

        // string.TrimEnd()
        internal static readonly MethodInfo TrimEnd =
            typeof(string).GetMethod(nameof(string.TrimEnd), System.Type.EmptyTypes)!;

        // string.EndsWith(string)
        internal static readonly MethodInfo EndsWith =
            typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;

        // string.StartsWith(string)
        internal static readonly MethodInfo StartsWith =
            typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;

        // string.Contains(string)
        internal static readonly MethodInfo Contains =
            typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    }

}
