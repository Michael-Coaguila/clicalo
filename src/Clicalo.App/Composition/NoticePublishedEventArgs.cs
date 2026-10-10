using Clicalo.Presentation.Panel;

namespace Clicalo.App.Composition;

/// <summary>Data of <see cref="PanelComposer.NoticePublished"/>.</summary>
/// <param name="notice">The notice on show now, or <see langword="null"/> when the bars rest.</param>
internal sealed class NoticePublishedEventArgs(PanelNotice? notice) : EventArgs
{
    /// <summary>The notice on show now, or <see langword="null"/> when the bars rest (AVI-002).</summary>
    public PanelNotice? Notice { get; } = notice;
}
