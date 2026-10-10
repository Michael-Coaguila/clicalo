using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Execution;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Clicalo.Platform.Windows.Launch.InstalledApps;

/// <summary>
/// Reads the shell's Applications folder (<c>shell:AppsFolder</c>), the list behind «Todas las aplicaciones» of the
/// Start menu: desktop programs and Store apps alike, each with the name Windows shows and its AppUserModelID
/// (EDI-014). It only lists: nothing is opened, and no file of the person is read. A program the launcher would
/// refuse (<see cref="LaunchSafety"/>) is left out, so every program offered can be opened.
/// </summary>
internal static unsafe class AppsFolder
{
    /// <summary>What the App field gets before the AppUserModelID; <c>Targets.ParseApp</c> reads it as a Store app.</summary>
    public const string TargetPrefix = @"shell:AppsFolder\";

    private const string FolderName = "shell:AppsFolder";

    // A Start menu has a few hundred entries; this only stops a broken shell extension from listing without end.
    private const int Ceiling = 2000;

    /// <summary>The programs of the folder, by name. Runs on a COM thread; empty when the shell does not answer.</summary>
    public static ImmutableArray<InstalledProgram> Read()
    {
        IShellItem* folder = null;
        IEnumShellItems* items = null;
        var found = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var itemId = IShellItem.IID_Guid;
            fixed (char* name = FolderName)
            {
                if (
                    PInvoke.SHCreateItemFromParsingName(name, null, &itemId, (void**)&folder).Failed
                    || folder is null
                )
                {
                    return [];
                }
            }

            var handler = PInvoke.BHID_EnumItems;
            var enumId = IEnumShellItems.IID_Guid;
            folder->BindToHandler(null, &handler, &enumId, (void**)&items);
            if (items is null)
            {
                return [];
            }

            for (var read = 0; read < Ceiling; read++)
            {
                IShellItem* item = null;
                uint fetched = 0;
                items->Next(1, &item, &fetched);
                if (fetched == 0 || item is null)
                {
                    break;
                }

                try
                {
                    var name = Name(item, SIGDN.SIGDN_NORMALDISPLAY);
                    var id = Name(item, SIGDN.SIGDN_PARENTRELATIVEPARSING);
                    if (
                        name.Length > 0
                        && id.Length > 0
                        && TargetOf(id, KnownFolder, File.Exists) is { } target
                    )
                    {
                        _ = found.TryAdd(target, new InstalledProgram(name, target));
                    }
                }
                finally
                {
                    _ = item->Release();
                }
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or ArgumentException)
        {
            // The shell refused: «Elegir programa» keeps the open apps.
        }
        finally
        {
            if (items is not null)
            {
                _ = items->Release();
            }

            if (folder is not null)
            {
                _ = folder->Release();
            }
        }

        return
        [
            .. found
                .Values.OrderBy(static p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(static p => p.Target, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// The text of the App field that opens the program Windows names <paramref name="id"/>, or null when the launcher
    /// would not open it. A Store app, and a desktop program with an AppUserModelID of its own, is
    /// <c>shell:AppsFolder\&lt;id&gt;</c>. A desktop program without one is named by the path of its file, with the known
    /// folder as a GUID (<c>{…}\notepad.exe</c>): the launcher refuses a backslash after <c>shell:AppsFolder\</c>, so
    /// its target is the path itself, when the file exists.
    /// </summary>
    /// <param name="id">The parsing name of the item inside the Applications folder.</param>
    /// <param name="knownFolder">The path of a known folder, or null when Windows has none for that GUID.</param>
    /// <param name="exists">Whether a file exists.</param>
    internal static string? TargetOf(
        string id,
        Func<Guid, string?> knownFolder,
        Func<string, bool> exists
    )
    {
        string target;
        if (!id.Contains('\\', StringComparison.Ordinal))
        {
            target = TargetPrefix + id;
        }
        else
        {
            var path = id;
            if (id[0] == '{')
            {
                var close = id.IndexOf('}', StringComparison.Ordinal);
                if (
                    close < 0
                    || !Guid.TryParse(id.AsSpan(0, close + 1), out var folder)
                    || knownFolder(folder) is not { Length: > 0 } root
                )
                {
                    return null;
                }

                path = root.TrimEnd('\\') + id[(close + 1)..];
            }

            if (!Path.IsPathFullyQualified(path) || !exists(path))
            {
                return null;
            }

            target = path;
        }

        return
            LaunchSafety.Check(
                new LaunchRequest.StartApp(Targets.ParseApp(target)),
                confirmed: false
            ) == LaunchVerdict.Allowed
            ? target
            : null;
    }

    private static string? KnownFolder(Guid folder)
    {
        PWSTR path = default;
        try
        {
            return
                PInvoke.SHGetKnownFolderPath(&folder, default, default, &path).Succeeded
                && path.Value is not null
                ? new string(path.Value)
                : null;
        }
        finally
        {
            if (path.Value is not null)
            {
                PInvoke.CoTaskMemFree(path.Value);
            }
        }
    }

    private static string Name(IShellItem* item, SIGDN form)
    {
        PWSTR text = default;
        try
        {
            item->GetDisplayName(form, &text);
            return text.Value is null ? string.Empty : new string(text.Value).Trim();
        }
        catch (Exception ex) when (ex is COMException or ArgumentException)
        {
            return string.Empty;
        }
        finally
        {
            if (text.Value is not null)
            {
                PInvoke.CoTaskMemFree(text.Value);
            }
        }
    }
}
