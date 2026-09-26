using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Library;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Document;

/// <summary>
/// The user document, root of all persisted state (blueprint §6.3, DAT-001): immutable and shared between threads;
/// only <c>DocumentStore</c> replaces it, through commands. Its invariants (<see cref="DocumentInvariant"/>) hold for
/// every instance the store publishes; persistence repairs or quarantines anything else before it gets here (§6.5).
/// </summary>
/// <param name="Revision">Raised by the store on every change; a change that only raises it is not saved.</param>
/// <param name="Library">Profiles, shortcuts and the Always visible row.</param>
/// <param name="Frequents">Pins, hidden and usage.</param>
/// <param name="Duplicates">Ignored repeated combinations.</param>
/// <param name="Settings">Settings.</param>
/// <param name="Onboarding">Welcome state.</param>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the domain package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed record UserDocument(
    long Revision,
    ShortcutLibrary Library,
    FrequentsState Frequents,
    DuplicatePolicy Duplicates,
    UserSettings Settings,
    OnboardingState Onboarding
)
{
    /// <summary>Every broken invariant; empty for a valid document.</summary>
    public ImmutableArray<DocumentViolation> Validate() => throw new NotImplementedException();

    /// <summary>
    /// This document with <paramref name="slices"/> taken from <paramref name="before"/> and every other slice kept:
    /// the undo of the store (§6.4). The revision is raised by the store, not here.
    /// </summary>
    /// <param name="before">The document of the undo entry.</param>
    /// <param name="slices">The slices the undone change touched.</param>
    public UserDocument RestoreSlices(UserDocument before, DocumentSlices slices) =>
        throw new NotImplementedException();
}
