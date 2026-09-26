namespace Clicalo.Domain.Settings;

/// <summary>
/// The value of <see cref="SettingsSchema.NoValue"/>: stands for <see langword="null"/> where an <see cref="object"/>
/// cannot be null, such as <see cref="SettingDescriptor.Default"/> of «no last profile» or «no API key».
/// </summary>
public sealed class NoSettingValue
{
    private NoSettingValue() { }

    /// <summary>The only instance.</summary>
    public static NoSettingValue Instance { get; } = new();

    /// <inheritdoc />
    public override string ToString() => "none";
}
