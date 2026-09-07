using System.Data.Common;

using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal
{

    /// <summary>
    /// Renders the command behind a query as Calcite SQL.
    /// </summary>
    /// <remarks>
    /// The relational default prefixes the command text with one comment line per parameter and
    /// leaves the placeholders in place. Calcite's placeholders are positional <c>?</c> markers with
    /// no name to declare, so such a preamble describes values the statement has no way to pick up:
    /// each placeholder is replaced by its value as a literal instead, leaving SQL that Calcite runs
    /// as it stands.
    /// </remarks>
    public class CalciteQueryStringFactory : RelationalQueryStringFactory
    {

        readonly IRelationalTypeMappingSource _typeMapper;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="typeMapper"></param>
        public CalciteQueryStringFactory(IRelationalTypeMappingSource typeMapper)
        {
            _typeMapper = typeMapper;
        }

        /// <inheritdoc />
        public override string Create(DbCommand command)
        {
            return CalciteParameterInliner.Inline(command, _typeMapper);
        }

    }

}
