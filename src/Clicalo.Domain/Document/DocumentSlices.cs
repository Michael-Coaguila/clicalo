namespace Clicalo.Domain.Document;

/// <summary>
/// The parts of a <see cref="UserDocument"/> that undo restores independently (blueprint §6.4): undoing «delete
/// shortcut» restores <see cref="Library"/> but not the usage recorded afterwards. Detected by reference.
/// </summary>
[Flags]
public enum DocumentSlices
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>Profiles, shortcuts and the Always visible row.</summary>
    Library = 1 << 0,

    /// <summary>Pinned and hidden Frequents.</summary>
    FrequentsCuration = 1 << 1,

    /// <summary>Usage time stamps (saved to <c>usage.json</c>, never triggers a backup).</summary>
    FrequentsUsage = 1 << 2,

    /// <summary>Ignored repeated combinations.</summary>
    Duplicates = 1 << 3,

    /// <summary>Settings.</summary>
    Settings = 1 << 4,

    /// <summary>Welcome state.</summary>
    Onboarding = 1 << 5,

    /// <summary>Every slice saved to <c>clicalo.json</c> and able to trigger an automatic backup (§6.4, §6.8).</summary>
    Significant = Library | FrequentsCuration | Duplicates | Settings | Onboarding,
}
