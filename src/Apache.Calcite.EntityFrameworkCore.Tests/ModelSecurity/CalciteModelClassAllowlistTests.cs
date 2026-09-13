using System;
using System.Runtime.CompilerServices;

using Apache.Calcite.EntityFrameworkCore.Core;
using Apache.Calcite.EntityFrameworkCore.Extensions;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.ModelSecurity;

/// <summary>
/// Tests that appending to Calcite's model class-name allowlist preserves what is already there.
/// </summary>
/// <remarks>
/// The allowlist is one comma-separated system property shared by the whole process, so assigning it
/// rather than appending would silently drop patterns the hosting application or another library had
/// set. Every test here saves and restores the property; xunit runs the tests of one class serially,
/// and nothing else in the suite writes this property.
/// </remarks>
public class CalciteModelClassAllowlistTests
{

    /// <summary>
    /// Runs <paramref name="body"/> with the allowlist set to <paramref name="initial"/>, restoring
    /// whatever was there before.
    /// </summary>
    /// <param name="initial">The value to start from, or <see langword="null"/> to start with no property.</param>
    /// <param name="body">The test body, given the value the property holds afterwards.</param>
    static void WithAllowlist(string? initial, Action<Func<string?>> body)
    {
        var saved = java.lang.System.getProperty(CalciteModelClassAllowlist.PropertyName);
        try
        {
            if (initial is null)
                java.lang.System.clearProperty(CalciteModelClassAllowlist.PropertyName);
            else
                java.lang.System.setProperty(CalciteModelClassAllowlist.PropertyName, initial);

            body(() => java.lang.System.getProperty(CalciteModelClassAllowlist.PropertyName));
        }
        finally
        {
            if (saved is null)
                java.lang.System.clearProperty(CalciteModelClassAllowlist.PropertyName);
            else
                java.lang.System.setProperty(CalciteModelClassAllowlist.PropertyName, saved);
        }
    }

    [Fact]
    public void Should_name_our_namespace_from_the_module_initializer()
    {
        // What an application does is load the provider — `UseCalcite` lives in it — and loading it is what
        // runs the module initializer. Force that here rather than depending on some earlier test in this
        // assembly having touched a provider type first; the runtime runs a module constructor at most once,
        // so this observes the real initializer rather than standing in for it.
        RuntimeHelpers.RunModuleConstructor(typeof(CalciteDbContextOptionsBuilderExtensions).Module.ModuleHandle);

        var value = java.lang.System.getProperty(CalciteModelClassAllowlist.PropertyName);

        Assert.NotNull(value);
        Assert.Contains("Apache.Calcite.EntityFrameworkCore.", value!.Split(','));
    }

    [Fact]
    public void Should_append_without_discarding_existing_patterns()
    {
        WithAllowlist("com.example.,org.other.Thing", read =>
        {
            Assert.True(CalciteModelClassAllowlist.Allow("Apache.Calcite.EntityFrameworkCore."));
            Assert.Equal("com.example.,org.other.Thing,Apache.Calcite.EntityFrameworkCore.", read());
        });
    }

    [Fact]
    public void Should_not_append_a_pattern_that_is_already_present()
    {
        WithAllowlist("com.example.,Apache.Calcite.EntityFrameworkCore.,org.other.", read =>
        {
            Assert.False(CalciteModelClassAllowlist.Allow("Apache.Calcite.EntityFrameworkCore."));
            Assert.Equal("com.example.,Apache.Calcite.EntityFrameworkCore.,org.other.", read());
        });
    }

    [Fact]
    public void Should_recognise_an_existing_pattern_despite_surrounding_whitespace()
    {
        // Calcite trims each entry when it parses, so " a. " and "a." are the same pattern
        WithAllowlist("com.example. , Apache.Calcite.EntityFrameworkCore. ", read =>
        {
            Assert.False(CalciteModelClassAllowlist.Allow("Apache.Calcite.EntityFrameworkCore."));
            Assert.Equal("com.example. , Apache.Calcite.EntityFrameworkCore. ", read());
        });
    }

    [Fact]
    public void Should_set_the_property_when_it_is_absent()
    {
        WithAllowlist(null, read =>
        {
            Assert.True(CalciteModelClassAllowlist.Allow("Apache.Calcite.EntityFrameworkCore."));
            Assert.Equal("Apache.Calcite.EntityFrameworkCore.", read());
        });
    }

    [Fact]
    public void Should_set_the_property_when_it_is_empty()
    {
        WithAllowlist("", read =>
        {
            Assert.True(CalciteModelClassAllowlist.Allow("Apache.Calcite.EntityFrameworkCore."));
            Assert.Equal("Apache.Calcite.EntityFrameworkCore.", read());
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a.,b.")]
    public void Should_reject_a_pattern_that_is_empty_or_carries_a_comma(string pattern)
    {
        Assert.Throws<ArgumentException>(() => CalciteModelClassAllowlist.Allow(pattern));
    }

}
