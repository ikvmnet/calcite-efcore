using System;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities
{

    /// <summary>
    /// Factory contract for creating <see cref="ValueGenerator"/> instances backed by a Calcite
    /// entity sequence for a specific property and CLR value type.
    /// </summary>
    public interface ICalciteSequenceValueGeneratorFactory
    {

        /// <summary>
        /// Attempts to create a value generator that produces values of <paramref name="clrType"/> for the given
        /// <paramref name="property"/> using the supplied entity sequence <paramref name="generatorState"/>.
        /// </summary>
        /// <param name="property">The property for which a value generator is being created.</param>
        /// <param name="clrType">The CLR type the generator must produce values for.</param>
        /// <param name="generatorState">The shared HiLo allocation state for the underlying entity sequence.</param>
        /// <param name="commandLogger">The diagnostics logger used by command execution within the generator.</param>
        /// <returns>A new <see cref="ValueGenerator"/>, or <see langword="null"/> if the type is not supported.</returns>
        ValueGenerator? TryCreate(IProperty property, Type clrType, CalciteEntitySequenceGeneratorState generatorState, IRelationalCommandDiagnosticsLogger commandLogger);

    }

}
