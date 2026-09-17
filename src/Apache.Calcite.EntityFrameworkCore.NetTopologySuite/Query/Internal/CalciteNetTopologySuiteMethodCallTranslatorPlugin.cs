using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Query.Internal;

/// <summary>
/// Adds the geometry method translators to the provider's own.
/// </summary>
public class CalciteNetTopologySuiteMethodCallTranslatorPlugin : IMethodCallTranslatorPlugin
{

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="sqlExpressionFactory"></param>
    /// <param name="typeMappingSource"></param>
    public CalciteNetTopologySuiteMethodCallTranslatorPlugin(ISqlExpressionFactory sqlExpressionFactory, IRelationalTypeMappingSource typeMappingSource)
    {
        Translators = [new CalciteGeometryMethodTranslator(sqlExpressionFactory, typeMappingSource)];
    }

    /// <inheritdoc />
    public virtual IEnumerable<IMethodCallTranslator> Translators { get; }

}
