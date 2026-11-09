namespace MustBeCleaner.Domain;

/// <summary>
/// Value of an eligibility condition: true, false, or unknown (R5.2).
/// Unknown is the default value and is treated as a refusal to act (R5.3).
/// </summary>
public enum TriState
{
    /// <summary>The condition was not observed. Blocks action.</summary>
    Unknown = 0,
    /// <summary>The condition holds.</summary>
    True = 1,
    /// <summary>The condition does not hold.</summary>
    False = 2,
}
