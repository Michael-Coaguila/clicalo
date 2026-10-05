using Clicalo.Domain.Execution;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;

namespace Clicalo.Application.Ports;

/// <summary>
/// The only way the engine sends input (blueprint §4.4, ADR-0023). Implemented once, in Platform.Windows, over the
/// product's single <c>SendInput</c> (<c>Clicalo.Platform.Core.Injection.LowLevelInjector</c>). Only
/// Application.Engine, Platform.Windows and App depend on it (ArchUnit). Called only from the engine thread.
/// </summary>
public interface IInputInjector
{
    /// <summary>Sends key and button events in the mode each key carries, with the menu mask before Alt or Win releases.</summary>
    /// <param name="events">The events, in order.</param>
    InjectionResult Send(ReadOnlySpan<InjectedEvent> events);

    /// <summary>Types a text as Unicode (<c>KEYEVENTF_UNICODE</c>); line breaks go as Enter (EJE-008).</summary>
    /// <param name="text">The text, in a buffer the caller wipes afterwards.</param>
    InjectionResult TypeText(ReadOnlySpan<char> text);

    /// <summary>
    /// Moves the pointer to <paramref name="target"/> and performs a click or one scroll step there (EJE-009); a drag
    /// holds the left button through <see cref="Send"/> instead.
    /// </summary>
    /// <param name="operation">The mouse action.</param>
    /// <param name="target">Where, in physical pixels; <see langword="null"/> for the centre of the foreground client area.</param>
    InjectionResult Mouse(MouseOp operation, PhysicalPoint? target);

    /// <summary>
    /// Sends one of Clícalo's own chords (blueprint §3.6) as one balanced batch: presses in order the keys Windows does
    /// not report down and releases them in reverse order, so a key an engine holder keeps is neither pressed again nor
    /// released under it; a batch <c>SendInput</c> takes only in part is balanced at once.
    /// </summary>
    /// <param name="chord">Which chord.</param>
    InjectionResult SendChord(InternalChord chord);

    /// <summary>
    /// Releases everything Windows reports down, keys and buttons, with the menu mask before Alt or Win (ADR-0023):
    /// the release of an engine that caught an exception (NFR-005), whose own state may be the broken part.
    /// </summary>
    InjectionResult ReleasePressed();
}
