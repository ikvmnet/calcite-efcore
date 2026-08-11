using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities
{

    public interface ICalciteReadOnlyEntitySequence : IReadOnlyAnnotatable
    {

        /// <summary>
        /// Gets the model in which this sequence is defined.
        /// </summary>
        IReadOnlyModel Model { get; }

        /// <summary>
        /// Gets the name of the sequence.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the entity type of the sequence.
        /// </summary>
        IReadOnlyEntityType EntityType { get; }

        /// <summary>
        /// Gets the value of the entity's primary key that identifies the row representing this sequence.
        /// </summary>
        object? KeyValue { get; }

        /// <summary>
        /// Gets the property of the entity type that is mapped to the sequence, or <see langword="null" /> if the sequence is not mapped to any property.
        /// </summary>
        IReadOnlyProperty? ValueProperty { get; }

        /// <summary>
        /// Gets the amount incremented to obtain each new value in the sequence.
        /// </summary>
        int IncrementBy { get; }

    }

}
