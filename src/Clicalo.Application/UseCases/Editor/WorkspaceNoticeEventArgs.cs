namespace Clicalo.Application.UseCases.Editor;

/// <summary>A message for the status bar of the Control Center (CCM-003).</summary>
/// <param name="notice">The message.</param>
public sealed class WorkspaceNoticeEventArgs(WorkspaceNotice notice) : EventArgs
{
    /// <summary>The message.</summary>
    public WorkspaceNotice Notice { get; } = notice;
}
