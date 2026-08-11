using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Metadata;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities;

/// <summary>
/// Annotation names for the entity-sequence HiLo machinery. Test infrastructure only — the
/// provider deliberately exposes no key-generation strategy; see the value generation notes in
/// TODO.md.
/// </summary>
public static class CalciteEntitySequenceAnnotationNames
{

    /// <summary>
    /// The annotation prefix, matching the provider's.
    /// </summary>
    public const string Prefix = "calcite:";

    /// <summary>
    /// Annotation holding the model's entity sequences.
    /// </summary>
    public const string EntitySequences = Prefix + nameof(EntitySequences);

    /// <summary>
    /// Annotation naming the sequence a model or property generates values from.
    /// </summary>
    public const string EntitySequenceName = Prefix + nameof(EntitySequenceName);

    /// <summary>
    /// Annotation holding the CLR type of the default sequence backing entity.
    /// </summary>
    public const string DefaultEntitySequenceEntityType = Prefix + nameof(DefaultEntitySequenceEntityType);

    /// <summary>
    /// Annotation naming the property that identifies a sequence row on the backing entity.
    /// </summary>
    public const string DefaultEntitySequenceNameProperty = Prefix + nameof(DefaultEntitySequenceNameProperty);

    /// <summary>
    /// Annotation naming the property that holds the sequence value on the backing entity.
    /// </summary>
    public const string DefaultEntitySequenceValueProperty = Prefix + nameof(DefaultEntitySequenceValueProperty);

}

/// <summary>
/// The entity-sequence fluent and metadata surface, relocated from the provider: these are
/// test-infrastructure strategies, not part of the provider's API.
/// </summary>
public static class CalciteEntitySequenceExtensions
{

    // ── model metadata ───────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the name of the sequence to use for generating values for properties of entity types in this model.
    /// </summary>
    public static string? GetEntitySequenceName(this IReadOnlyModel model)
    {
        return (string?)model[CalciteEntitySequenceAnnotationNames.EntitySequenceName];
    }

    /// <summary>
    /// Gets the configuration source for the model-level entity sequence name.
    /// </summary>
    public static ConfigurationSource? GetEntitySequenceNameConfigurationSource(this IConventionModel model)
    {
        return model.FindAnnotation(CalciteEntitySequenceAnnotationNames.EntitySequenceName)?.GetConfigurationSource();
    }

    /// <summary>
    /// Sets the entity sequence name annotation for the specified model.
    /// </summary>
    public static void SetEntitySequenceName(this IMutableModel model, string? name)
    {
        model.SetOrRemoveAnnotation(CalciteEntitySequenceAnnotationNames.EntitySequenceName, name);
    }

    /// <summary>
    /// Sets the entity sequence name annotation for the specified model.
    /// </summary>
    [return: NotNullIfNotNull(nameof(name))]
    public static string? SetEntitySequenceName(this IConventionModel model, string? name, bool fromDataAnnotation = false)
    {
        return (string?)model.SetOrRemoveAnnotation(CalciteEntitySequenceAnnotationNames.EntitySequenceName, name, fromDataAnnotation)?.Value;
    }

    /// <summary>
    /// Finds the entity sequence with the given name in the model.
    /// </summary>
    public static ICalciteEntitySequence? FindEntitySequence(this IReadOnlyModel model, string name)
    {
        return CalciteEntitySequence.FindEntitySequence(model, name);
    }

    /// <summary>
    /// Gets the CLR type of the entity that backs default per-entity HiLo sequences for this model, if configured.
    /// </summary>
    public static Type? GetDefaultEntitySequenceEntityType(this IReadOnlyModel model)
    {
        return (Type?)model[CalciteEntitySequenceAnnotationNames.DefaultEntitySequenceEntityType];
    }

    /// <summary>
    /// Sets the CLR type of the entity that backs default per-entity HiLo sequences for this model.
    /// </summary>
    public static void SetDefaultEntitySequenceEntityType(this IMutableModel model, Type? type)
    {
        model.SetOrRemoveAnnotation(CalciteEntitySequenceAnnotationNames.DefaultEntitySequenceEntityType, type);
    }

    /// <summary>
    /// Gets the name of the property on the default sequence backing entity that identifies a sequence row.
    /// </summary>
    public static string? GetDefaultEntitySequenceNameProperty(this IReadOnlyModel model)
    {
        return (string?)model[CalciteEntitySequenceAnnotationNames.DefaultEntitySequenceNameProperty];
    }

