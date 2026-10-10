using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ModelBuilding;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.ModelBuilding;

public class CalciteModelBuilderNonGenericTest : CalciteModelBuilderTestBase
{

    public class CalciteNonGenericNonRelationship(CalciteModelBuilderFixture fixture) : CalciteNonRelationship(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new NonGenericTestModelBuilder(Fixture, configure);

    }

    public class CalciteNonGenericComplexType(CalciteModelBuilderFixture fixture) : CalciteComplexType(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new NonGenericTestModelBuilder(Fixture, configure);

    }

    public class CalciteNonGenericComplexCollection(CalciteModelBuilderFixture fixture) : CalciteComplexCollection(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new NonGenericTestModelBuilder(Fixture, configure);

    }

    public class CalciteNonGenericInheritance(CalciteModelBuilderFixture fixture) : CalciteInheritance(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new NonGenericTestModelBuilder(Fixture, configure);

    }

    public class CalciteNonGenericOneToMany(CalciteModelBuilderFixture fixture) : CalciteOneToMany(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new NonGenericTestModelBuilder(Fixture, configure);

    }

    public class CalciteNonGenericManyToOne(CalciteModelBuilderFixture fixture) : CalciteManyToOne(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new NonGenericTestModelBuilder(Fixture, configure);

    }

    public class CalciteNonGenericOneToOne(CalciteModelBuilderFixture fixture) : CalciteOneToOne(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new NonGenericTestModelBuilder(Fixture, configure);

    }

    public class CalciteNonGenericManyToMany(CalciteModelBuilderFixture fixture) : CalciteManyToMany(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new NonGenericTestModelBuilder(Fixture, configure);

    }

    public class CalciteNonGenericOwnedTypes(CalciteModelBuilderFixture fixture) : CalciteOwnedTypes(fixture)
    {

        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new NonGenericTestModelBuilder(Fixture, configure);

    }

}
