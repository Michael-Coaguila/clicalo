using System.Runtime.InteropServices;

namespace Clicalo.Platform.Core.Injection;

/// <summary>What <c>SendInput</c> returned.</summary>
/// <param name="Sent">Events accepted.</param>
/// <param name="LastError">The last Win32 error when fewer events were accepted; 0 otherwise.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SendResult(int Sent, int LastError);
