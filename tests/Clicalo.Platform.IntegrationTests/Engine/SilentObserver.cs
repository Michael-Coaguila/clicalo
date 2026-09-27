using Clicalo.Application.Engine;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>An engine observer that ignores everything.</summary>
internal sealed class SilentObserver : IEngineObserver
{
    public static SilentObserver Instance { get; } = new();

    public void OnSnapshot(EngineSnapshot snapshot) { }

    public void OnNotice(Message text, NoticeUrgency urgency) { }

    public void OnUsage(ShortcutId shortcut, DateTimeOffset at) { }

    public void OnLastAction(ShortcutId shortcut) { }
}
