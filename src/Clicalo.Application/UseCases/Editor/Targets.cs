using System.Net;
using Clicalo.Domain.Library;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// The address of a Web shortcut and the target of an App shortcut as the editor reads and writes them (EDI-014,
/// EJE-011). Pure: nothing is resolved on disk or on the network here.
/// </summary>
public static class Targets
{
    private const string Https = "https://";
    private const string StorePrefix = "shell:AppsFolder\\";
    private const string ExeSuffix = ".exe";

    /// <summary>
    /// The address typed in the Web field: http and https only, «ejemplo.com» completed to https://, domains with ñ,
    /// localhost, IP addresses, ports and parameters. Anything else is kept as written and marked «Incompleto» with
    /// [badUrl] (EDI-014).
    /// </summary>
    /// <param name="text">What the person typed or dictated.</param>
    public static UrlTarget ParseUrl(string? text)
    {
        var written = text ?? string.Empty;
        var trimmed = written.Trim();
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace))
        {
            return new UrlTarget.Raw(written);
        }

        var candidate = trimmed.Contains("://", StringComparison.Ordinal)
            ? trimmed
            : Https + trimmed;
        if (
            !Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || !(
                string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            )
            || !IsHost(uri)
        )
        {
            return new UrlTarget.Raw(written);
        }

        return new UrlTarget.Valid(uri);
    }

    /// <summary>The text of the Web field for <paramref name="target"/>.</summary>
    /// <param name="target">The address.</param>
    public static string Text(UrlTarget target) =>
        target switch
        {
            UrlTarget.Valid valid => valid.Address.OriginalString,
            UrlTarget.Raw raw => raw.Text,
            _ => string.Empty,
        };

    /// <summary>
    /// The target typed in the App field (EJE-011): a Store app (<c>shell:AppsFolder\&lt;AUMID&gt;</c>), a document, or an
    /// executable with its arguments passed as they are, never through a command interpreter. A network path (UNC)
    /// is kept as written and marked «Incompleto»: it would need its own confirmation.
    /// </summary>
    /// <param name="text">What the person typed, dictated or chose.</param>
    public static AppTarget ParseApp(string? text)
    {
        var written = text ?? string.Empty;
        var trimmed = written.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return new AppTarget.Raw(written);
        }

        if (trimmed.StartsWith(StorePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var aumid = trimmed[StorePrefix.Length..].Trim();
            return aumid.Length == 0 ? new AppTarget.Raw(written) : new AppTarget.StoreApp(aumid);
        }

        string path;
        string arguments;
        if (trimmed[0] == '"')
        {
            var close = trimmed.IndexOf('"', 1);
            if (close < 0)
            {
                return new AppTarget.Raw(written);
            }

            path = trimmed[1..close].Trim();
            arguments = trimmed[(close + 1)..].Trim();
        }
        else
        {
            var exe = trimmed.IndexOf(ExeSuffix + " ", StringComparison.OrdinalIgnoreCase);
            if (exe > 0)
            {
                path = trimmed[..(exe + ExeSuffix.Length)];
                arguments = trimmed[(exe + ExeSuffix.Length)..].Trim();
            }
            else
            {
                path = trimmed;
                arguments = string.Empty;
            }
        }

        if (path.Length == 0 || path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return new AppTarget.Raw(written);
        }

        return IsDocument(path) && arguments.Length == 0
            ? new AppTarget.Document(path)
            : new AppTarget.Executable(path, arguments);
    }

    /// <summary>The text of the App field for <paramref name="target"/>.</summary>
    /// <param name="target">The target.</param>
    public static string Text(AppTarget target) =>
        target switch
        {
            AppTarget.Executable exe when exe.Arguments.Length == 0 => exe.Path,
            AppTarget.Executable exe => Quote(exe.Path) + " " + exe.Arguments,
            AppTarget.StoreApp store => StorePrefix + store.AppUserModelId,
            AppTarget.Document document => document.Path,
            AppTarget.Raw raw => raw.Text,
            _ => string.Empty,
        };

    private static bool IsHost(Uri uri)
    {
        var host = uri.IdnHost;
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        if (
            uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
            || IPAddress.TryParse(host, out _)
        )
        {
            return true;
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var labels = host.Split('.');
        return labels.Length >= 2
            && labels.All(static label => label.Length > 0)
            && labels[^1].Length >= 2
            && !labels[^1].All(char.IsDigit);
    }

    private static bool IsDocument(string path)
    {
        var dot = path.LastIndexOf('.');
        var separator = path.LastIndexOfAny(['\\', '/']);
        if (dot <= separator + 1 || dot == path.Length - 1)
        {
            return false;
        }

        var extension = path[dot..];
        return separator >= 0
            && !string.Equals(extension, ExeSuffix, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".com", StringComparison.OrdinalIgnoreCase);
    }

    private static string Quote(string path) =>
        path.Contains(' ', StringComparison.Ordinal) ? "\"" + path + "\"" : path;
}
