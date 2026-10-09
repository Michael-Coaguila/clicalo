using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>«Novedades» of one version (ACT-004).</summary>
/// <param name="Version">The version.</param>
/// <param name="Date">Its month, written in the language («oct 2026»); empty when unknown.</param>
/// <param name="NewBadge">[newBadge] for the version found; empty otherwise.</param>
/// <param name="Items">The changes, each with ✓.</param>
public sealed record NotesModel(
    string Version,
    string Date,
    string NewBadge,
    ValueList<string> Items
);
