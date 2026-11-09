using System.Collections.Immutable;

namespace MustBeCleaner.Domain;

/// <summary>
/// Observations about the outside world (active processes, locks, app fingerprint) supplied to
/// the Policy Core as input, so the core does no I/O. Anything not observed reads as
/// <see cref="TriState.Unknown"/> (R5.2, R5.3). Immutable.
/// </summary>
public sealed class WorldFacts
{
    private readonly ImmutableDictionary<Condition, TriState> _observed;

    private WorldFacts(ImmutableDictionary<Condition, TriState> observed) => _observed = observed;

    /// <summary>No observations: every condition is Unknown.</summary>
    public static WorldFacts None { get; } = new(ImmutableDictionary<Condition, TriState>.Empty);

    /// <summary>Value of <paramref name="condition"/>, or Unknown if not observed.</summary>
    public TriState this[Condition condition] =>
        _observed.TryGetValue(condition, out var value) ? value : TriState.Unknown;

    /// <summary>Returns a copy with <paramref name="condition"/> observed as <paramref name="value"/>.</summary>
    public WorldFacts With(Condition condition, TriState value) => new(_observed.SetItem(condition, value));
}
