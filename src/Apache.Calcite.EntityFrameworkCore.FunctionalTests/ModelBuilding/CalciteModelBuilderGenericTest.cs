using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ModelBuilding;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.ModelBuilding;

public partial class CalciteModelBuilderGenericTest : CalciteModelBuilderTestBase
{

    public partial class CalciteGenericNonRelationship(CalciteModelBuilderFixture fixture) : CalciteNonRelationship(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);

    }

    public class CalciteGenericComplexType(CalciteModelBuilderFixture fixture) : CalciteComplexType(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);

    }

    public class CalciteGenericComplexCollection(CalciteModelBuilderFixture fixture) : CalciteComplexCollection(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);

    }

    public partial class CalciteGenericInheritance(CalciteModelBuilderFixture fixture) : CalciteInheritance(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);

    }

    public partial class CalciteGenericOneToMany(CalciteModelBuilderFixture fixture) : CalciteOneToMany(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);

    }

    public partial class CalciteGenericManyToOne(CalciteModelBuilderFixture fixture) : CalciteManyToOne(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);

    }

    public partial class CalciteGenericOneToOne(CalciteModelBuilderFixture fixture) : CalciteOneToOne(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);

    }

    public class CalciteGenericManyToMany(CalciteModelBuilderFixture fixture) : CalciteManyToMany(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);

    }

    public partial class CalciteGenericOwnedTypes(CalciteModelBuilderFixture fixture) : CalciteOwnedTypes(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);

    }

}
