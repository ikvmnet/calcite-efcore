using Apache.Calcite.EntityFrameworkCore.Metadata;
using Apache.Calcite.EntityFrameworkCore.Metadata.Internal;
using Apache.Calcite.EntityFrameworkCore.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using System;
using System.Linq.Expressions;

namespace Apache.Calcite.EntityFrameworkCore.Extensions
{

    public static class CalciteModelBuilderExtensions
    {

        /// <summary>
        /// Configures the default value generation strategy for key properties marked as <see cref="ValueGenerated.OnAdd"/>.
        /// </summary>
        /// <param name="modelBuilder"></param>
        /// <param name="valueGenerationStrategy"></param>
        /// <param name="fromDataAnnotation"></param>
        /// <returns></returns>
        public static IConventionModelBuilder? HasValueGenerationStrategy(this IConventionModelBuilder modelBuilder, CalciteValueGenerationStrategy? valueGenerationStrategy, bool fromDataAnnotation = false)
        {
            if (modelBuilder.CanSetValueGenerationStrategy(valueGenerationStrategy, fromDataAnnotation))
            {
                modelBuilder.Metadata.SetValueGenerationStrategy(valueGenerationStrategy, fromDataAnnotation);
                return modelBuilder;
            }

            return null;
        }

        /// <summary>
        /// Returns a value indicating whether the given value can be set as the default value generation strategy.
        /// </summary>
        /// <param name="modelBuilder"></param>
        /// <param name="valueGenerationStrategy"></param>
        /// <param name="fromDataAnnotation"></param>
        /// <returns></returns>
        public static bool CanSetValueGenerationStrategy(this IConventionModelBuilder modelBuilder, CalciteValueGenerationStrategy? valueGenerationStrategy, bool fromDataAnnotation = false)
        {
            return modelBuilder.CanSetAnnotation(CalciteAnnotationNames.ValueGenerationStrategy, valueGenerationStrategy, fromDataAnnotation);
        }

    }

}
