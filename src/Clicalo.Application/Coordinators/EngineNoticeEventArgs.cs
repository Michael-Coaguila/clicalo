using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.Coordinators;

/// <summary>Data of <see cref="EngineObserverRelay.NoticeRaised"/>.</summary>
/// <param name="text">The notice, localized when it is painted (IDI-001).</param>
/// <param name="urgency">How screen readers announce it.</param>
public sealed class EngineNoticeEventArgs(Message text, NoticeUrgency urgency) : EventArgs
{
    /// <summary>The notice, localized when it is painted (IDI-001).</summary>
    public Message Text { get; } = text ?? throw new ArgumentNullException(nameof(text));

    /// <summary>How screen readers announce it.</summary>
    public NoticeUrgency Urgency { get; } = urgency;
}
