using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities
{

    /// <summary>
    /// Default implementation of <see cref="ICalciteSequenceValueGeneratorFactory"/> that creates a
    /// <see cref="CalciteEntitySequenceHiLoValueGenerator{TValue}"/> for the supported integral and decimal CLR types.
    /// </summary>
    public class CalciteSequenceValueGeneratorFactory : ICalciteSequenceValueGeneratorFactory
    {

        readonly ICurrentDbContext _currentDbContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="CalciteSequenceValueGeneratorFactory"/> class.
        /// </summary>
        /// <param name="currentDbContext">The current <see cref="DbContext"/> accessor used by generated value generators.</param>
        public CalciteSequenceValueGeneratorFactory(ICurrentDbContext currentDbContext)
        {
            _currentDbContext = currentDbContext;
        }

        /// <inheritdoc />
        public virtual ValueGenerator? TryCreate(IProperty property, Type type, CalciteEntitySequenceGeneratorState generatorState, IRelationalCommandDiagnosticsLogger commandLogger)
        {
            if (type == typeof(long))
            {
                return new CalciteEntitySequenceHiLoValueGenerator<long>(_currentDbContext, generatorState, commandLogger);
            }

            if (type == typeof(int))
            {
                return new CalciteEntitySequenceHiLoValueGenerator<int>(_currentDbContext, generatorState, commandLogger);
            }

            if (type == typeof(decimal))
            {
                return new CalciteEntitySequenceHiLoValueGenerator<decimal>(_currentDbContext, generatorState, commandLogger);
            }

            if (type == typeof(short))
            {
                return new CalciteEntitySequenceHiLoValueGenerator<short>(_currentDbContext, generatorState, commandLogger);
            }

            if (type == typeof(byte))
            {
                return new CalciteEntitySequenceHiLoValueGenerator<byte>(_currentDbContext, generatorState, commandLogger);
            }

            if (type == typeof(char))
            {
                return new CalciteEntitySequenceHiLoValueGenerator<char>(_currentDbContext, generatorState, commandLogger);
            }

            if (type == typeof(ulong))
            {
                return new CalciteEntitySequenceHiLoValueGenerator<ulong>(_currentDbContext, generatorState, commandLogger);
            }

            if (type == typeof(uint))
            {
                return new CalciteEntitySequenceHiLoValueGenerator<uint>(_currentDbContext, generatorState, commandLogger);
            }

            if (type == typeof(ushort))
            {
                return new CalciteEntitySequenceHiLoValueGenerator<ushort>(_currentDbContext, generatorState, commandLogger);
            }

            if (type == typeof(sbyte))
            {
                return new CalciteEntitySequenceHiLoValueGenerator<sbyte>(_currentDbContext, generatorState, commandLogger);
            }

            return null;
        }
    }

}
