using System.Runtime.InteropServices;

namespace Clicalo.Platform.Core.Injection;

/// <summary>What <c>SendInput</c> returned.</summary>
/// <param name="Sent">Events accepted.</param>
/// <param name="LastError">The last Win32 error when fewer events were accepted; 0 otherwise.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SendResult(int Sent, int LastError)
{
    /// <summary><c>ERROR_ACCESS_DENIED</c>: <c>SendInput</c> is refused while the secure desktop is in front.</summary>
    public const int AccessDenied = 5;

    /// <summary>
    /// Whether the secure desktop refused the whole batch (a locked session, UAC, Ctrl+Alt+Supr): nothing accepted and
    /// <see cref="AccessDenied"/>.
    /// </summary>
    public bool IsSecureDesktopRefusal => Sent == 0 && LastError == AccessDenied;
}