    /// <summary>
    /// Sets the name of the property on the default sequence backing entity that identifies a sequence row.
    /// </summary>
    public static void SetDefaultEntitySequenceNameProperty(this IMutableModel model, string? name)
    {
        model.SetOrRemoveAnnotation(CalciteEntitySequenceAnnotationNames.DefaultEntitySequenceNameProperty, name);
    }

    /// <summary>
    /// Gets the name of the property on the default sequence backing entity that holds the sequence value.
    /// </summary>
    public static string? GetDefaultEntitySequenceValueProperty(this IReadOnlyModel model)
    {
        return (string?)model[CalciteEntitySequenceAnnotationNames.DefaultEntitySequenceValueProperty];
    }

    /// <summary>
    /// Sets the name of the property on the default sequence backing entity that holds the sequence value.
    /// </summary>
    public static void SetDefaultEntitySequenceValueProperty(this IMutableModel model, string? name)
    {
        model.SetOrRemoveAnnotation(CalciteEntitySequenceAnnotationNames.DefaultEntitySequenceValueProperty, name);
    }

    // ── property metadata ────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the name of the sequence to use for value generation.
    /// </summary>
    public static string? GetEntitySequenceName(this IReadOnlyProperty property)
    {
        return (string?)property[CalciteEntitySequenceAnnotationNames.EntitySequenceName];
    }

    /// <summary>
    /// Sets the name of the entity sequence associated with the specified property.
    /// </summary>
    public static void SetEntitySequenceName(this IMutableProperty property, string name)
    {
        property.SetOrRemoveAnnotation(CalciteEntitySequenceAnnotationNames.EntitySequenceName, name);
    }

    /// <summary>
    /// Sets the name of the entity sequence associated with the specified property.
    /// </summary>
    public static string? SetEntitySequenceName(this IConventionProperty property, string? name, bool fromDataAnnotation = false)
    {
        return (string?)property.SetOrRemoveAnnotation(CalciteEntitySequenceAnnotationNames.EntitySequenceName, name, fromDataAnnotation)?.Value;
    }

    /// <summary>
    /// Gets the configuration source for the entity sequence name annotation applied to the specified property, if any.
    /// </summary>
    public static ConfigurationSource? GetEntitySequenceNameConfigurationSource(this IConventionProperty property)
    {
        return property.FindAnnotation(CalciteEntitySequenceAnnotationNames.EntitySequenceName)?.GetConfigurationSource();
    }

    /// <summary>
    /// Finds the entity sequence associated with the specified property, if one is defined on the
    /// property or the model.
    /// </summary>
    public static ICalciteEntitySequence? FindEntitySequence(this IReadOnlyProperty property)
    {
        var model = property.DeclaringType.Model;
        var sequenceName = property.GetEntitySequenceName() ?? model.GetEntitySequenceName();
        if (sequenceName is null)
            return null;

        return model.FindEntitySequence(sequenceName);
    }

    // ── fluent surface ───────────────────────────────────────────────────────────

    /// <summary>
    /// Adds or updates the entity sequence with the given name on the model.
    /// </summary>
    public static CalciteEntitySequence EntitySequence(this IMutableModel model, string name, IReadOnlyEntityType entityType, IReadOnlyProperty valueProperty, ConfigurationSource configurationSource)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(entityType);

        var sequence = (CalciteEntitySequence?)CalciteEntitySequence.FindEntitySequence(model, name);
        if (sequence != null)
        {
            sequence.UpdateConfigurationSource(configurationSource);
            return sequence;
        }

