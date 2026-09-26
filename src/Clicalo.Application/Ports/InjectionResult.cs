using System.Runtime.InteropServices;

namespace Clicalo.Application.Ports;

/// <summary>The result of one call to <see cref="IInputInjector"/>.</summary>
/// <param name="Status">What happened.</param>
/// <param name="EventsSent">How many events <c>SendInput</c> accepted.</param>
/// <param name="Win32Error">The last error when the status is <see cref="InjectionStatus.Failed"/>; 0 otherwise.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct InjectionResult(
    InjectionStatus Status,
    int EventsSent,
    int Win32Error
);
