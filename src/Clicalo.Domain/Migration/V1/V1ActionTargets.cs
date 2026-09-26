using System.Collections.Frozen;
using Clicalo.Domain.Library;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// Converts the <c>action</c> of v1 web and app buttons (catalog §7.4, EJE-011, LOG-008). Nothing is run here: v1
/// passed app commands to a command interpreter, so a command that needs one (an interpreter, a script or shell
/// syntax) is kept as raw text for review and is never started through an interpreter.
/// </summary>
internal static class V1ActionTargets
{
    private const string StoreAppPrefix = "shell:appsfolder\\";

    private static readonly FrozenSet<string> Interpreters = new[]
    {
        "cmd",
        "command",
        "powershell",
        "powershell_ise",
        "pwsh",
        "wscript",
        "cscript",
        "mshta",
        "bash",
        "sh",
        "wsl",
        "python",
        "pythonw",
        "py",
        "pyw",
        "node",
        "perl",
        "ruby",
        "php",
        "rundll32",
        "regsvr32",
        "start",
        "call",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> ScriptExtensions = new[]
    {
        ".bat",
        ".cmd",
        ".ps1",
        ".psm1",
        ".vbs",
        ".vbe",
        ".js",
        ".jse",
        ".wsf",
        ".wsh",
        ".hta",
        ".py",
        ".pyw",
        ".sh",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> ExecutableExtensions = new[]
    {
        ".exe",
        ".com",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The target of a v1 web button, and whether it needs review.</summary>
    /// <param name="action">The address as written.</param>
    public static (UrlTarget Target, MigrationNoteKind? Review) Url(string? action)
    {
        var text = (action ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return (new UrlTarget.Raw(text), MigrationNoteKind.MissingAction);
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var address))
        {
            return IsWeb(address)
                ? (new UrlTarget.Valid(address), null)
                : (new UrlTarget.Raw(text), MigrationNoteKind.NonWebAddress);
        }

        // «ejemplo.com» is completed to https:// as the editor does; anything else is not an address.
        if (
            !text.Any(char.IsWhiteSpace)
            && Uri.TryCreate("https://" + text, UriKind.Absolute, out var completed)
            && completed.Host.Contains('.', StringComparison.Ordinal)
        )
        {
            return (new UrlTarget.Valid(completed), null);
        }

        return (new UrlTarget.Raw(text), MigrationNoteKind.NonWebAddress);
    }

    /// <summary>The target of a v1 app button, and whether it needs review.</summary>
    /// <param name="action">The command as written.</param>
    public static (AppTarget Target, MigrationNoteKind? Review) App(string? action)
    {
        var text = (action ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return (new AppTarget.Raw(text), MigrationNoteKind.MissingAction);
        }

        // Shell syntax, or an address that only the interpreter's «start» could open.
        if (
            HasShellSyntax(text)
            || (
                Uri.TryCreate(text, UriKind.Absolute, out var address)
                && !address.IsFile
                && !text.StartsWith(StoreAppPrefix, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return (new AppTarget.Raw(text), MigrationNoteKind.InterpreterCommand);
        }

        if (text.StartsWith(StoreAppPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var aumid = text[StoreAppPrefix.Length..].Trim();
            return aumid.Length > 0 && !aumid.Any(char.IsWhiteSpace)
                ? (new AppTarget.StoreApp(aumid), null)
                : (new AppTarget.Raw(text), MigrationNoteKind.InterpreterCommand);
        }

        var (program, arguments) = Split(text);
        var extension = Extension(program);
        if (program.Length == 0 || NeedsInterpreter(program, extension))
        {
            return (new AppTarget.Raw(text), MigrationNoteKind.InterpreterCommand);
        }

        if (ExecutableExtensions.Contains(extension) || extension.Length == 0)
        {
            return (new AppTarget.Executable(program, arguments), null);
        }

        // A document opens with its default app; a document with arguments only made sense to the interpreter.
        return arguments.Length == 0
            ? (new AppTarget.Document(program), null)
            : (new AppTarget.Raw(text), MigrationNoteKind.InterpreterCommand);
    }

    private static bool IsWeb(Uri address) =>
        string.Equals(address.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static bool HasShellSyntax(string text)
    {
        foreach (var c in text)
        {
            if (c is '&' or '|' or '<' or '>' or '^' or '`' or '\n' or '\r')
            {
                return true;
            }
        }

        // %VARIABLE% expansion is done by the interpreter.
        var first = text.IndexOf('%', StringComparison.Ordinal);
        return first >= 0 && text.IndexOf('%', first + 1) > first + 1;
    }

    /// <summary>
    /// Splits a command into the program and its arguments: a quoted program, a program up to <c>.exe</c> (paths may
    /// contain spaces), or the first word.
    /// </summary>
    private static (string Program, string Arguments) Split(string text)
    {
        if (text[0] == '"')
        {
            var close = text.IndexOf('"', 1);
            return close < 0
                ? (text[1..].Trim(), string.Empty)
                : (text[1..close].Trim(), text[(close + 1)..].Trim());
        }

        foreach (var extension in ExecutableExtensions)
        {
            var end = text.IndexOf(extension + " ", StringComparison.OrdinalIgnoreCase);
            if (end > 0)
            {
                end += extension.Length;
                return (text[..end], text[end..].Trim());
            }
        }

        if (
            ExecutableExtensions.Contains(Extension(text))
            || text.Contains('\\', StringComparison.Ordinal)
        )
        {
            // A path, spaces included (C:\Program Files\App\app.exe, C:\Docs\my notes.txt).
            return (text, string.Empty);
        }

        var space = text.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? (text, string.Empty) : (text[..space], text[(space + 1)..].Trim());
    }

    private static bool NeedsInterpreter(string program, string extension)
    {
        if (ScriptExtensions.Contains(extension))
        {
            return true;
        }

        var name = FileName(program);
        var stem = extension.Length > 0 ? name[..^extension.Length] : name;
        return Interpreters.Contains(stem);
    }

    private static string FileName(string path)
    {
        var slash = path.LastIndexOfAny(['\\', '/']);
        return slash < 0 ? path : path[(slash + 1)..];
    }

    private static string Extension(string path)
    {
        var name = FileName(path);
        var dot = name.LastIndexOf('.');
        return dot <= 0 ? string.Empty : name[dot..];
    }
}
