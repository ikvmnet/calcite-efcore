using Microsoft.EntityFrameworkCore.Query;

using Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal
{

    public class CalciteMethodCallTranslatorProvider : RelationalMethodCallTranslatorProvider
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        public CalciteMethodCallTranslatorProvider(RelationalMethodCallTranslatorProviderDependencies dependencies) :
            base(dependencies)
        {
            var sqlExpressionFactory = (CalciteSqlExpressionFactory)dependencies.SqlExpressionFactory;
            AddTranslators(
            [
                new CalciteConvertTranslator(sqlExpressionFactory),
                new CalciteMathTranslator(sqlExpressionFactory),
                new CalciteStringMethodTranslator(sqlExpressionFactory),
                new CalciteBoolMethodTranslator(sqlExpressionFactory),
                new CalciteByteMethodTranslator(sqlExpressionFactory),
                new CalciteSByteMethodTranslator(sqlExpressionFactory),
                new CalciteInt16MethodTranslator(sqlExpressionFactory),
                new CalciteUInt16MethodTranslator(sqlExpressionFactory),
                new CalciteInt32MethodTranslator(sqlExpressionFactory),
                new CalciteUInt32MethodTranslator(sqlExpressionFactory),
                new CalciteInt64MethodTranslator(sqlExpressionFactory),
                new CalciteUInt64MethodTranslator(sqlExpressionFactory),
                new CalciteSingleMethodTranslator(sqlExpressionFactory),
                new CalciteDoubleMethodTranslator(sqlExpressionFactory),
                new CalciteDecimalMethodTranslator(sqlExpressionFactory),
                new CalciteCharMethodTranslator(sqlExpressionFactory),
            ]);
        }

    }

}
