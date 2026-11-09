using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace MustBeCleaner.TestKit.Pbt;

/// <summary>Shared FsCheck settings. Design.md: minimum 100 iterations per property.</summary>
public static class PbtConfig
{
    public const int MinIterations = 100;

    public static Config Default { get; } = Config.Default.WithMaxTest(MinIterations);

    /// <summary>Checks a property with the shared configuration; throws on counter-example.</summary>
    public static void Check(Property property) => property.Check(Default);
}

/// <summary>Builds the tag every property test must carry: "Feature: mustbecleaner-storage-cleaner, Property {n}: {title}".</summary>
public static class PropertyTag
{
    public const string Feature = "mustbecleaner-storage-cleaner";

    public static string Format(int number, string title)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return $"Feature: {Feature}, Property {number}: {title}";
    }
}

/// <summary>
/// Use instead of [Property]: enforces the 100-iteration floor and puts the convention tag in the test display name.
/// Example: [MbcProperty(5, "Total byte tanpa overlap")].
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MbcPropertyAttribute : PropertyAttribute
{
    private int _maxTest = PbtConfig.MinIterations;

    public int Number { get; }

    public MbcPropertyAttribute(int number, string title)
    {
        Number = number;
        DisplayName = PropertyTag.Format(number, title);
        base.MaxTest = PbtConfig.MinIterations;
    }

    /// <summary>Can be raised above the minimum; lower values are clamped to 100.</summary>
    public new int MaxTest
    {
        get => _maxTest;
        set { _maxTest = Math.Max(value, PbtConfig.MinIterations); base.MaxTest = _maxTest; }
    }
}
