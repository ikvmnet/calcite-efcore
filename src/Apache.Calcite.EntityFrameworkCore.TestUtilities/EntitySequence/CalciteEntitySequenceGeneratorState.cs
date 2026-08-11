using Apache.Calcite.EntityFrameworkCore.Metadata;

using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities
{

    /// <summary>
    /// Tracks the HiLo allocation state for a Calcite entity-backed sequence so that successive value
    /// generators sharing the same sequence and connection coordinate their reservations correctly.
    /// </summary>
    public class CalciteEntitySequenceGeneratorState : HiLoValueGeneratorState
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="CalciteEntitySequenceGeneratorState"/> class for the given entity sequence.
        /// </summary>
        /// <param name="entitySequence">The entity sequence whose state is being tracked.</param>
        public CalciteEntitySequenceGeneratorState(ICalciteEntitySequence entitySequence) :
            base(entitySequence.IncrementBy)
        {
            EntitySequence = entitySequence;
        }

        /// <summary>
        /// Gets the entity sequence whose HiLo allocation state is being tracked by this instance.
        /// </summary>
        public virtual ICalciteEntitySequence EntitySequence { get; }

    }

}
