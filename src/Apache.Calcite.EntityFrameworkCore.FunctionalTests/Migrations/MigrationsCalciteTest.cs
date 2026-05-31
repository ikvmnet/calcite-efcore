using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;
using Apache.Calcite.EntityFrameworkCore.Scaffolding.Internal;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

using Xunit;
using Xunit.Abstractions;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Migrations;

public class MigrationsCalciteTest : MigrationsTestBase<MigrationsCalciteTest.MigrationsCalciteFixture>
{

    public MigrationsCalciteTest(MigrationsCalciteFixture fixture, ITestOutputHelper testOutputHelper) :
        base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    protected override string NonDefaultCollation => "ISO-8859-1$en_US$tertiary";

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_table_all_settings() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_table_no_key() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_table_with_comments() => Task.CompletedTask;

    public override Task Create_table_with_complex_properties_mapped_to_json()
        => Test(
            builder => { },
            builder =>
            {
                builder.Entity(
                    "Entity", e =>
                    {
                        e.Property<int>("Id").ValueGeneratedOnAdd();
                        e.HasKey("Id");
                        e.Property<string>("Name");

                        e.ComplexProperty<MyJsonComplex>(
                            "ComplexReference", cp =>
                            {
                                cp.ToJson("ComplexReferenceJSON");
                                cp.Property(x => x.Value).HasJsonPropertyName("custom_value");
                                cp.Property(x => x.Date).HasJsonPropertyName("custom_date");
                                cp.Ignore(x => x.NestedCollection);
                                cp.ComplexProperty(
                                    x => x.Nested, np =>
                                    {
                                        np.Property("Foo").HasJsonPropertyName("nested_foo");
                                        np.Property("Bar").HasJsonPropertyName("nested_bar");
                                        np.HasJsonPropertyName("nested_complex");
                                    });
                            });

                        e.ComplexCollection<List<MyJsonComplex>, MyJsonComplex>(
                            "ComplexCollection", cp =>
                            {
                                cp.ToJson("ComplexCollectionJSON");
                                cp.Property(x => x.Value).HasJsonPropertyName("custom_value2");
                                cp.Property(x => x.Date).HasJsonPropertyName("custom_date2");
                                cp.Ignore(x => x.NestedCollection);
                                cp.ComplexProperty(
                                    x => x.Nested, np =>
                                    {
                                        np.Property("Foo").HasJsonPropertyName("nested_foo2");
                                        np.Property("Bar").HasJsonPropertyName("nested_bar2");
                                        np.HasJsonPropertyName("nested_complex2");
                                    });
                            });
                    });
            },
            model =>
            {
                Assert.Equal(2, model.Tables.Count);
                var table = Assert.Single(model.Tables, t => t.Name == "Entity");

                Assert.Collection(
                    table.Columns,
                    c => Assert.Equal("Id", c.Name),
                    c => Assert.Equal("Name", c.Name),
                    c => Assert.Equal("ComplexCollectionJSON", c.Name),
                    c => Assert.Equal("ComplexReferenceJSON", c.Name));
                Assert.Null(table.PrimaryKey); // Calcite does not support primary key constraints
            });

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_table_with_multiline_comments() => Task.CompletedTask;

    [Theory(Skip = "Schema modification not supported")]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public override Task Create_table_with_computed_column(bool? stored) => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_table_with_complex_type_with_required_properties_on_derived_entity_in_TPH() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_table_with_optional_complex_type_with_required_properties() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_table_with_required_primitive_collection() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_table_with_optional_primitive_collection() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_table_add_comment() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_table_add_comment_non_default_schema() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_table_change_comment() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_table_remove_comment() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Convert_json_entities_to_regular_owned() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Convert_regular_owned_entities_to_json() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Drop_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Rename_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Rename_table_with_json_column() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Rename_table_with_primary_key() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Move_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_schema() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_defaultValue_string() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_defaultValue_unspecified() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_defaultValue_datetime() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_defaultValueSql() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_defaultValueSql_unspecified() => Task.CompletedTask;

