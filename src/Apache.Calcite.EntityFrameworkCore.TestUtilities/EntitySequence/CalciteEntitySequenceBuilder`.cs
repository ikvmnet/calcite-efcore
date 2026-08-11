using System;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities
{

    public class CalciteEntitySequenceBuilder<TEntity, TValue> : CalciteEntitySequenceBuilder
         where TEntity : class
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="sequence"></param>
        public CalciteEntitySequenceBuilder(ICalciteMutableEntitySequence sequence) :
            base(sequence)
        {

        }

        /// <summary>
        /// Sets the value of the entity's primary key that identifies the single row in the entity table
        /// representing this sequence. The row will be created automatically by the database creator if it
        /// does not already exist.
        /// </summary>
        /// <param name="keyValue">The primary key value of the sequence row.</param>
        /// <returns>The same builder so that multiple calls can be chained.</returns>
        public virtual CalciteEntitySequenceBuilder<TEntity, TValue> HasKeyValue(object keyValue)
        {
            ArgumentNullException.ThrowIfNull(keyValue);

            Metadata.KeyValue = keyValue;
            return this;
        }

    }

}
