using System.Reflection;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using MustBeCleaner.TestKit.Pbt;
using Xunit;

namespace MustBeCleaner.TestKit.Tests;

public class PropertyConventionTests
{
    [Fact]
    public void Tag_follows_the_spec_convention()
    {
        Assert.Equal(
            "Feature: mustbecleaner-storage-cleaner, Property 5: Total byte tanpa overlap",
            PropertyTag.Format(5, "Total byte tanpa overlap"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Tag_rejects_non_positive_property_numbers(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PropertyTag.Format(number, "x"));
    }

    [Fact]
    public void Tag_rejects_blank_titles()
    {
        Assert.Throws<ArgumentException>(() => PropertyTag.Format(1, "  "));
    }

    [Fact]
    public void Attribute_runs_at_least_100_iterations_and_carries_the_tag_as_display_name()
    {
        var attr = new MbcPropertyAttribute(3, "Klasifikasi deterministik");
        Assert.True(attr.MaxTest >= PbtConfig.MinIterations);
        Assert.Equal(100, PbtConfig.MinIterations);
        Assert.Equal("Feature: mustbecleaner-storage-cleaner, Property 3: Klasifikasi deterministik", attr.DisplayName);
    }

    [Fact]
    public void Attribute_cannot_lower_iterations_below_the_minimum()
    {
        var attr = new MbcPropertyAttribute(3, "x") { MaxTest = 5 };
        Assert.Equal(PbtConfig.MinIterations, attr.MaxTest);
    }

    [MbcProperty(99, "Sample: reversing a list twice restores it")]
    public bool Sample_property_uses_the_convention_attribute(int[] xs) =>
        xs.Reverse().Reverse().SequenceEqual(xs);

    [Fact]
    public void Check_helper_runs_at_least_100_iterations()
    {
        var runs = 0;
        PbtConfig.Check(Prop.ForAll<int>(x => { runs++; return true; }));
        Assert.True(runs >= PbtConfig.MinIterations);
    }

    [Fact]
    public void Every_MbcProperty_in_the_test_assemblies_has_a_unique_number()
    {
        var numbers = typeof(PropertyConventionTests).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods())
            .Select(m => m.GetCustomAttribute<MbcPropertyAttribute>())
            .Where(a => a is not null)
            .Select(a => a!.Number)
            .ToList();
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }
}

