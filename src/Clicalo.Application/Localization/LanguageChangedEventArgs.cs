namespace Clicalo.Application.Localization;

/// <summary>Data of <see cref="ILocalizationContext.LanguageChanged"/>.</summary>
public sealed class LanguageChangedEventArgs(ILocalizer previous, ILocalizer current) : EventArgs
{
    /// <summary>The formatter before the change.</summary>
    public ILocalizer Previous { get; } = previous;

    /// <summary>The formatter after the change.</summary>
    public ILocalizer Current { get; } = current;
}
