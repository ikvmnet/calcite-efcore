using System;
using System.Collections.Generic;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public partial class CalciteApiConsistencyTest(CalciteApiConsistencyTest.CalciteApiConsistencyFixture fixture)
    : ApiConsistencyTestBase<CalciteApiConsistencyTest.CalciteApiConsistencyFixture>(fixture)
{

    /// <inheritdoc />
    protected override void AddServices(ServiceCollection serviceCollection)
        => serviceCollection.AddEntityFrameworkCalcite();

    /// <inheritdoc />
    protected override Assembly TargetAssembly
        => typeof(CalciteDbContextOptionsBuilder).Assembly;

    public class CalciteApiConsistencyFixture : ApiConsistencyFixtureBase
    {

        /// <inheritdoc />
        public override HashSet<Type> FluentApiTypes { get; } =
        [
            typeof(CalciteServiceCollectionExtensions),
            typeof(CalciteDbContextOptionsBuilderExtensions),
            typeof(CalciteDbContextOptionsBuilder),
            typeof(CalciteModelBuilderExtensions),
            typeof(CalcitePropertyBuilderExtensions),
        ];

        /// <inheritdoc />
        public override
            Dictionary<Type,
                (Type ReadonlyExtensions,
                Type MutableExtensions,
                Type ConventionExtensions,
                Type ConventionBuilderExtensions,
                Type RuntimeExtensions)> MetadataExtensionTypes
        { get; } = new()
        {
            {
                typeof(IReadOnlyModel), (
                    typeof(CalciteModelExtensions),
                    typeof(CalciteModelExtensions),
                    typeof(CalciteModelExtensions),
                    typeof(CalciteModelBuilderExtensions),
                    null!
                )
            },
            {
                typeof(IReadOnlyProperty), (
                    typeof(CalcitePropertyExtensions),
                    typeof(CalcitePropertyExtensions),
                    typeof(CalcitePropertyExtensions),
                    typeof(CalcitePropertyBuilderExtensions),
                    null!
                )
            },
        };

    }

}
