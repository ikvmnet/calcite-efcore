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
/// Covers how an <c>ARRAY</c> column's value becomes the collection the model asked for.
/// </summary>
/// <remarks>
/// This is the reading the provider does itself rather than asking the driver for, so it is tested
/// against the value directly: what arrives depends on who produced the row — a .NET array from the
/// driver's own conversion of a <c>java.util.List</c>, or whatever an adapter written in .NET put
/// there — and the column has to read the same either way.
///
/// <para>An element converts as its own type mapping says and no further. A conversion no mapping
/// defines does not happen here merely because the value sits inside a collection, and the element
/// mapping is the same extension point it is anywhere else: giving an element type a mapping is
/// what makes it readable, rather than anything special about being in a list.</para>
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
    /// Reads a value through the mapping's real reader expression, compiled, so the test exercises
    /// exactly what the shaper runs rather than a second path that could drift from it.
    /// </summary>
    static object? Read(Type collectionType, object? value, RelationalTypeMapping? elementMapping = null)
    {
        return Read(Mapping(collectionType, elementMapping), value);
    }

    static object? Read(CalciteArrayTypeMapping mapping, object? value)
    {
        var parameter = Expression.Parameter(typeof(object), "value");
        var reader = Expression.Lambda<Func<object?, object?>>(
            Expression.Convert(mapping.CustomizeDataReaderExpression(parameter), typeof(object)),
            parameter);

        return reader.Compile()(value);
    }

    [Fact]
    public void Reads_a_dotnet_array_and_a_java_backed_list_the_same()
    {
        // the two producers, as the reader hands them over
        Assert.Equal(["a", "b"], (List<string>)Read(typeof(List<string>), new[] { "a", "b" })!);
        Assert.Equal(["a", "b"], (List<string>)Read(typeof(List<string>), new List<string> { "a", "b" })!);
        Assert.Equal(["a", "b"], (List<string>)Read(typeof(List<string>), new object[] { "a", "b" })!);
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
    public void Reads_into_a_concrete_collection_the_driver_has_no_case_for()
    {
        // these are the types the driver cannot build, and the reason the collection allowlist is
        // narrow is the write half rather than this one
        Assert.Equal(["a", "b"], ((ReadOnlyCollection<string>)Read(typeof(ReadOnlyCollection<string>), new[] { "a", "b" })!).ToList());
        Assert.Equal(["a", "b"], ((ObservableCollection<string>)Read(typeof(ObservableCollection<string>), new[] { "a", "b" })!).ToList());
        Assert.Equal(["a", "b"], ((Collection<string>)Read(typeof(Collection<string>), new[] { "a", "b" })!).ToList());
    }

    [Fact]
    public void Reads_into_an_array_and_a_set()
    {
        Assert.Equal(["a", "b"], (string[])Read(typeof(string[]), new[] { "a", "b" })!);
        Assert.Equal([1, 2], (int[])Read(typeof(int[]), new[] { 1, 2 }, CalciteIntTypeMapping.Default)!);

        var set = (HashSet<string>)Read(typeof(HashSet<string>), new[] { "a", "b", "a" })!;
        Assert.Equal(["a", "b"], set.OrderBy(v => v).ToList());
    }

    [Fact]
    public void Does_not_invent_a_conversion_the_element_mapping_does_not_define()
    {
        // a string is not a Guid and nothing in the type mapping says how it would become one, so
        // reading a collection of them is a mapping that does not hold rather than a value to repair
        var ex = Assert.Throws<InvalidCastException>(() => Read(typeof(List<Guid>), new[] { Guid.NewGuid().ToString() }, CalciteGuidTypeMapping.Default));
        Assert.Contains("own type mapping", ex.Message);

        // the same rule the other way: a collection of Guids is not read as a collection of strings
        Assert.Throws<InvalidCastException>(() => Read(typeof(List<string>), new object[] { Guid.NewGuid() }));
    }

    [Fact]
    public void An_element_converts_where_its_own_mapping_says_how()
    {
        // a char is stored as a string and CalciteCharTypeMapping carries the converter that says
        // so, so the element arrives as a char -- through the mapping, not through a rule invented
        // for collections
        Assert.Equal(['a', 'b'], (List<char>)Read(typeof(List<char>), new[] { "a", "b" }, CalciteCharTypeMapping.Default)!);
    }

    [Fact]
    public void An_element_whose_mapping_defines_no_conversion_is_refused()
    {
        // neither mapping carries a converter, and what Calcite's runtime holds is not the type the
        // element asked for. Giving those mappings a converter is what would make them readable
        Assert.Throws<InvalidCastException>(() => Read(typeof(List<DateOnly>), new[] { new DateTime(2020, 1, 2) }, CalciteDateOnlyTypeMapping.Default));
        Assert.Throws<InvalidCastException>(() => Read(typeof(List<TimeOnly>), new[] { new TimeSpan(3, 4, 5) }, CalciteTimeOnlyTypeMapping.Default));
    }

    [Fact]
    public void A_nested_collection_is_read_by_its_own_mapping()
    {
        // an element that is itself a collection recurses rather than being handed over whole, so
        // the inner elements go through their own mapping too
        var inner = new CalciteArrayTypeMapping("VARCHAR ARRAY", typeof(List<string>), CalciteStringTypeMapping.Default);
        var outer = new CalciteArrayTypeMapping("VARCHAR ARRAY ARRAY", typeof(List<List<string>>), inner);

        var read = (List<List<string>>)Read(outer, new object[] { new[] { "a", "b" }, new[] { "c" } })!;

        Assert.Equal(2, read.Count);
        Assert.Equal(["a", "b"], read[0]);
        Assert.Equal(["c"], read[1]);
    }

    [Fact]
    public void Applies_the_element_converter()
    {
        // an enum element is stored as its integer, and the element mapping's own converter is what
        // brings it back
        var elementMapping = (RelationalTypeMapping)CalciteIntTypeMapping.Default.WithComposedConverter(new EnumToNumberConverter<Sample, int>());

        Assert.Equal([Sample.One, Sample.Two], (List<Sample>)Read(typeof(List<Sample>), new[] { 1, 2 }, elementMapping)!);
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
        Assert.Null(Read(typeof(List<string>), null));
        Assert.Null(Read(typeof(List<string>), DBNull.Value));
        Assert.Empty((List<string>)Read(typeof(List<string>), Array.Empty<string>())!);
    }

    [Fact]
    public void A_value_that_is_not_a_sequence_is_refused()
    {
        // a scalar in a column the model calls a collection is a mapping mistake, and saying so
        // beats handing back a collection of one or a null
        var ex = Assert.Throws<InvalidCastException>(() => Read(typeof(List<string>), 42));
        Assert.Contains("ARRAY", ex.Message);

        // a string is enumerable and is not a collection of characters here
        Assert.Throws<InvalidCastException>(() => Read(typeof(List<string>), "ab"));
    }

}
