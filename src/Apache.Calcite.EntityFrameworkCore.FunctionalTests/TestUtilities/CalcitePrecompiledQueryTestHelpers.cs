using System.Collections.Generic;
using System.Reflection;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Infrastructure.Internal;

using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities
{

    public class CalcitePrecompiledQueryTestHelpers : PrecompiledQueryTestHelpers
    {

        /// <inheritdoc/>
        public static readonly CalcitePrecompiledQueryTestHelpers Instance = new();

        /// <inheritdoc/>
        /// <remarks>
        /// The driver is named as well as the provider, because a generated shaper reaches it: an
        /// <c>ARRAY</c> column is read through <see cref="CalciteDataReader.GetArray{T}"/>, which is
        /// the driver's own accessor rather than one of <c>DbDataReader</c>'s, so the source EF
        /// generates does not compile without it. An application has it transitively through the
        /// provider; a compilation assembled by hand has to say so.
        /// </remarks>
        protected override IEnumerable<MetadataReference> BuildProviderMetadataReferences()
        {
            yield return MetadataReference.CreateFromFile(typeof(CalciteOptionsExtension).Assembly.Location);
            yield return MetadataReference.CreateFromFile(typeof(CalciteDataReader).Assembly.Location);
            yield return MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location);
        }

    }

}
