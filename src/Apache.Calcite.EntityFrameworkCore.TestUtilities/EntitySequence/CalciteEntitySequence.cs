using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities
{

    /// <summary>
    /// Calcite implementation of an entity-backed sequence used as a HiLo value generator source.
    /// Stores sequence configuration in model-level annotations and exposes both read-only and mutable
    /// surfaces of the metadata.
    /// </summary>
    public class CalciteEntitySequence : ConventionAnnotatable, ICalciteMutableEntitySequence, ICalciteEntitySequence
    {

        /// <summary>
        /// CLR types supported as the value type of an entity sequence.
        /// </summary>
        public static IReadOnlyCollection<Type> SupportedTypes { get; } = [typeof(byte), typeof(long), typeof(int), typeof(short), typeof(decimal)];

        /// <summary>
        /// The default CLR type used for sequence values when none is explicitly specified.
        /// </summary>
        public static readonly Type DefaultClrType = typeof(long);

        /// <summary>
        /// The default amount by which the sequence is incremented when generating each new value.
        /// </summary>
        public const int DefaultIncrementBy = 1;

        /// <summary>
        /// The default starting value used when seeding a new sequence row.
        /// </summary>
        public const int DefaultStartValue = 1;

        /// <summary>
        /// The default maximum value for the sequence; <see langword="null"/> means unbounded.
        /// </summary>
        public static readonly long? DefaultMaxValue = default;

        /// <summary>
        /// The default minimum value for the sequence; <see langword="null"/> means unbounded.
        /// </summary>
        public static readonly long? DefaultMinValue = default;

        /// <summary>
        /// Indicates whether the sequence is cyclic by default.
        /// </summary>
        public static readonly bool DefaultIsCyclic = default;

        int? _incrementBy;
        ConfigurationSource _configurationSource;
        ConfigurationSource? _incrementByConfigurationSource;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="entityType"></param>
        /// <param name="valueProperty"></param>
        /// <param name="model"></param>
        /// <param name="configurationSource"></param>
        public CalciteEntitySequence(string name, IReadOnlyEntityType entityType, IReadOnlyProperty valueProperty, IReadOnlyModel model, ConfigurationSource configurationSource)
        {
            Model = model;
            EntityType = entityType;
            ValueProperty = valueProperty;
            Name = name;
            _configurationSource = configurationSource;
        }

        /// <summary>
        /// Gets the <see cref="ICalciteEntitySequence"/>s defined on the model.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public static IEnumerable<ICalciteEntitySequence> GetEntitySequences(IReadOnlyModel model)
        {
            return ((Dictionary<string, ICalciteEntitySequence>?)model[CalciteEntitySequenceAnnotationNames.EntitySequences])?.OrderBy(t => t.Key).Select(t => t.Value) ?? [];
        }

        /// <summary>
        /// Finds the <see cref="ICalciteEntitySequence"/> with the given name defined on the model.
        /// </summary>
        /// <param name="model"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static ICalciteEntitySequence? FindEntitySequence(IReadOnlyModel model, string name)
        {
            var sequences = (Dictionary<string, ICalciteEntitySequence>?)model[CalciteEntitySequenceAnnotationNames.EntitySequences];
            if (sequences == null || !sequences.TryGetValue(name, out var sequence))
            {
                return null;
            }

            return sequence;
        }

        /// <summary>
        /// Adds a new <see cref="ICalciteEntitySequence"/> to the model with the given name.
        /// </summary>
        /// <param name="model"></param>
        /// <param name="name"></param>
        /// <param name="entityType"></param>
        /// <param name="valueProperty"></param>
        /// <param name="configurationSource"></param>
        /// <returns></returns>
        public static CalciteEntitySequence AddEntitySequence(IMutableModel model, string name, IReadOnlyEntityType entityType, IReadOnlyProperty valueProperty, ConfigurationSource configurationSource)
        {
            var sequence = new CalciteEntitySequence(name, entityType, valueProperty, model, configurationSource);
            var sequences = (Dictionary<string, ICalciteEntitySequence>?)model[CalciteEntitySequenceAnnotationNames.EntitySequences];
            if (sequences == null)
            {
                sequences = new Dictionary<string, ICalciteEntitySequence>();
                model[CalciteEntitySequenceAnnotationNames.EntitySequences] = sequences;
            }

            sequences.Add(name, sequence);
            return sequence;
        }

        /// <summary>
        /// Renames the specified sequence within the given model to the provided name.
        /// </summary>
        /// <remarks>If the sequence is not found in the model, the method returns null and no changes are
        /// made. The sequence must be mutable to be renamed.</remarks>
        /// <param name="model">The mutable model containing the sequence to be renamed.</param>
        /// <param name="sequence">The sequence to rename. Must be present in the model and mutable.</param>
        /// <param name="name">The new name to assign to the sequence.</param>
        /// <returns>The updated sequence with the new name, or null if the sequence does not exist in the model.</returns>
        public static CalciteEntitySequence? SetName(IMutableModel model, CalciteEntitySequence sequence, string name)
        {
            sequence.EnsureMutable();

            var sequences = (Dictionary<string, ICalciteEntitySequence>?)model[RelationalAnnotationNames.Sequences];
            if (sequences == null || !sequences.ContainsKey(sequence.Name))
                return null;

            sequences.Remove(sequence.Name);
            sequence.Name = name;
            sequences.Add(sequence.Name, sequence);

            return sequence;
        }

        /// <summary>
        /// Removes the sequence with the specified name from the given model.
        /// </summary>
        /// <param name="model">The mutable model from which to remove the sequence.</param>
        /// <param name="name">The name of the sequence to remove.</param>
        /// <returns>The removed sequence if it existed in the model; otherwise, null.</returns>
        public static ICalciteEntitySequence? RemoveSequence(IMutableModel model, string name)
        {
            var sequences = (Dictionary<string, CalciteEntitySequence>?)model[RelationalAnnotationNames.Sequences];
            if (sequences == null || !sequences.TryGetValue(name, out var sequence))
                return null;

            sequences.Remove(sequence.Name);

            return sequence;
        }

        /// <summary>
        /// Gets the model in which this sequence is defined.
        /// </summary>
        public virtual IReadOnlyModel Model { get; }

        /// <summary>
        /// Gets the unique name of the sequence.
        /// </summary>
        public virtual string Name { get; set; }

        /// <summary>
        /// Indicates whether the sequence is read-only.
        /// </summary>
        public override bool IsReadOnly => ((Annotatable)Model).IsReadOnly;

        /// <summary>
        /// Gets the entity type that holds the sequence.
        /// </summary>
        public virtual IReadOnlyEntityType EntityType { get; set; }

        /// <summary>
        /// Gets or sets the value of the entity's primary key that identifies the row representing this sequence.
        /// </summary>
        public virtual object? KeyValue { get; set; }

        /// <summary>
        /// Gets the property that holds the sequence value.
        /// </summary>
        public virtual IReadOnlyProperty ValueProperty { get; set; }

        /// <summary>
        /// Gets the source from which the configuration was obtained.
        /// </summary>
        /// <returns>A value indicating the origin of the configuration for this instance.</returns>
        public virtual ConfigurationSource GetConfigurationSource() => _configurationSource;

        /// <summary>
        /// Updates the configuration source to the highest precedence between the current and the specified source.
        /// </summary>
        /// <remarks>If the specified configuration source has higher precedence than the current source,
        /// the current source is updated. Otherwise, the current source remains unchanged.</remarks>
        /// <param name="configurationSource">The configuration source to compare with the current source. Determines whether the current configuration
        /// source should be updated based on precedence.</param>
        public virtual void UpdateConfigurationSource(ConfigurationSource configurationSource)
        {
            _configurationSource = _configurationSource.Max(configurationSource);
        }

        /// <summary>
        /// Gets or sets the value by which the sequence is incremented each time a new value is generated.
        /// </summary>
        /// <remarks>If not explicitly set, a default increment value is used. Changing this property
        /// affects the step size between generated sequence values.</remarks>
        public virtual int IncrementBy
        {
            get => _incrementBy ?? DefaultIncrementBy;
            set => SetIncrementBy(value, ConfigurationSource.Explicit);
        }

        /// <summary>
        /// Sets the increment value for the sequence and updates the configuration source.
        /// </summary>
        /// <param name="incrementBy"></param>
        /// <param name="configurationSource"></param>
        /// <returns></returns>
        public virtual int? SetIncrementBy(int? incrementBy, ConfigurationSource configurationSource)
        {
            EnsureMutable();

            _incrementBy = incrementBy;

            _incrementByConfigurationSource = incrementBy == null
                ? null
                : configurationSource.Max(_incrementByConfigurationSource);

            return incrementBy;
        }

        /// <summary>
        /// Gets the configuration source for the increment value of the sequence.
        /// </summary>
        /// <returns></returns>
        public virtual ConfigurationSource? GetIncrementByConfigurationSource()
        {
            return _incrementByConfigurationSource;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return ((ISequence)this).ToDebugString(MetadataDebugStringOptions.SingleLineDefault);
        }

        IMutableModel ICalciteMutableEntitySequence.Model
        {
            [DebuggerStepThrough]
            get => (IMutableModel)Model;
        }

        IModel ICalciteEntitySequence.Model
        {
            [DebuggerStepThrough]
            get => (IModel)Model;
        }

    }

}
