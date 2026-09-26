using Clicalo.Domain.Execution;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Coordinators;

/// <summary>
/// What the platform adds to an <c>ExternalForeground</c> before the engine sees it (blueprint §7.7, §7.9): the process
/// the user sees and the keyboard layout of its thread, against which keys are resolved when sending.
/// </summary>
/// <param name="Process">The executable (Store apps already resolved to their own process).</param>
/// <param name="Layout">The layout of the foreground thread.</param>
public sealed record ForegroundDetails(ProcessName Process, KeyboardLayoutSnapshot Layout);
