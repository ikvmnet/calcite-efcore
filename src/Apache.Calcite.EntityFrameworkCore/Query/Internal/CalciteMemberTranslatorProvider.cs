using Apache.Calcite.EntityFrameworkCore.Query.Internal.Translators;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal
{

    public class CalciteMemberTranslatorProvider : RelationalMemberTranslatorProvider
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="typeMappingSource"></param>
        public CalciteMemberTranslatorProvider(RelationalMemberTranslatorProviderDependencies dependencies, IRelationalTypeMappingSource typeMappingSource) :
            base(dependencies)
        {
            var sqlExpressionFactory = (CalciteSqlExpressionFactory)dependencies.SqlExpressionFactory;
            AddTranslators(
            [
                new CalciteStringMemberTranslator(sqlExpressionFactory),
                new CalciteDateTimeMemberTranslator(sqlExpressionFactory, typeMappingSource),
                new CalciteDateTimeOffsetMemberTranslator(sqlExpressionFactory, typeMappingSource),
                new CalciteDateOnlyMemberTranslator(sqlExpressionFactory, typeMappingSource),
                new CalciteTimeOnlyMemberTranslator(sqlExpressionFactory),
            ]);
        }

    }

}
