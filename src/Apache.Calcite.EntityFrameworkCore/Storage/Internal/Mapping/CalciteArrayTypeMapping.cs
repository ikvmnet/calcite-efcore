using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Apache.Calcite.Data;

using Apache.Calcite.EntityFrameworkCore.Utilities;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;
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
/// <para>Reading is done here rather than asked of the driver. The driver returns .NET objects
/// throughout, so the column arrives as a .NET sequence whichever side produced it — Calcite's own
/// runtime holds an array as a <c>java.util.List</c> and the driver converts it, while an adapter
/// written in .NET puts a .NET array there to begin with — and building the model's collection from
/// that sequence is the one reading that works for both. Asking the driver for the collection type
/// instead only works for the first, because the conversion it would run is keyed on the Java type.
/// Writing is the driver's: it types a parameter from the column and binds a sequence as the array
/// the column is.</para>
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

    static readonly MethodInfo GetArrayMethod =
        typeof(CalciteDataReader).GetMethod(nameof(CalciteDataReader.GetArray), 1, [typeof(int)])!;

    static readonly MethodInfo BuildMethod =
        typeof(CalciteArrayTypeMapping).GetMethod(nameof(Build), BindingFlags.Public | BindingFlags.Static)!;

    /// <summary>
    /// Gets the element type the column is read as, which is the element's provider type and is
    /// nullable exactly where the model says an element may be absent.
    /// </summary>
    /// <remarks>
    /// Only the collection says whether an element may be absent — a <c>List&lt;int&gt;</c> and a
    /// <c>List&lt;int?&gt;</c> share an element mapping — and the driver holds the caller to it: an
    /// <c>int[]</c> has nowhere to put a null, so asking for one over a column that has them is
    /// refused rather than quietly widened. Asking for <c>int?[]</c> is how the model says it
    /// expects them.
    /// </remarks>
    public virtual Type ReaderElementType
    {
        get
        {
            var provider = ElementMapping.Converter?.ProviderClrType ?? ElementMapping.ClrType;
            var model = ClrType.TryGetSequenceType() ?? ElementMapping.ClrType;

            return provider.IsValueType && model.IsNullableType() ? provider.MakeNullable() : provider;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The column is read as an array of its elements, which is the driver's own accessor for a
    /// collection: ADO.NET has none, and naming the element type there selects the mapping that
    /// fills it rather than casting whatever the column's default reading produced. That is the only
    /// way to reach a reading that is not the default — a <c>DateOnly</c> over a <c>DATE</c> — and it
    /// is why the elements need no further conversion here.
    /// </remarks>
    public override MethodInfo GetDataReaderMethod()
    {
        return GetArrayMethod.MakeGenericMethod(ReaderElementType);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Everything this needs is written into the expression rather than carried by it. A precompiled
    /// query turns the shaper into C# source, which can emit a type argument and a lambda but not a
    /// constant holding a type mapping, so the types arrive as type arguments and the element's own
    /// conversion — where EF rather than the driver defines one, as for an enum — arrives as the
    /// converter's own expression, inlined.
    /// </remarks>
    /// <param name="expression"></param>
    /// <returns></returns>
    public override Expression CustomizeDataReaderExpression(Expression expression)
    {
        var readerElementType = ReaderElementType;
        var modelElementType = ClrType.TryGetSequenceType() ?? ElementMapping.ClrType;
        var element = Expression.Parameter(readerElementType, "element");

        Expression read = ElementMapping.Converter is { } converter
            ? ReplacingExpressionVisitor.Replace(
                converter.ConvertFromProviderExpression.Parameters[0],
                readerElementType == converter.ProviderClrType ? element : Expression.Convert(element, converter.ProviderClrType),
                converter.ConvertFromProviderExpression.Body)
            : element;

        return Expression.Call(
            BuildMethod.MakeGenericMethod(ClrType, readerElementType, modelElementType),
            expression,
            Expression.Lambda(
                typeof(Func<,>).MakeGenericType(readerElementType, modelElementType),
                read.Type == modelElementType ? read : Expression.Convert(read, modelElementType),
                element));
    }

    /// <summary>
    /// Returns the array the driver read as the collection the model asked for.
    /// </summary>
    /// <remarks>
    /// The driver answers an <c>ARRAY</c> column with an array of its elements or with nothing, and
    /// nothing is a null column, so there is no third case to take apart here. What is left is the
    /// container: the model may want a list, a set or a collection type the driver has no reason to
    /// know about, and that is built here.
    /// </remarks>
    /// <typeparam name="TCollection"></typeparam>
    /// <typeparam name="TSource"></typeparam>
    /// <typeparam name="TElement"></typeparam>
    /// <param name="values"></param>
    /// <param name="readElement"></param>
    /// <returns></returns>
    public static TCollection? Build<TCollection, TSource, TElement>(TSource[]? values, Func<TSource, TElement> readElement)
    {
        if (values is null)
            return default;

        var items = new List<TElement>(values.Length);
        foreach (var value in values)
            items.Add(readElement(value));

        return (TCollection)Fill(typeof(TCollection), typeof(TElement), items);
    }

    /// <summary>
    /// Returns a collection of <paramref name="collectionType"/> holding <paramref name="items"/>.
    /// </summary>
    /// <remarks>
    /// An interface names what the collection has to satisfy rather than what to build, so it is
    /// built as the concrete type EF itself would pick. A concrete type is built as itself, either
    /// from the items or by adding them one at a time, which is what carries the collection types
    /// the driver has no case for.
    /// </remarks>
    /// <param name="collectionType"></param>
    /// <param name="elementType"></param>
    /// <param name="items"></param>
    /// <returns></returns>
    static object Fill<TElement>(Type collectionType, Type elementType, List<TElement> items)
    {
        if (collectionType.IsArray)
            return items.ToArray();

        if (collectionType.IsInstanceOfType(items))
            return items;

        // a concrete collection that wraps or copies a list is built from it, which covers
        // ReadOnlyCollection, ObservableCollection and Collection in one
        if (collectionType.GetConstructor([typeof(IList<>).MakeGenericType(elementType)]) is { } fromList)
            return fromList.Invoke([items]);

        if (collectionType.GetConstructor([typeof(IEnumerable<>).MakeGenericType(elementType)]) is { } fromSequence)
            return fromSequence.Invoke([items]);

        if (collectionType.GetConstructor(Type.EmptyTypes) is { } empty && empty.Invoke(null) is ICollection<TElement> built)
        {
            foreach (var item in items)
                built.Add(item);

            return built;
        }

        throw new InvalidCastException($"Cannot build the collection '{collectionType}' from an ARRAY column: it is neither a list nor constructible from one.");
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
        if (ElementMapping.Converter is { } converter && parameter.Value is IEnumerable elements and not string)
            parameter.Value = ToProviderList(elements, converter);
    }

    /// <summary>
    /// Returns the elements converted to the values the store holds, as a <see cref="List{T}"/> of
    /// the provider's element type.
    /// </summary>
    /// <remarks>
    /// Only the elements convert. The collection carries no converter of its own — the store holds
    /// the collection — but its elements may, and nothing upstream walks into a collection to apply
    /// one, so it is applied here. Where the element has no converter the value is passed through
    /// untouched: the driver types a parameter from the column, so it binds a collection as the
    /// array the column is without help.
    /// </remarks>
    /// <param name="elements"></param>
    /// <param name="converter"></param>
    /// <returns></returns>
    static IList ToProviderList(IEnumerable elements, ValueConverter converter)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(converter.ProviderClrType.MakeNullable()))!;
        foreach (var element in elements)
            list.Add(element is null ? null : converter.ConvertToProvider(element));

        return list;
    }

}
