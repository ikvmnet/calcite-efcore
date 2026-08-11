using Apache.Calcite.EntityFrameworkCore.Metadata;
using Apache.Calcite.EntityFrameworkCore.Metadata.Internal;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Apache.Calcite.EntityFrameworkCore.Extensions
{

    /// <summary>
    /// Calcite-specific extension methods for <see cref="IReadOnlyModel"/>, providing access to the
    /// model-level value generation strategy.
    /// </summary>
    public static class CalciteModelExtensions
    {

        /// <summary>
        /// Gets the model-level Calcite value generation strategy, if one has been configured.
        /// </summary>
        /// <param name="model">The model to inspect.</param>
        /// <returns>The strategy, or <see langword="null"/> if none has been configured.</returns>
        public static CalciteValueGenerationStrategy? GetValueGenerationStrategy(this IReadOnlyModel model)
        {
            return (CalciteValueGenerationStrategy?)model[CalciteAnnotationNames.ValueGenerationStrategy];
        }

        /// <summary>
        /// Sets the model-level Calcite value generation strategy.
        /// </summary>
        /// <param name="model">The model to update.</param>
        /// <param name="value">The strategy to apply, or <see langword="null"/> to clear.</param>
        public static void SetValueGenerationStrategy(this IMutableModel model, CalciteValueGenerationStrategy? value)
        {
            model.SetOrRemoveAnnotation(CalciteAnnotationNames.ValueGenerationStrategy, value);
        }

        /// <summary>
        /// Sets the model-level Calcite value generation strategy.
        /// </summary>
        /// <param name="model">The model to update.</param>
        /// <param name="value">The strategy to apply, or <see langword="null"/> to clear.</param>
        /// <param name="fromDataAnnotation">Whether the configuration originates from a data annotation.</param>
        /// <returns>The applied strategy, or <see langword="null"/> if it could not be set.</returns>
        public static CalciteValueGenerationStrategy? SetValueGenerationStrategy(this IConventionModel model, CalciteValueGenerationStrategy? value, bool fromDataAnnotation = false)
        {
            return (CalciteValueGenerationStrategy?)model.SetOrRemoveAnnotation(CalciteAnnotationNames.ValueGenerationStrategy, value, fromDataAnnotation)?.Value;
        }

        /// <summary>
        /// Gets the configuration source for the model-level value generation strategy annotation, if any.
        /// </summary>
        /// <param name="model">The model to inspect.</param>
        /// <returns>The configuration source, or <see langword="null"/> if not configured.</returns>
        public static ConfigurationSource? GetValueGenerationStrategyConfigurationSource(this IConventionModel model)
        {
            return model.FindAnnotation(CalciteAnnotationNames.ValueGenerationStrategy)?.GetConfigurationSource();
        }


    }

}
