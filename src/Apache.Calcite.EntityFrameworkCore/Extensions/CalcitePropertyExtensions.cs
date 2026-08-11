using System;
using System.Linq;

using Apache.Calcite.EntityFrameworkCore.Metadata;
using Apache.Calcite.EntityFrameworkCore.Metadata.Internal;
using Apache.Calcite.EntityFrameworkCore.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Extensions
{

    /// <summary>
    /// Calcite-specific extension methods for working with <see cref="IReadOnlyProperty"/> instances,
    /// including entity sequence name annotations and value generation strategy resolution.
    /// </summary>
    public static class CalcitePropertyExtensions
    {

        /// <summary>
        /// Gets the configured Calcite value generation strategy for the property, or
        /// <see cref="CalciteValueGenerationStrategy.None"/> if value generation is not applicable.
        /// </summary>
        /// <param name="property">The property to inspect.</param>
        /// <returns>The resolved value generation strategy.</returns>
        public static CalciteValueGenerationStrategy GetValueGenerationStrategy(this IReadOnlyProperty property)
        {
            var annotation = property.FindAnnotation(CalciteAnnotationNames.ValueGenerationStrategy);
            if (annotation != null)
            {
                return (CalciteValueGenerationStrategy?)annotation.Value ?? CalciteValueGenerationStrategy.None;
            }

            var defaultValueGenerationStrategy = GetDefaultValueGenerationStrategy(property);

            if (property.ValueGenerated != ValueGenerated.OnAdd || property.IsForeignKey() || property.TryGetDefaultValue(out _) || property.GetDefaultValueSql() != null || property.GetComputedColumnSql() != null)
            {
                return CalciteValueGenerationStrategy.None;
            }

            return defaultValueGenerationStrategy;
        }

        /// <summary>
        /// Gets the configured Calcite value generation strategy for the property as it applies to the specified
        /// <paramref name="storeObject"/>.
        /// </summary>
        /// <param name="property">The property to inspect.</param>
        /// <param name="storeObject">The target store object.</param>
        /// <returns>The resolved value generation strategy.</returns>
        public static CalciteValueGenerationStrategy GetValueGenerationStrategy(this IReadOnlyProperty property, in StoreObjectIdentifier storeObject)
        {
            return GetValueGenerationStrategy(property, storeObject, null);
        }

        internal static CalciteValueGenerationStrategy GetValueGenerationStrategy(this IReadOnlyProperty property, in StoreObjectIdentifier storeObject, ITypeMappingSource? typeMappingSource)
        {
            var @override = property.FindOverrides(storeObject)?.FindAnnotation(CalciteAnnotationNames.ValueGenerationStrategy);
            if (@override != null)
            {
                return (CalciteValueGenerationStrategy?)@override.Value ?? CalciteValueGenerationStrategy.None;
            }

            var annotation = property.FindAnnotation(CalciteAnnotationNames.ValueGenerationStrategy);
            if (annotation?.Value != null
                && StoreObjectIdentifier.Create(property.DeclaringType, storeObject.StoreObjectType) == storeObject)
            {
                return (CalciteValueGenerationStrategy)annotation.Value;
            }

            var table = storeObject;
            var sharedTableRootProperty = property.FindSharedStoreObjectRootProperty(storeObject);
            if (sharedTableRootProperty != null)
            {
                return CalciteValueGenerationStrategy.None;
            }

            if (property.ValueGenerated != ValueGenerated.OnAdd
                || table.StoreObjectType != StoreObjectType.Table
                || property.TryGetDefaultValue(storeObject, out _)
                || property.GetDefaultValueSql(storeObject) != null
                || property.GetComputedColumnSql(storeObject) != null
                || property.GetContainingForeignKeys()
                    .Any(fk =>
                        !fk.IsBaseLinking()
                        || (StoreObjectIdentifier.Create(fk.PrincipalEntityType, StoreObjectType.Table)
                                is { } principal
                            && fk.GetConstraintName(table, principal) != null)))
            {
                return CalciteValueGenerationStrategy.None;
            }

            var defaultStrategy = GetDefaultValueGenerationStrategy(property, storeObject, typeMappingSource);
            if (defaultStrategy != CalciteValueGenerationStrategy.None)
            {
                if (annotation != null)
                {
                    return (CalciteValueGenerationStrategy?)annotation.Value ?? CalciteValueGenerationStrategy.None;
                }
            }

            return defaultStrategy;
        }

        /// <summary>
        /// Gets the Calcite value generation strategy configured on the given relational property overrides, if any.
        /// </summary>
        /// <param name="overrides">The property overrides to inspect.</param>
        /// <returns>The configured strategy, or <see langword="null"/> if none is set.</returns>
        public static CalciteValueGenerationStrategy? GetValueGenerationStrategy(this IReadOnlyRelationalPropertyOverrides overrides)
        {
            return (CalciteValueGenerationStrategy?)overrides.FindAnnotation(CalciteAnnotationNames.ValueGenerationStrategy)?.Value;
        }

        static CalciteValueGenerationStrategy GetDefaultValueGenerationStrategy(IReadOnlyProperty property)
        {
            var modelStrategy = property.DeclaringType.Model.GetValueGenerationStrategy();
            if (modelStrategy is CalciteValueGenerationStrategy.EntitySequenceHiLo && IsCompatibleWithValueGeneration(property))
            {
                return modelStrategy.Value;
            }

            return CalciteValueGenerationStrategy.None;
        }

        static CalciteValueGenerationStrategy GetDefaultValueGenerationStrategy(IReadOnlyProperty property, in StoreObjectIdentifier storeObject, ITypeMappingSource? typeMappingSource)
        {
            var modelStrategy = property.DeclaringType.Model.GetValueGenerationStrategy();
            if (modelStrategy is CalciteValueGenerationStrategy.EntitySequenceHiLo && IsCompatibleWithValueGeneration(property, storeObject, typeMappingSource))
            {
                return modelStrategy.Value;
            }

            return CalciteValueGenerationStrategy.None;
        }

        /// <summary>
        /// Sets the Calcite value generation strategy on a mutable property.
        /// </summary>
        /// <param name="property">The property to update.</param>
        /// <param name="value">The strategy to apply, or <see langword="null"/> to clear.</param>
        public static void SetValueGenerationStrategy(this IMutableProperty property, CalciteValueGenerationStrategy? value)
        {
            property.SetOrRemoveAnnotation(CalciteAnnotationNames.ValueGenerationStrategy, value);
        }

        /// <summary>
        /// Sets the Calcite value generation strategy on a convention property.
        /// </summary>
        /// <param name="property">The property to update.</param>
        /// <param name="value">The strategy to apply, or <see langword="null"/> to clear.</param>
        /// <param name="fromDataAnnotation">Whether the configuration originates from a data annotation.</param>
        /// <returns>The applied strategy, or <see langword="null"/> if it could not be set.</returns>
        public static CalciteValueGenerationStrategy? SetValueGenerationStrategy(this IConventionProperty property, CalciteValueGenerationStrategy? value, bool fromDataAnnotation = false)
        {
            return (CalciteValueGenerationStrategy?)property.SetOrRemoveAnnotation(CalciteAnnotationNames.ValueGenerationStrategy, value, fromDataAnnotation)?.Value;
        }

        /// <summary>
        /// Sets the Calcite value generation strategy on a mutable property for a specific store object.
        /// </summary>
        /// <param name="property">The property to update.</param>
        /// <param name="value">The strategy to apply, or <see langword="null"/> to clear.</param>
        /// <param name="storeObject">The target store object.</param>
        public static void SetValueGenerationStrategy(this IMutableProperty property, CalciteValueGenerationStrategy? value, in StoreObjectIdentifier storeObject)
        {
            property.GetOrCreateOverrides(storeObject).SetValueGenerationStrategy(value);
        }

        /// <summary>
        /// Sets the Calcite value generation strategy on a convention property for a specific store object.
        /// </summary>
        /// <param name="property">The property to update.</param>
        /// <param name="value">The strategy to apply, or <see langword="null"/> to clear.</param>
        /// <param name="storeObject">The target store object.</param>
        /// <param name="fromDataAnnotation">Whether the configuration originates from a data annotation.</param>
        /// <returns>The applied strategy, or <see langword="null"/> if it could not be set.</returns>
        public static CalciteValueGenerationStrategy? SetValueGenerationStrategy(this IConventionProperty property, CalciteValueGenerationStrategy? value, in StoreObjectIdentifier storeObject, bool fromDataAnnotation = false)
        {
            return property.GetOrCreateOverrides(storeObject, fromDataAnnotation).SetValueGenerationStrategy(value, fromDataAnnotation);
        }

        /// <summary>
        /// Sets the Calcite value generation strategy on the given relational property overrides.
        /// </summary>
        /// <param name="overrides">The property overrides to update.</param>
        /// <param name="value">The strategy to apply, or <see langword="null"/> to clear.</param>
        public static void SetValueGenerationStrategy(this IMutableRelationalPropertyOverrides overrides, CalciteValueGenerationStrategy? value)
        {
            overrides.SetOrRemoveAnnotation(CalciteAnnotationNames.ValueGenerationStrategy, value);
        }

        /// <summary>
        /// Sets the Calcite value generation strategy on the given relational property overrides.
        /// </summary>
        /// <param name="overrides">The property overrides to update.</param>
        /// <param name="value">The strategy to apply, or <see langword="null"/> to clear.</param>
        /// <param name="fromDataAnnotation">Whether the configuration originates from a data annotation.</param>
        /// <returns>The applied strategy, or <see langword="null"/> if it could not be set.</returns>
        public static CalciteValueGenerationStrategy? SetValueGenerationStrategy(this IConventionRelationalPropertyOverrides overrides, CalciteValueGenerationStrategy? value, bool fromDataAnnotation = false)
        {
            return (CalciteValueGenerationStrategy?)overrides.SetOrRemoveAnnotation(CalciteAnnotationNames.ValueGenerationStrategy, value, fromDataAnnotation)?.Value;
        }

        /// <summary>
        /// Gets the configuration source for the value generation strategy annotation on the property.
        /// </summary>
        /// <param name="property">The property to inspect.</param>
        /// <returns>The configuration source, or <see langword="null"/> if the annotation is not set.</returns>
        public static ConfigurationSource? GetValueGenerationStrategyConfigurationSource(this IConventionProperty property)
        {
            return property.FindAnnotation(CalciteAnnotationNames.ValueGenerationStrategy)?.GetConfigurationSource();
        }

        /// <summary>
        /// Gets the configuration source for the value generation strategy annotation on the property
        /// for the specified store object.
        /// </summary>
        /// <param name="property">The property to inspect.</param>
        /// <param name="storeObject">The target store object.</param>
        /// <returns>The configuration source, or <see langword="null"/> if the annotation is not set.</returns>
        public static ConfigurationSource? GetValueGenerationStrategyConfigurationSource(this IConventionProperty property, in StoreObjectIdentifier storeObject)
        {
            return property.FindOverrides(storeObject)?.GetValueGenerationStrategyConfigurationSource();
        }

        /// <summary>
        /// Gets the configuration source for the value generation strategy annotation on the given
        /// relational property overrides.
        /// </summary>
        /// <param name="overrides">The property overrides to inspect.</param>
        /// <returns>The configuration source, or <see langword="null"/> if the annotation is not set.</returns>
        public static ConfigurationSource? GetValueGenerationStrategyConfigurationSource(this IConventionRelationalPropertyOverrides overrides)
        {
            return overrides.FindAnnotation(CalciteAnnotationNames.ValueGenerationStrategy)?.GetConfigurationSource();
        }

        /// <summary>
        /// Determines whether the given property is compatible with Calcite value generation, that is,
        /// whether its CLR type (or provider type) is an integer, enum, or decimal.
        /// </summary>
        /// <param name="property">The property to inspect.</param>
        /// <returns><see langword="true"/> if the property is compatible; otherwise <see langword="false"/>.</returns>
        public static bool IsCompatibleWithValueGeneration(IReadOnlyProperty property)
        {
            var valueConverter = property.GetValueConverter() ?? property.FindTypeMapping()?.Converter;
            var type = (valueConverter?.ProviderClrType ?? property.ClrType).UnwrapNullableType();
            return type.IsInteger() || type.IsEnum || type == typeof(decimal);
        }

        static bool IsCompatibleWithValueGeneration(IReadOnlyProperty property, in StoreObjectIdentifier storeObject, ITypeMappingSource? typeMappingSource)
        {
            if (storeObject.StoreObjectType != StoreObjectType.Table)
                return false;

            var valueConverter = property.GetValueConverter() ?? (property.FindRelationalTypeMapping(storeObject) ?? typeMappingSource?.FindMapping((IProperty)property))?.Converter;
            var type = (valueConverter?.ProviderClrType ?? property.ClrType).UnwrapNullableType();
            return type.IsInteger() || type.IsEnum || type == typeof(decimal);
        }

    }

}
