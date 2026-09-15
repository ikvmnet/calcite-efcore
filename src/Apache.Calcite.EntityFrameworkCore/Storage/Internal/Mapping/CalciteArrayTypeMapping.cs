using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;

using Apache.Calcite.EntityFrameworkCore.Utilities;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping;

/// <summary>
/// Maps a CLR collection onto a Calcite <c>ARRAY</c> column.
/// </summary>
/// <remarks>
/// Calcite has a native array type, so a primitive collection is stored as one rather than as the
/// JSON string every other relational provider falls back to. The store type is the element's own
/// store type with <c>ARRAY</c> appended, which is the postfix form Calcite's DDL parser accepts
/// (<c>VARCHAR ARRAY</c>; <c>ARRAY&lt;VARCHAR&gt;</c> is rejected).
///
/// <para>The collection itself does not convert. <c>Apache.Calcite.Data</c> reads an <c>ARRAY</c>
/// column through <see cref="DbDataReader.GetFieldValue{T}"/> into whatever collection the caller
/// names, and accepts a CLR array or list as a parameter value, so the mapping names the model's
/// own collection type and lets the reader and the parameter binder do the work. Its
/// <em>elements</em> can convert, where the element's own mapping says so, and that conversion is
/// applied here because nothing upstream walks into a collection to apply it.</para>
/// </remarks>
public class CalciteArrayTypeMapping : RelationalTypeMapping, ICalciteTypeMapping
{

    /// <summary>
    /// The store type suffixes Calcite writes a collection type with, longest first so that a name
    /// is matched against the more specific suffix before the less specific one.
    /// </summary>
    static readonly string[] CollectionSuffixes = [" MULTISET", " ARRAY"];

    /// <summary>
    /// Returns the element's store type name for a collection store type name, or
    /// <see langword="null"/> when the name does not describe a collection.
    /// </summary>
    /// <param name="storeTypeName"></param>
    /// <returns></returns>
    public static string? GetElementStoreTypeName(string? storeTypeName)
    {
        if (storeTypeName is null)
            return null;

        var trimmed = storeTypeName.TrimEnd();
        foreach (var suffix in CollectionSuffixes)
            if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return trimmed[..^suffix.Length].TrimEnd();

        return null;
    }

    /// <summary>
    /// Returns the store type name for a collection of <paramref name="elementStoreTypeName"/>.
    /// </summary>
    /// <param name="elementStoreTypeName"></param>
    /// <returns></returns>
    public static string GetCollectionStoreTypeName(string elementStoreTypeName)
    {
        return elementStoreTypeName + " ARRAY";
    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="storeType">The full store type name, e.g. <c>VARCHAR ARRAY</c>.</param>
    /// <param name="collectionType">The CLR collection type the model uses.</param>
    /// <param name="elementMapping">The mapping for one element.</param>
    /// <param name="comparer">The structural comparer for the collection.</param>
    /// <param name="jsonValueReaderWriter">The reader/writer used where the collection lands inside a JSON document.</param>
    public CalciteArrayTypeMapping(string storeType, Type collectionType, RelationalTypeMapping elementMapping, ValueComparer? comparer = null, JsonValueReaderWriter? jsonValueReaderWriter = null) :
        this(
            new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(
                    collectionType,
                    comparer: Retype(collectionType, comparer),
                    keyComparer: Retype(collectionType, comparer),
                    providerValueComparer: Retype(collectionType, comparer),
                    elementMapping: elementMapping,
                    jsonValueReaderWriter: jsonValueReaderWriter),
                storeType))
    {

    }

    /// <summary>
    /// Returns a collection comparer declared for the collection's own type.
    /// </summary>
    /// <remarks>
    /// EF's collection comparers are declared <c>ValueComparer&lt;object&gt;</c>, which is fine
    /// wherever a converter stands between the model and the store, because the provider value is
    /// then the converted one and gets a comparer of its own. Nothing converts here — the store
    /// holds the collection itself — so EF reads the provider value comparer off the key comparer,
    /// and the model validator requires that one to be declared for exactly the property's type.
    ///
    /// <para>The structural comparer is therefore wrapped rather than replaced. Replacing it with a
    /// comparer built by default for the collection type would compare a
    /// <see cref="System.Collections.Generic.List{T}"/> by reference, which reports two equal
    /// collections as different and a list mutated in place as unchanged.</para>
    /// </remarks>
    /// <param name="collectionType"></param>
    /// <param name="comparer"></param>
    /// <returns></returns>
    static ValueComparer? Retype(Type collectionType, ValueComparer? comparer)
    {
        return comparer is null || comparer.Type == collectionType
            ? comparer
            : (ValueComparer)Activator.CreateInstance(typeof(CalciteArrayValueComparer<>).MakeGenericType(collectionType), comparer)!;
    }

