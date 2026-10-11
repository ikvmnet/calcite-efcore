using Apache.Calcite.EntityFrameworkCore.FunctionalTests.TestUtilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.JsonQuery;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query
{

    public class JsonQueryCalciteFixture : JsonQueryRelationalFixture
    {

        protected override ITestStoreFactory TestStoreFactory => CalciteTestStoreFactory.Instance;

        /// <inheritdoc />
        /// <remarks>
        /// A collection of collections is a nested primitive collection, which the relational model validator
        /// rejects, so the model ignores them the way the SQLite derivation does.
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<JsonEntityAllTypes>(b =>
            {
                b.Ignore(e => e.TestInt64CollectionCollection);
                b.Ignore(e => e.TestDoubleCollectionCollection);
                b.Ignore(e => e.TestSingleCollectionCollection);
                b.Ignore(e => e.TestBooleanCollectionCollection);
                b.Ignore(e => e.TestCharacterCollectionCollection);
                b.Ignore(e => e.TestDefaultStringCollectionCollection);
                b.Ignore(e => e.TestMaxLengthStringCollectionCollection);
                b.Ignore(e => e.TestInt16CollectionCollection);
                b.Ignore(e => e.TestInt32CollectionCollection);
                b.Ignore(e => e.TestNullableEnumWithIntConverterCollectionCollection);
                b.Ignore(e => e.TestNullableInt32CollectionCollection);
                b.Ignore(e => e.TestNullableEnumCollectionCollection);

                b.OwnsOne(
                    e => e.Reference, b =>
                    {
                        b.Ignore(e => e.TestInt64CollectionCollection);
                        b.Ignore(e => e.TestDoubleCollectionCollection);
                        b.Ignore(e => e.TestSingleCollectionCollection);
                        b.Ignore(e => e.TestBooleanCollectionCollection);
                        b.Ignore(e => e.TestCharacterCollectionCollection);
                        b.Ignore(e => e.TestDefaultStringCollectionCollection);
                        b.Ignore(e => e.TestMaxLengthStringCollectionCollection);
                        b.Ignore(e => e.TestInt16CollectionCollection);
                        b.Ignore(e => e.TestInt32CollectionCollection);
                        b.Ignore(e => e.TestNullableEnumWithIntConverterCollectionCollection);
                        b.Ignore(e => e.TestNullableInt32CollectionCollection);
                        b.Ignore(e => e.TestNullableEnumCollectionCollection);
                    });
                b.OwnsMany(
                    e => e.Collection, b =>
                    {
                        b.Ignore(e => e.TestInt64CollectionCollection);
                        b.Ignore(e => e.TestDoubleCollectionCollection);
                        b.Ignore(e => e.TestSingleCollectionCollection);
                        b.Ignore(e => e.TestBooleanCollectionCollection);
                        b.Ignore(e => e.TestCharacterCollectionCollection);
                        b.Ignore(e => e.TestDefaultStringCollectionCollection);
                        b.Ignore(e => e.TestMaxLengthStringCollectionCollection);
                        b.Ignore(e => e.TestInt16CollectionCollection);
                        b.Ignore(e => e.TestInt32CollectionCollection);
                        b.Ignore(e => e.TestNullableEnumWithIntConverterCollectionCollection);
                        b.Ignore(e => e.TestNullableInt32CollectionCollection);
                        b.Ignore(e => e.TestNullableEnumCollectionCollection);
                    });
            });
        }

    }

}
