using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Query.Internal;

/// <summary>
/// Adds the geometry member translators to the provider's own.
/// </summary>
public class CalciteNetTopologySuiteMemberTranslatorPlugin : IMemberTranslatorPlugin
{

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="typeMappingSource"></param>
    public CalciteNetTopologySuiteMemberTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    {
        Translators = [new CalciteGeometryMemberTranslator(sqlExpressionFactory, typeMappingSource)];
    }

    /// <inheritdoc />
    public virtual IEnumerable<IMemberTranslator> Translators { get; }

}