    /// <summary>
    /// Presents a collection comparer as one declared for the collection's own type.
    /// </summary>
    /// <typeparam name="TCollection"></typeparam>
    sealed class CalciteArrayValueComparer<TCollection> : ValueComparer<TCollection>
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="inner"></param>
        public CalciteArrayValueComparer(ValueComparer inner) :
            base(
                (a, b) => inner.Equals(a, b),
                v => inner.GetHashCode(v!),
                v => (TCollection)inner.Snapshot(v!)!)
        {

        }

    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="parameters"></param>
    protected CalciteArrayTypeMapping(RelationalTypeMappingParameters parameters) :
        base(parameters)
    {

    }

    /// <summary>
    /// Gets the mapping for one element of the collection.
    /// </summary>
    public virtual RelationalTypeMapping ElementMapping =>
        (RelationalTypeMapping)ElementTypeMapping!;

    /// <inheritdoc/>
    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
    {
        return new CalciteArrayTypeMapping(parameters);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The store type already carries the element's size in its own name (<c>VARCHAR(50) ARRAY</c>),
    /// so a size applied to the collection would be meaningless and is dropped.
    /// </remarks>
    public override RelationalTypeMapping WithStoreTypeAndSize(string storeType, int? size)
    {
        return base.WithStoreTypeAndSize(storeType, null);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Calcite writes an array literal as <c>ARRAY[a, b]</c>, built from the element mapping so that
    /// each element is quoted and cast exactly as it would be on its own.
    ///
    /// <para>An empty array has no literal form: Calcite's parser requires at least one element, so
    /// <c>ARRAY[]</c> is rejected outright. The empty case is written instead as a multiset built
    /// from a query that selects nothing, cast to the column's type — which is the only spelling of
    /// an empty array the validator accepts.</para>
    /// </remarks>
    protected override string GenerateNonNullSqlLiteral(object value)
    {
        var builder = new StringBuilder();
        var count = 0;

        builder.Append("ARRAY[");

        foreach (var element in (IEnumerable)value)
        {
            if (count++ > 0)
                builder.Append(", ");

            builder.Append(element is null ? "NULL" : ElementMapping.GenerateSqlLiteral(element));
        }

        builder.Append(']');

        return count == 0
            ? $"CAST(MULTISET(SELECT 1 FROM (VALUES (1)) AS t(c) WHERE 1 = 0) AS {StoreType})"
            : builder.ToString();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The driver decides an array parameter's type from the value, so no <see cref="System.Data.DbType"/>
    /// is imposed here; setting one would describe the collection as though it were a scalar.
    ///
    /// <para>What does have to happen is the element conversion. The collection carries no converter
    /// of its own — the store holds the collection — but its elements may: an enum element is stored
    /// as the integer its own mapping converts it to. Nothing upstream walks into a collection to
    /// apply that, so the elements are converted here, on the way to the parameter.</para>
    /// </remarks>
    protected override void ConfigureParameter(DbParameter parameter)
    {
        if (parameter.Value is IEnumerable elements and not string)
            parameter.Value = ToProviderList(elements);
    }

    /// <summary>
    /// Returns the elements as a <see cref="List{T}"/> of the provider's element type, converted by
    /// the element mapping where it has a converter.
    /// </summary>
    /// <remarks>
    /// A list rather than an array, and that is not incidental. The driver decides a parameter's
    /// Calcite type from the value's own type, and it reads an array of an 8-bit element —
    /// <see cref="byte"/> or <see cref="sbyte"/> — as binary: a <c>byte[]</c> bound against a
    /// <c>TINYINT ARRAY</c> column arrives as a <c>ByteString</c> and the insert fails on
    /// <em>Unable to cast ByteString to java.util.List</em>. The same elements in a
    /// <see cref="List{T}"/> bind as the array they are. Normalizing every collection to a list
    /// rather than special-casing the two element types keeps one path for all of them.
    /// </remarks>
    /// <param name="elements"></param>
    /// <returns></returns>
    IList ToProviderList(IEnumerable elements)
    {
        var converter = ElementMapping.Converter;
        var elementType = converter?.ProviderClrType ?? ElementMapping.ClrType;

        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType.MakeNullable()))!;
        foreach (var element in elements)
            list.Add(element is null || converter is null ? element : converter.ConvertToProvider(element));

        return list;
    }

}
