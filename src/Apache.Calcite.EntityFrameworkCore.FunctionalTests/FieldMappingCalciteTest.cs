using System.Threading.Tasks;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests;

public abstract partial class FieldMappingCalciteTest
{
    public abstract class FieldMappingCalciteTestBase<TFixture>(TFixture fixture) : FieldMappingTestBase<TFixture>(fixture)
        where TFixture : FieldMappingCalciteTestBase<TFixture>.FieldMappingCalciteFixtureBase, new()
    {
        /// <inheritdoc />
        protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
            => facade.UseTransaction(transaction.GetDbTransaction());

        public abstract class FieldMappingCalciteFixtureBase : FieldMappingFixtureBase
        {
            /// <inheritdoc />
            protected override ITestStoreFactory TestStoreFactory
                => CalciteTestStoreFactory.Instance;
        }
    }

    public partial class DefaultMappingTest(DefaultMappingTest.DefaultMappingFixture fixture)
        : FieldMappingCalciteTestBase<DefaultMappingTest.DefaultMappingFixture>(fixture)
    {
        public class DefaultMappingFixture : FieldMappingCalciteFixtureBase;
    }

    public partial class EnforceFieldTest(EnforceFieldTest.EnforceFieldFixture fixture)
        : FieldMappingCalciteTestBase<EnforceFieldTest.EnforceFieldFixture>(fixture)
    {
        public class EnforceFieldFixture : FieldMappingCalciteFixtureBase
        {
            /// <inheritdoc />
            protected override string StoreName
                => "FieldMappingEnforceFieldTest";

            /// <inheritdoc />
            protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
            {
                modelBuilder.UsePropertyAccessMode(PropertyAccessMode.Field);
                base.OnModelCreating(modelBuilder, context);
            }
        }
    }

    public partial class EnforceFieldForQueryTest(EnforceFieldForQueryTest.EnforceFieldForQueryFixture fixture)
        : FieldMappingCalciteTestBase<EnforceFieldForQueryTest.EnforceFieldForQueryFixture>(fixture)
    {
        public class EnforceFieldForQueryFixture : FieldMappingCalciteFixtureBase
        {
            /// <inheritdoc />
            protected override string StoreName
                => "FieldMappingFieldQueryTest";

            /// <inheritdoc />
            protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
            {
                modelBuilder.UsePropertyAccessMode(PropertyAccessMode.FieldDuringConstruction);
                base.OnModelCreating(modelBuilder, context);
            }
        }
    }

    public partial class EnforcePropertyTest(EnforcePropertyTest.EnforcePropertyFixture fixture)
        : FieldMappingCalciteTestBase<EnforcePropertyTest.EnforcePropertyFixture>(fixture)
    {
        // Cannot force property access when properties missing getter/setter
        /// <inheritdoc />
        public override void Simple_query_read_only_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_collection_read_only_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_reference_read_only_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Load_collection_read_only_props()
        {
        }

        /// <inheritdoc />
        public override void Load_reference_read_only_props()
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_constant_read_only_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_param_read_only_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Projection_read_only_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override Task Update_read_only_props()
            => Task.CompletedTask;

        /// <inheritdoc />
        public override void Simple_query_read_only_props_with_named_fields(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_collection_read_only_props_with_named_fields(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_reference_read_only_props_with_named_fields(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Load_collection_read_only_props_with_named_fields()
        {
        }

        /// <inheritdoc />
        public override void Load_reference_read_only_props_with_named_fields()
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_constant_read_only_props_with_named_fields(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_param_read_only_props_with_named_fields(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Projection_read_only_props_with_named_fields(bool tracking)
        {
        }

        /// <inheritdoc />
        public override Task Update_read_only_props_with_named_fields()
            => Task.CompletedTask;

        /// <inheritdoc />
        public override void Simple_query_write_only_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_collection_write_only_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_reference_write_only_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Load_collection_write_only_props()
        {
        }

        /// <inheritdoc />
        public override void Load_reference_write_only_props()
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_constant_write_only_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_param_write_only_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Projection_write_only_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override Task Update_write_only_props()
            => Task.CompletedTask;

        /// <inheritdoc />
        public override void Simple_query_write_only_props_with_named_fields(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_collection_write_only_props_with_named_fields(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_reference_write_only_props_with_named_fields(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Load_collection_write_only_props_with_named_fields()
        {
        }

        /// <inheritdoc />
        public override void Load_reference_write_only_props_with_named_fields()
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_constant_write_only_props_with_named_fields(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_param_write_only_props_with_named_fields(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Projection_write_only_props_with_named_fields(bool tracking)
        {
        }

        /// <inheritdoc />
        public override Task Update_write_only_props_with_named_fields()
            => Task.CompletedTask;

        /// <inheritdoc />
        public override void Simple_query_fields_only(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_collection_fields_only(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_reference_fields_only(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Load_collection_fields_only()
        {
        }

        /// <inheritdoc />
        public override void Load_reference_fields_only()
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_constant_fields_only(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_param_fields_only(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Projection_fields_only(bool tracking)
        {
        }

        /// <inheritdoc />
        public override Task Update_fields_only()
            => Task.CompletedTask;

        /// <inheritdoc />
        public override void Simple_query_fields_only_for_navs_too(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_collection_fields_only_for_navs_too(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_reference_fields_only_only_for_navs_too(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Load_collection_fields_only_only_for_navs_too()
        {
        }

        /// <inheritdoc />
        public override void Load_reference_fields_only_only_for_navs_too()
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_constant_fields_only_only_for_navs_too(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_param_fields_only_only_for_navs_too(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Projection_fields_only_only_for_navs_too(bool tracking)
        {
        }

        /// <inheritdoc />
        public override Task Update_fields_only_only_for_navs_too()
            => Task.CompletedTask;

        /// <inheritdoc />
        public override void Include_collection_full_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_reference_full_props(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Load_collection_full_props()
        {
        }

        /// <inheritdoc />
        public override void Load_reference_full_props()
        {
        }

        /// <inheritdoc />
        public override Task Update_full_props()
            => Task.CompletedTask;

        /// <inheritdoc />
        public override void Simple_query_props_with_IReadOnlyCollection(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_collection_props_with_IReadOnlyCollection(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Include_reference_props_with_IReadOnlyCollection(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Load_collection_props_with_IReadOnlyCollection()
        {
        }

        /// <inheritdoc />
        public override void Load_reference_props_with_IReadOnlyCollection()
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_constant_props_with_IReadOnlyCollection(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Query_with_conditional_param_props_with_IReadOnlyCollection(bool tracking)
        {
        }

        /// <inheritdoc />
        public override void Projection_props_with_IReadOnlyCollection(bool tracking)
        {
        }

        /// <inheritdoc />
        public override Task Update_props_with_IReadOnlyCollection()
            => Task.CompletedTask;

        public class EnforcePropertyFixture : FieldMappingCalciteFixtureBase
        {
            /// <inheritdoc />
            protected override string StoreName
                => "FieldMappingEnforcePropertyTest";

            /// <inheritdoc />
            protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
            {
                modelBuilder.UsePropertyAccessMode(PropertyAccessMode.Property);
                base.OnModelCreating(modelBuilder, context);
            }
        }
    }
}
