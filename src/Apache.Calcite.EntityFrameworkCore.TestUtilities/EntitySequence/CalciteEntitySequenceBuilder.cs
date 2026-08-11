namespace Apache.Calcite.EntityFrameworkCore.TestUtilities
{

    public class CalciteEntitySequenceBuilder
    {

        readonly ICalciteMutableEntitySequence _metadata;

        /// <summary>
        /// Creates a new builder for the given <see cref="ICalciteEntitySequence" />.
        /// </summary>
        /// <param name="metadata">The <see cref="ICalciteMutableEntitySequence" /> to configure.</param>
        public CalciteEntitySequenceBuilder(ICalciteMutableEntitySequence metadata)
        {
            _metadata = metadata;
        }

        /// <summary>
        ///     The sequence.
        /// </summary>
        public virtual ICalciteMutableEntitySequence Metadata => _metadata;

        /// <summary>
        /// Sets the <see cref="ICalciteMutableEntitySequence" /> to increment by the given amount when generating each next value.
        /// </summary>
        /// <param name="increment">The amount to increment between values.</param>
        /// <returns>The same builder so that multiple calls can be chained.</returns>
        public virtual CalciteEntitySequenceBuilder IncrementsBy(int increment)
        {
            Metadata.IncrementBy = increment;
            return this;
        }

    }

}
