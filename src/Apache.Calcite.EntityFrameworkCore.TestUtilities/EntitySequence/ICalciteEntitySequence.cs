using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities
{

    public interface ICalciteEntitySequence : ICalciteReadOnlyEntitySequence, IAnnotatable
    {

        /// <summary>
        /// Gets the model in which this sequence is defined.
        /// </summary>
        new IModel Model { get; }

    }

}
