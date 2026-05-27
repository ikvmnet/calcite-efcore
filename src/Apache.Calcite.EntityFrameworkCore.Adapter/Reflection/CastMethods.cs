using System;
using System.Reflection;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Reflection
{

    /// <summary>
    /// Cached <see cref="MethodInfo"/> references for type-conversion methods used by
    /// <c>CAST</c> translation in the rex-to-LINQ translator.
    /// </summary>
    /// <remarks>
    /// Only methods that EF Core providers can translate to SQL are included here.
    /// Methods that are not provider-translatable (e.g. <c>ToString(string format)</c>,
    /// <c>DateTime.Parse</c>, <c>Convert.ToInt32(string)</c>) are intentionally absent;
    /// the corresponding <c>TranslateCast*</c> overrides throw <see cref="NotImplementedException"/>
    /// until a provider-level translation path exists for them.
    /// </remarks>
    internal static class CastMethods
    {

        // object.ToString()  — translated by SqlServer/SQLite ObjectToStringTranslator to CAST/CONVERT
        internal static readonly MethodInfo ObjectToString =
            typeof(object).GetMethod(nameof(object.ToString), Type.EmptyTypes)!;

    }

}
