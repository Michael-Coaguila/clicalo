using System.Text.RegularExpressions;

namespace Clicalo.Infrastructure.Logging;

/// <summary>
/// Removes the Windows user from log text (LOG-001: «rutas de App, sin el nombre de usuario»): the profile folder
/// becomes <c>%USERPROFILE%</c> and the user name as a path segment becomes <c>%USERNAME%</c>. It runs when the text is
/// written, never later (blueprint §9.4).
/// </summary>
public sealed class UserPathRedactor
{
    private readonly string? _profile;
    private readonly Regex? _userSegment;

    /// <summary>Creates a redactor for one user.</summary>
    /// <param name="profileFolder">The profile folder (<c>C:\Users\name</c>), or empty.</param>
    /// <param name="userName">The user name, or empty.</param>
    public UserPathRedactor(string? profileFolder, string? userName)
    {
        _profile = string.IsNullOrWhiteSpace(profileFolder)
            ? null
            : profileFolder.TrimEnd('\\', '/');
        _userSegment = string.IsNullOrWhiteSpace(userName)
            ? null
            : new Regex(
                @"(?<=[\\/])" + Regex.Escape(userName) + @"(?=[\\/]|\b|$)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(250)
            );
    }

    /// <summary>The redactor of the Windows user running the process.</summary>
    public static UserPathRedactor ForCurrentUser() =>
        new(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.UserName);

    /// <summary><paramref name="text"/> without the user's profile folder or name.</summary>
    /// <param name="text">Log text.</param>
    public string Redact(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = text;
        if (_profile is not null && result.Contains(_profile, StringComparison.OrdinalIgnoreCase))
        {
            result = result.Replace(_profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        if (_userSegment is not null)
        {
            try
            {
                result = _userSegment.Replace(result, "%USERNAME%");
            }
            catch (RegexMatchTimeoutException)
            {
                return "[path hidden]";
            }
        }

        return result;
    }
}
