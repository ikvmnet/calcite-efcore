using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;

using Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping;

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Types;

/// <summary>
/// Covers how the array the driver reads becomes the collection the model asked for.
/// </summary>
/// <remarks>
/// The driver answers an <c>ARRAY</c> column with an array of its elements or with nothing, and it
/// is the one that decides what an element reads as — naming the element type there selects a
/// mapping rather than casting a result. What is left to the provider is the container, and the
/// conversions EF rather than the driver defines, and that is what these cover. They drive the
/// mapping's real reader expression, compiled, so they exercise what the shaper runs rather than a
/// second path that could drift from it.
/// </remarks>
public class ArrayMaterializationTests
{

    enum Sample
    {
        One = 1,
        Two = 2,
    }

    static CalciteArrayTypeMapping Mapping(Type collectionType, RelationalTypeMapping? elementMapping = null)
    {
        return new CalciteArrayTypeMapping("VARCHAR ARRAY", collectionType, elementMapping ?? CalciteStringTypeMapping.Default);
    }

    /// <summary>
    /// Reads an array through the mapping's reader expression, as the shaper would.
    /// </summary>
    static object? Read(Type collectionType, Array? values, RelationalTypeMapping? elementMapping = null)
    {
        var mapping = Mapping(collectionType, elementMapping);
        var parameter = Expression.Parameter(mapping.ReaderElementType.MakeArrayType(), "values");
        var reader = Expression.Lambda(
            Expression.Convert(mapping.CustomizeDataReaderExpression(parameter), typeof(object)),
            parameter);

        return reader.Compile().DynamicInvoke([values]);
    }

    [Fact]
    public void Reads_into_a_collection_named_by_an_interface()
    {
        // an interface says what the collection must satisfy, so it is built as the concrete type
        foreach (var type in new[] { typeof(IList<string>), typeof(ICollection<string>), typeof(IEnumerable<string>), typeof(IReadOnlyList<string>), typeof(IReadOnlyCollection<string>) })
        {
            var read = Read(type, new[] { "a", "b" });
            Assert.True(type.IsInstanceOfType(read), $"{type} was not satisfied by {read?.GetType()}");
            Assert.Equal(["a", "b"], ((IEnumerable<string>)read!).ToList());
        }
    }

    [Fact]
    public void Reads_into_a_concrete_collection_the_driver_has_no_reason_to_know_about()
    {
        Assert.Equal(["a", "b"], ((ReadOnlyCollection<string>)Read(typeof(ReadOnlyCollection<string>), new[] { "a", "b" })!).ToList());
        Assert.Equal(["a", "b"], ((ObservableCollection<string>)Read(typeof(ObservableCollection<string>), new[] { "a", "b" })!).ToList());
        Assert.Equal(["a", "b"], ((Collection<string>)Read(typeof(Collection<string>), new[] { "a", "b" })!).ToList());
    }

    [Fact]
    public void Reads_into_a_list_an_array_and_a_set()
    {
        Assert.Equal(["a", "b"], (List<string>)Read(typeof(List<string>), new[] { "a", "b" })!);
        Assert.Equal(["a", "b"], (string[])Read(typeof(string[]), new[] { "a", "b" })!);
        Assert.Equal([1, 2], (int[])Read(typeof(int[]), new[] { 1, 2 }, CalciteIntTypeMapping.Default)!);

        var set = (HashSet<string>)Read(typeof(HashSet<string>), new[] { "a", "b", "a" })!;
        Assert.Equal(["a", "b"], set.OrderBy(v => v).ToList());
    }

    [Fact]
    public void Asks_the_driver_for_an_element_type_the_model_can_hold()
    {
        // only the collection says whether an element may be absent, and the driver holds the caller
        // to it: an int[] has nowhere to put a null, so the ask says int?[] where one is allowed
        Assert.Equal(typeof(int), Mapping(typeof(List<int>), CalciteIntTypeMapping.Default).ReaderElementType);
        Assert.Equal(typeof(int?), Mapping(typeof(List<int?>), CalciteIntTypeMapping.Default).ReaderElementType);
        Assert.Equal(typeof(string), Mapping(typeof(List<string>)).ReaderElementType);

        // where EF defines the conversion, the ask is for what the store holds rather than the model
        Assert.Equal(typeof(string), Mapping(typeof(List<char>), CalciteCharTypeMapping.Default).ReaderElementType);
    }

    [Fact]
    public void Applies_a_conversion_EF_defines_rather_than_the_driver()
    {
        // a char is stored as a string, and CalciteCharTypeMapping carries the converter saying so
        Assert.Equal(['a', 'b'], (List<char>)Read(typeof(List<char>), new[] { "a", "b" }, CalciteCharTypeMapping.Default)!);

        // an enum is stored as its integer, and the element mapping's converter brings it back
        var enumMapping = (RelationalTypeMapping)CalciteIntTypeMapping.Default.WithComposedConverter(new EnumToNumberConverter<Sample, int>());
        Assert.Equal([Sample.One, Sample.Two], (List<Sample>)Read(typeof(List<Sample>), new[] { 1, 2 }, enumMapping)!);
    }

    [Fact]
    public void Keeps_order_duplicates_and_nulls()
    {
        Assert.Equal(["b", "a", "b"], (List<string>)Read(typeof(List<string>), new[] { "b", "a", "b" })!);
        Assert.Equal(["a", null, "b"], (List<string?>)Read(typeof(List<string>), new[] { "a", null, "b" })!);
        Assert.Equal([1, null, 2], (List<int?>)Read(typeof(List<int?>), new int?[] { 1, null, 2 }, CalciteIntTypeMapping.Default)!);
    }

    [Fact]
    public void An_absent_collection_is_null_and_an_empty_one_is_empty()
    {
        // the driver answers with an array or with nothing, and nothing is a null column
        Assert.Null(Read(typeof(List<string>), null));
        Assert.Empty((List<string>)Read(typeof(List<string>), Array.Empty<string>())!);
    }

}
