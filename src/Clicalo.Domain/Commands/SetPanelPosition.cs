using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Remembers where the panel is on a monitor (docs/02 <c>panelPosByMonitor</c>). Placement, never undoable: undoing it
/// would move the panel under the user's finger (<c>undo-exemptions.json</c>).
/// </summary>
/// <param name="Position">The monitor and the position.</param>
public sealed record SetPanelPosition(MonitorPosition Position) : IDocumentCommand
{
    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (Position is null || string.IsNullOrEmpty(Position.MonitorId))
        {
            return Changes.Fail(CommandFailures.MonitorEmpty());
        }

        var positions = document.Settings.PanelPositions.Items;
        var index = -1;
        for (var i = 0; i < positions.Length && index < 0; i++)
        {
            if (string.Equals(positions[i].MonitorId, Position.MonitorId, StringComparison.Ordinal))
            {
                index = i;
            }
        }

        if (index >= 0 && positions[index].Equals(Position))
        {
            return Changes.Transparent(document);
        }

        var updated = index >= 0 ? positions.SetItem(index, Position) : positions.Add(Position);
        return Changes.Transparent(
            document with
            {
                Settings = document.Settings with { PanelPositions = new(updated) },
            }
        );
    }
}
