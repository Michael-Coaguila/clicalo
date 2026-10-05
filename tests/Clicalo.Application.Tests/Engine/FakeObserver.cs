using Clicalo.Application.Engine;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Tests.Engine;

/// <summary>The UI side of the engine: snapshots, notices, usage and the last action.</summary>
internal sealed class FakeObserver : IEngineObserver
{
    public List<EngineSnapshot> Snapshots { get; } = [];

    public List<(Message Text, NoticeUrgency Urgency)> Notices { get; } = [];

    public List<ShortcutId> Usage { get; } = [];

    public List<ShortcutId> LastActions { get; } = [];

    public void OnSnapshot(EngineSnapshot snapshot) => Snapshots.Add(snapshot);

    public void OnNotice(Message text, NoticeUrgency urgency) => Notices.Add((text, urgency));

    public void OnUsage(ShortcutId shortcut, DateTimeOffset at) => Usage.Add(shortcut);

    public void OnLastAction(ShortcutId shortcut) => LastActions.Add(shortcut);
}
