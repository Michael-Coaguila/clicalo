namespace Clicalo.Domain.Settings;

/// <summary>The outcome of <see cref="SettingsSchema.Write"/>.</summary>
public enum SettingWriteStatus
{
    /// <summary>The value was written (or was already the current one).</summary>
    Written,

    /// <summary>No setting has that path.</summary>
    UnknownPath,

    /// <summary>The value has another type (or is <see langword="null"/> for a setting that needs one).</summary>
    WrongType,

    /// <summary>The value is outside the range or the choices of the setting.</summary>
    OutOfRange,
}
