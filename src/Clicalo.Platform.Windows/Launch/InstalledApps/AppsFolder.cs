using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Clicalo.Application.UseCases.Editor;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace Clicalo.Platform.Windows.Launch.InstalledApps;

/// <summary>
/// Reads the shell's Applications folder (<c>shell:AppsFolder</c>), the list behind «Todas las aplicaciones» of the
/// Start menu: desktop programs and Store apps alike, each with the name Windows shows and its AppUserModelID
/// (EDI-014). It only lists: nothing is opened, and no file of the person is read.
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
                    if (name.Length > 0 && id.Length > 0)
                    {
                        _ = found.TryAdd(id, new InstalledProgram(name, TargetPrefix + id));
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
