using System;

namespace Apache.Calcite.EntityFrameworkCore
{

    /// <summary>
    /// Provides Calcite-specific SQL functions that don't have direct .NET equivalents.
    /// Methods in this class are translated by the Calcite query provider into SQL function calls.
    /// </summary>
    public static class CalciteFunctions
    {

        /// <summary>
        /// Reverses the characters in a string. Translated to SQL REVERSE() function.
        /// </summary>
        /// <param name="value">The string to reverse.</param>
        /// <returns>The reversed string.</returns>
        /// <remarks>
        /// This method is only supported within LINQ queries and is translated to the SQL REVERSE function.
        /// It cannot be called directly in-memory.
        /// </remarks>
        public static string? Reverse(string? value)
        {
            throw new InvalidOperationException($"{nameof(CalciteFunctions)}.{nameof(Reverse)} is only supported in LINQ queries and will be translated to SQL.");
        }

    }

}