        return CalciteEntitySequence.AddEntitySequence(model, name, entityType, valueProperty, configurationSource);
    }

    /// <summary>
    /// Configures the model to use a sequence-based hi-lo pattern to generate values for key
    /// properties marked as <see cref="ValueGenerated.OnAdd"/>. All entities share the implicit
    /// default sequence unless they specify their own.
    /// </summary>
    public static ModelBuilder UseHiLoEntitySequence(this ModelBuilder modelBuilder)
    {
        return UseHiLoEntitySequence(modelBuilder, CalciteValueGenerationStrategyConvention.DefaultSequenceName);
    }

    /// <summary>
    /// Configures the model to use a sequence-based hi-lo pattern to generate values for key
    /// properties marked as <see cref="ValueGenerated.OnAdd"/>.
    /// </summary>
    public static ModelBuilder UseHiLoEntitySequence(this ModelBuilder modelBuilder, string name)
    {
        var model = modelBuilder.Model;
        model.SetValueGenerationStrategy(CalciteValueGenerationStrategy.EntitySequenceHiLo);
        model.SetEntitySequenceName(name);

        // If the user has not registered a custom default backing entity, register the built-in CalciteSequence
        // entity now and add the default entity sequence eagerly so that runtime value generation can resolve it.
        var backingType = model.GetDefaultEntitySequenceEntityType();
        if (backingType == null)
        {
            modelBuilder.Entity<CalciteSequence>();
            model.SetDefaultEntitySequenceEntityType(typeof(CalciteSequence));
            model.SetDefaultEntitySequenceNameProperty(nameof(CalciteSequence.Name));
            model.SetDefaultEntitySequenceValueProperty(nameof(CalciteSequence.NextValue));
        }

        if (CalciteEntitySequence.FindEntitySequence(model, name) == null)
        {
            var entityClrType = model.GetDefaultEntitySequenceEntityType()!;
            var valueProp = model.GetDefaultEntitySequenceValueProperty()!;
            var entityType = model.FindEntityType(entityClrType)!;
            var sequence = CalciteEntitySequence.AddEntitySequence(
                model,
                name,
                entityType,
                entityType.FindProperty(valueProp)!,
                ConfigurationSource.Explicit);
            sequence.KeyValue = name;
        }

        return modelBuilder;
    }

    /// <summary>
    /// Registers a backing entity to be used as the source of automatically-generated per-entity
    /// HiLo sequences. Each entity with a value-generated primary key receives its own sequence
    /// row in this entity, keyed by entity name.
    /// </summary>
    public static ModelBuilder HasDefaultEntitySequenceEntity<TEntity, TValue>(
        this ModelBuilder modelBuilder,
        Expression<Func<TEntity, string>> namePropertyExpression,
        Expression<Func<TEntity, TValue>> valuePropertyExpression)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(namePropertyExpression);
        ArgumentNullException.ThrowIfNull(valuePropertyExpression);

        var entity = modelBuilder.Entity<TEntity>();
        var nameProperty = entity.Metadata.FindProperty(namePropertyExpression.GetPropertyAccess())
            ?? throw new InvalidOperationException("The name property specified in the expression is not part of the entity.");
        var valueProperty = entity.Metadata.FindProperty(valuePropertyExpression.GetPropertyAccess())
            ?? throw new InvalidOperationException("The value property specified in the expression is not part of the entity.");

        var model = modelBuilder.Model;
        model.SetValueGenerationStrategy(CalciteValueGenerationStrategy.EntitySequenceHiLo);
        model.SetDefaultEntitySequenceEntityType(typeof(TEntity));
        model.SetDefaultEntitySequenceNameProperty(nameProperty.Name);
        model.SetDefaultEntitySequenceValueProperty(valueProperty.Name);
        return modelBuilder;
    }

    /// <summary>
    /// Configures a sequence backed by the given entity's property. The entity is not affected by
    /// the sequence, but the sequence can be used as a value generator for other entities.
    /// </summary>
    public static CalciteEntitySequenceBuilder<TEntity, TValue> HasEntitySequence<TEntity, TValue>(this EntityTypeBuilder<TEntity> entity, string name, Expression<Func<TEntity, TValue>> valuePropertyExpression)
        where TEntity : class
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(valuePropertyExpression);

        var property = entity.Metadata.FindProperty(valuePropertyExpression.GetPropertyAccess());
        if (property == null)
            throw new InvalidOperationException("The property specified in the expression is not part of the entity.");

        return new CalciteEntitySequenceBuilder<TEntity, TValue>(entity.Metadata.Model.EntitySequence(name, entity.Metadata, property, ConfigurationSource.Explicit));
    }

    /// <summary>
    /// Configures the property to use a HiLo value generation strategy based on a sequence entity
    /// with the specified name.
    /// </summary>
    public static PropertyBuilder UseHiLoEntitySequence(this PropertyBuilder propertyBuilder, string name)
    {
        var property = propertyBuilder.Metadata;

        property.SetValueGenerationStrategy(CalciteValueGenerationStrategy.EntitySequenceHiLo);
        property.SetEntitySequenceName(name);

        return propertyBuilder;
    }

    /// <summary>
    /// Returns whether the given name can be set as the sequence entity name on the property.
    /// </summary>
    public static bool CanSetEntitySequenceName(this IConventionPropertyBuilder propertyBuilder, string? name, bool fromDataAnnotation = false)
    {
        return propertyBuilder.CanSetAnnotation(CalciteEntitySequenceAnnotationNames.EntitySequenceName, name, fromDataAnnotation);
    }

}
