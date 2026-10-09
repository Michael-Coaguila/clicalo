using System.Collections.Frozen;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Clicalo.Application.Ports;
using Clicalo.Domain.Primitives;

namespace Clicalo.App.Composition;

/// <summary>
/// The apps open on the desktop for the Control Center (ATJ-006, PRB-003, EDI-014): the processes with a visible main
/// window and a title, one per executable, without Clícalo itself, the shell hosts and the touch keyboard, ordered by
/// name. Read off the UI thread when asked; the titles are never kept or logged (LOG-001). A process that cannot be
/// inspected (another user's or an administrator's) counts as elevated (docs/03 §1).
/// </summary>
internal sealed class ControlCenterOpenApps : IOpenApps
{
    private static readonly FrozenSet<string> Hidden = new[]
    {
        "applicationframehost",
        "textinputhost",
        "shellexperiencehost",
        "startmenuexperiencehost",
        "searchhost",
        "lockapp",
        "systemsettingsbroker",
        "clicalo",
        "clicalo.sentinel",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public ValueTask<ImmutableArray<OpenApp>> ListAsync(CancellationToken cancellationToken) =>
        new(Task.Run(Read, cancellationToken));

    private static ImmutableArray<OpenApp> Read()
    {
        var own = Environment.ProcessId;
        var apps = new Dictionary<string, OpenApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (Describe(process, own) is { } app && !apps.ContainsKey(app.Process.Value))
                {
                    apps.Add(app.Process.Value, app);
                }
            }
        }

        return
        [
            .. apps.Values.OrderBy(static a => a.Name, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    private static OpenApp? Describe(Process process, int own)
    {
        try
        {
            if (
                process.Id == own
                || process.MainWindowHandle == 0
                || string.IsNullOrWhiteSpace(process.MainWindowTitle)
                || Hidden.Contains(process.ProcessName)
            )
            {
                return null;
            }

            var executable = process.ProcessName + ".exe";
            string? path = null;
            string? description = null;
            var elevated = false;
            try
            {
                path = process.MainModule?.FileName;
                description = process.MainModule?.FileVersionInfo.FileDescription;
            }
            catch (Win32Exception)
            {
                elevated = true;
            }

            var name = string.IsNullOrWhiteSpace(description)
                ? process.ProcessName
                : description.Trim();
            if (path is not null)
            {
                executable = Path.GetFileName(path);
            }

            return new OpenApp(
                new ProcessName(executable),
                name,
                new WindowToken(process.MainWindowHandle),
                elevated,
                path
            );
        }
        catch (Exception ex)
            when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // The process ended while it was read.
            return null;
        }
    }
}
