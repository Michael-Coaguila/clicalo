using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Primitives;
using Clicalo.Platform.Windows.SingleInstance;

namespace Clicalo.App.Composition;

/// <summary>
/// Adds the process and the keyboard layout to an external foreground before the engine sees it (blueprint §7.7,
/// §7.9). Runs on the SysEvents thread and never blocks: both queries are local and answer «unknown» on failure.
/// </summary>
/// <param name="layouts">
/// Builds the layout snapshot of a thread: the engine package's builder (<c>VkKeyScanEx</c>, <c>MapVirtualKeyEx</c>,
/// <see cref="EngineAdapters.Layouts"/>).
/// </param>
internal sealed class ForegroundDescriber(Func<uint, KeyboardLayoutSnapshot> layouts)
{
    private readonly ConcurrentDictionary<ProcessName, string> _names = new();

    /// <summary>Describes <paramref name="foreground"/>.</summary>
    /// <param name="foreground">The verified external foreground.</param>
    public ForegroundDetails Describe(ExternalForeground foreground)
    {
        ArgumentNullException.ThrowIfNull(foreground);
        return new ForegroundDetails(
            new ProcessName(ProcessIdentity.ImageFileName(foreground.AppProcessId) ?? string.Empty),
            layouts(foreground.ThreadId)
        );
    }

    /// <summary>
    /// The name of the app in front as the notices say it (EJE-003): the description of its executable («Microsoft
    /// Word», «Editor del Registro», in the language of Windows). It reads the file, so it runs on the thread pool, never
    /// on the SysEvents thread; the path comes from <c>QueryFullProcessImageName</c>, which also answers for an app
    /// that runs as administrator. Null when the executable has no description.
    /// </summary>
    /// <param name="foreground">The verified external foreground.</param>
    /// <param name="process">Its executable.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    public Task<string?> NameAsync(
        ExternalForeground foreground,
        ProcessName process,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(foreground);
        return _names.TryGetValue(process, out var known)
            ? Task.FromResult<string?>(known)
            : Task.Run(() => Read(foreground.AppProcessId, process), cancellationToken);
    }

    /// <summary>The name already read for <paramref name="process"/>, or null.</summary>
    /// <param name="process">An executable.</param>
    public string? KnownName(ProcessName process) =>
        _names.TryGetValue(process, out var name) ? name : null;

    private string? Read(uint processId, ProcessName process)
    {
        if (ProcessIdentity.ImagePath(processId) is not { } path)
        {
            return null;
        }

        string? description;
        try
        {
            description = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (string.IsNullOrEmpty(description))
        {
            return null;
        }

        _names[process] = description;
        return description;
    }
}
