using System.Runtime.InteropServices;

namespace Clicalo.Platform.Core.Injection;

/// <summary>The result of <see cref="InjectionGate.TryInject"/>.</summary>
/// <param name="Result">Whether it ran.</param>
/// <param name="Send">What <c>SendInput</c> returned; default when fenced.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct GateOutcome(GateResult Result, SendResult Send);
