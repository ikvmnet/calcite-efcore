using System;

using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Metadata;
using Apache.Calcite.EntityFrameworkCore.Storage.Internal;
using Apache.Calcite.EntityFrameworkCore.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Apache.Calcite.EntityFrameworkCore.ValueGeneration
{

    /// <summary>
    /// Calcite-specific <see cref="IValueGeneratorSelector"/> that prefers entity-sequence HiLo generators for
    /// properties configured with <see cref="CalciteValueGenerationStrategy.EntitySequenceHiLo"/> and otherwise
    /// falls back to the relational defaults, with special handling for <see cref="Guid"/> properties.
    /// </summary>
    public class CalciteValueGeneratorSelector : RelationalValueGeneratorSelector
    {

        readonly ICalciteSequenceValueGeneratorFactory _sequenceFactory;
        readonly ICalciteConnection _connection;
        readonly IRelationalCommandDiagnosticsLogger _commandLogger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CalciteValueGeneratorSelector"/> class.
        /// </summary>
        /// <param name="dependencies">The base selector dependencies.</param>
        /// <param name="sequenceFactory">The factory used to create entity sequence value generators.</param>
        /// <param name="connection">The Calcite relational connection used to scope cached generator state.</param>
        /// <param name="commandLogger">The diagnostics logger used by command execution within generators.</param>
        public CalciteValueGeneratorSelector(ValueGeneratorSelectorDependencies dependencies, ICalciteSequenceValueGeneratorFactory sequenceFactory, ICalciteConnection connection, IRelationalCommandDiagnosticsLogger commandLogger) :
            base(dependencies)
        {
            _sequenceFactory = sequenceFactory;
            _connection = connection;
            _commandLogger = commandLogger;
        }

        /// <summary>
        /// Gets the Calcite-specific value generator cache used by this selector.
        /// </summary>
        public new virtual ICalciteValueGeneratorCache Cache => (ICalciteValueGeneratorCache)base.Cache;

        /// <inheritdoc />
        [Obsolete("Use TrySelect and throw if needed when the generator is not found.")]
        public override ValueGenerator? Select(IProperty property, ITypeBase typeBase)
        {
            if (TrySelect(property, typeBase, out var valueGenerator))
            {
                return valueGenerator;
            }

            throw new NotSupportedException(CoreStrings.NoValueGenerator(property.Name, property.DeclaringType.DisplayName(), property.ClrType.ShortDisplayName()));
        }

        /// <inheritdoc />
        public override bool TrySelect(IProperty property, ITypeBase typeBase, out ValueGenerator? valueGenerator)
        {
            if (property.GetValueGeneratorFactory() != null || property.GetValueGenerationStrategy() != CalciteValueGenerationStrategy.EntitySequenceHiLo)
            {
                return base.TrySelect(property, typeBase, out valueGenerator);
            }

            var propertyType = property.ClrType.UnwrapNullableType().UnwrapEnumType();

            valueGenerator = _sequenceFactory.TryCreate(
                property,
                propertyType,
                Cache.GetOrAddEntitySequenceState(property, _connection),
                _commandLogger);

            if (valueGenerator != null)
            {
                return true;
            }

            var converter = property.GetTypeMapping().Converter;
            if (converter != null && converter.ProviderClrType != propertyType)
            {
                valueGenerator = _sequenceFactory.TryCreate(
                    property,
                    converter.ProviderClrType,
                    Cache.GetOrAddEntitySequenceState(property, _connection),
                    _commandLogger);

                if (valueGenerator != null)
                {
                    valueGenerator = valueGenerator.WithConverter(converter);
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        protected override ValueGenerator? FindForType(IProperty property, ITypeBase typeBase, Type clrType)
        {
            if (property.ClrType.UnwrapNullableType() == typeof(Guid))
            {
                return property.ValueGenerated == ValueGenerated.Never || property.GetDefaultValueSql() != null
                    ? new TemporaryGuidValueGenerator()
                    : new SequentialGuidValueGenerator();
            }

            if (property.ValueGenerated == ValueGenerated.OnAdd
                && (clrType == typeof(int) || clrType == typeof(long) || clrType == typeof(short)))
            {
                throw new NotSupportedException(
                    $"Property '{property.Name}' on entity '{property.DeclaringType.DisplayName()}' is configured as '{nameof(ValueGenerated.OnAdd)}' for a numeric type, " +
                    $"but the Calcite provider does not support store-generated keys. " +
                    $"Configure an explicit client-side value generator (e.g. UseHiLoEntitySequence) or set '{nameof(ValueGenerated.Never)}'.");
            }

            return base.FindForType(property, typeBase, clrType);
        }

    }

}
