using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Query.Internal;

/// <summary>
/// Adds the geometry aggregate translators to the provider's own.
/// </summary>
public class CalciteNetTopologySuiteAggregateMethodCallTranslatorPlugin : IAggregateMethodCallTranslatorPlugin
{

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="typeMappingSource"></param>
    public CalciteNetTopologySuiteAggregateMethodCallTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    {
        Translators = [new CalciteGeometryAggregateMethodTranslator(sqlExpressionFactory, typeMappingSource)];
    }

    /// <inheritdoc />
    public virtual IEnumerable<IAggregateMethodCallTranslator> Translators { get; }

}
