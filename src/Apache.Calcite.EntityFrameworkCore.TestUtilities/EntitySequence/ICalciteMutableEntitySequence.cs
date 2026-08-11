using Microsoft.EntityFrameworkCore.Metadata;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities
{

    public interface ICalciteMutableEntitySequence : ICalciteReadOnlyEntitySequence, IMutableAnnotatable
    {

        /// <summary>
        /// Gets the <see cref="IMutableModel" /> in which this sequence is defined.
        /// </summary>
        new IMutableModel Model { get; }

        /// <summary>
        /// Gets the <see cref="IReadOnlyEntityType"/> that holds the sequence.
        /// </summary>
        new IReadOnlyEntityType EntityType { get; set; }

        /// <summary>
        /// Gets or sets the value of the entity's primary key that identifies the row representing this sequence.
        /// </summary>
        new object? KeyValue { get; set; }

        /// <summary>
        /// Gets or sets the value property associated with this instance.
        /// </summary>
        new IReadOnlyProperty ValueProperty { get; set; }

        /// <summary>
        /// Gets the amount incremented to obtain each new value in the sequence.
        /// </summary>
        new int IncrementBy { get; set; }

    }

}