    [Theory(Skip = "Schema modification not supported")]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public override Task Add_column_with_computedSql(bool? stored) => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_computedSql_unspecified() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_required() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_ansi() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_max_length() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_unbounded_max_length() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_max_length_on_derived() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_fixed_length() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_comment() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_collation() => Task.CompletedTask;

    [Theory(Skip = "Schema modification not supported")]
    [InlineData(false)]
    [InlineData(true)]
    public override Task Add_column_computed_with_collation(bool stored) => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_shared() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_column_with_check_constraint() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_change_type() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_make_required() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_make_required_with_null_data() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_make_required_with_index() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_make_required_with_composite_index() => Task.CompletedTask;

    [Theory(Skip = "Schema modification not supported")]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public override Task Alter_column_make_computed(bool? stored) => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_change_computed() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_change_computed_recreates_indexes() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_change_computed_type() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_make_non_computed() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_add_comment() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_computed_column_add_comment() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_change_comment() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_remove_comment() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_set_collation() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_column_reset_collation() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Drop_column() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Drop_column_primary_key() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Drop_column_computed_and_non_computed_with_dependency() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Rename_column() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_index() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_index_unique() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_index_descending() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_index_descending_mixed() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_index_make_unique() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_index_change_sort_order() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_index_with_filter() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_unique_index_with_filter() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Drop_index() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Drop_json_columns_from_existing_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Rename_index() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Rename_json_column() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_primary_key_int() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_primary_key_string() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_primary_key_with_name() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_primary_key_composite_with_name() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Drop_primary_key_int() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Drop_primary_key_string() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_foreign_key() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_foreign_key_with_name() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Drop_foreign_key() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_unique_constraint() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_unique_constraint_composite_with_name() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Drop_unique_constraint() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_check_constraint_with_name() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_check_constraint() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Drop_check_constraint() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_sequence() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_sequence_long() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_sequence_short() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Create_sequence_all_settings() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_sequence_all_settings() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_sequence_increment_by() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Alter_sequence_restart_with() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Drop_sequence() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Rename_sequence() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Move_sequence() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task InsertDataOperation() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task DeleteDataOperation_simple_key() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task DeleteDataOperation_composite_key() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task UpdateDataOperation_simple_key() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task UpdateDataOperation_composite_key() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task UpdateDataOperation_multiple_columns() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_required_primitive_collection_to_existing_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_required_primitive_collection_with_custom_default_value_to_existing_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_required_primitive_collection_with_custom_converter_to_existing_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_required_primitive_collection_with_custom_converter_and_custom_default_value_to_existing_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_json_columns_to_existing_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_optional_primitive_collection_to_existing_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_required_primitive_collection_with_custom_default_value_sql_to_existing_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_required_primitve_collection_to_existing_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_required_primitve_collection_with_custom_default_value_to_existing_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_required_primitve_collection_with_custom_converter_to_existing_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_required_primitve_collection_with_custom_converter_and_custom_default_value_to_existing_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Add_required_primitve_collection_with_custom_default_value_sql_to_existing_table() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Multiop_drop_table_and_create_the_same_table_in_one_migration() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Multiop_create_table_and_drop_it_in_one_migration() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Multiop_rename_table_and_drop() => Task.CompletedTask;

    [Fact(Skip = "Schema modification not supported")]
    public override Task Multiop_rename_table_and_create_new_table_with_the_old_name() => Task.CompletedTask;

    public class MigrationsCalciteFixture : MigrationsFixtureBase
    {

        public override RelationalTestHelpers TestHelpers => CalciteTestHelpers.Instance;

        protected override string StoreName => nameof(MigrationsCalciteTest);

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
        {
            return base
                .AddServices(serviceCollection)
                .AddScoped<IDatabaseModelFactory, CalciteDatabaseModelFactory>();
        }

    }

}
