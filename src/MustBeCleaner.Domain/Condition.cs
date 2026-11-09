namespace MustBeCleaner.Domain;

/// <summary>
/// The eligibility conditions of R11.1 that are evaluated as <see cref="TriState"/> (R5.2).
/// "No protection rule matches" is not listed: it is decided by the rule set, not observed.
/// </summary>
public enum Condition
{
    /// <summary>Scope is a supported local NTFS volume (R7.7, R11.1).</summary>
    LocalScopeSupported,
    /// <summary>A trusted Adapter matches the app and version (R11.1, R5.8).</summary>
    TrustedAdapterMatches,
    /// <summary>The disposable category is confirmed (R11.1).</summary>
    DisposableCategoryConfirmed,
    /// <summary>Owner is quiescent with no unknown activity (R11.1, R11.3).</summary>
    OwnerQuiescent,
    /// <summary>Identity and metadata are unchanged since preview (R11.1, R11.4).</summary>
    IdentityUnchangedSincePreview,
    /// <summary>A recovery method is ready (R11.1).</summary>
    RecoveryReady,
    /// <summary>The user selected the Item on the Manifest (R11.1).</summary>
    SelectedOnManifest,
}
