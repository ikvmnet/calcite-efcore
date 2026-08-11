using System.Diagnostics.CodeAnalysis;

using org.apache.calcite.rex;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rex
{

    /// <summary>
    /// Provides translation mappings from Calcite <see cref="org.apache.calcite.sql.SqlOperator"/> instances
    /// to <see cref="SqlOperatorTranslator"/> delegates.
    /// </summary>
    public interface ISqlOperatorTranslationProvider
    {

        /// <summary>
        /// Returns the <see cref="SqlOperatorTranslator"/> registered for <paramref name="call"/>'s operator.
        /// </summary>
        /// <param name="call">The <see cref="RexCall"/> whose operator to look up.</param>
        /// <param name="translator">
        /// When this method returns, contains the translator if one is registered; otherwise, <see langword="null"/>.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if a translator is registered for the call's operator; otherwise, <see langword="false"/>.
        /// </returns>
        bool TryGet(RexCall call, [NotNullWhen(true)] out SqlOperatorTranslator? translator);

    }

}
