using System;

using Apache.Calcite.EntityFrameworkCore.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Apache.Calcite.EntityFrameworkCore.ValueGeneration
{

    /// <summary>
    /// Calcite-specific <see cref="IValueGeneratorSelector"/>. Calcite has no store-generated keys
    /// and no way to read one back, so a plain numeric <see cref="ValueGenerated.OnAdd"/> key is
    /// refused with guidance rather than silently misbehaving; <see cref="Guid"/> keys generate
    /// client side.
    /// </summary>
    public class CalciteValueGeneratorSelector : RelationalValueGeneratorSelector
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="CalciteValueGeneratorSelector"/> class.
        /// </summary>
        /// <param name="dependencies">The base selector dependencies.</param>
        public CalciteValueGeneratorSelector(ValueGeneratorSelectorDependencies dependencies) :
            base(dependencies)
        {

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
                    $"Configure an explicit client-side value generator or set '{nameof(ValueGenerated.Never)}'.");
            }

            return base.FindForType(property, typeBase, clrType);
        }

    }

}
