using System;
using System.Linq.Expressions;

using org.apache.calcite.rex;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rex
{

    /// <summary>
    /// Translates Calcite <see cref="RexNode"/> expressions into CLR <see cref="Expression"/> trees
    /// suitable for use in LINQ <c>Where</c> and <c>Select</c> clauses.
    /// </summary>
    /// <remarks>
    /// Implement this interface to provide custom translation logic for Rex nodes. The default
    /// implementation is <see cref="RexToLinqTranslator"/>, which handles standard SQL operators,
    /// functions, and expression kinds. Pass your implementation via
    /// <see cref="Infrastructure.CalciteDbContextOptionsBuilder"/> extensions to customize the adapter's
    /// behavior at the DbContext level.
    /// </remarks>
    public interface IRexToLinqTranslator
    {

        /// <summary>
        /// Translates a <see cref="RexNode"/> into a CLR <see cref="Expression"/> within the given context.
        /// </summary>
        /// <param name="rex">The Calcite expression to translate.</param>
        /// <param name="context">
        /// The translation context containing input parameters, correlations, and dynamic parameter mappings.
        /// </param>
        /// <returns>A CLR <see cref="Expression"/> representing the translated logic.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when the <paramref name="rex"/> node kind is not supported by this translator.
        /// </exception>
        Expression Translate(RexNode rex, EfCoreTranslationContext context);

        /// <summary>
        /// Returns the CLR output type that <paramref name="rex"/> will produce under <paramref name="context"/>,
        /// without building a full expression tree.
        /// </summary>
        /// <param name="rex">The Calcite expression whose type to resolve.</param>
        /// <param name="context">
        /// The translation context containing input parameters, correlations, and dynamic parameter mappings.
        /// </param>
        /// <returns>The CLR <see cref="Type"/> that the translated expression will produce.</returns>
        /// <exception cref="NotSupportedException">
        /// Thrown when the <paramref name="rex"/> node kind is not supported by this translator.
        /// </exception>
        Type ResolveType(RexNode rex, EfCoreTranslationContext context);

    }

}
