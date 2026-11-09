namespace MustBeCleaner.Domain;

/// <summary>
/// The single status every Item receives (R5.1). <see cref="InventoryOnly"/> is for folders no
/// Adapter recognizes and non-NTFS volumes; it is excluded from bulk selection (R5.8, R7.7).
/// </summary>
public enum Status
{
    /// <summary>Matches a protection rule. Cannot be selected for action (R5.4, R6).</summary>
    Protected,
    /// <summary>Needs a user decision; not yet Eligible.</summary>
    Review,
    /// <summary>Every R11.1 condition is True and no protection rule matches.</summary>
    Eligible,
    /// <summary>Listed only; no action offered (R5.8).</summary>
    InventoryOnly,
}
