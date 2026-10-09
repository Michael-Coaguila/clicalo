using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.Panel;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>Records every intention of the body of the panel, in order, as text.</summary>
internal sealed class RecordingBodyIntents : IPanelBodyIntents
{
    public List<string> Calls { get; } = [];

    public void ShowFrequents() => Calls.Add(nameof(ShowFrequents));

    public void ReturnFromFrequents() => Calls.Add(nameof(ReturnFromFrequents));

    public void TogglePicker() => Calls.Add(nameof(TogglePicker));

    public void ChooseProfile(ProfileId profile) =>
        Calls.Add(nameof(ChooseProfile) + ":" + profile.Value);

    public void CreateProfileForActiveApp() => Calls.Add(nameof(CreateProfileForActiveApp));

    public void OpenTemplates() => Calls.Add(nameof(OpenTemplates));

    public void AdvanceSticky(ModifierKind modifier) =>
        Calls.Add(nameof(AdvanceSticky) + ":" + modifier);

    public void Undo() => Calls.Add(nameof(Undo));

    public void Repeat() => Calls.Add(nameof(Repeat));

    public void AddShortcut(ProfileId profile) =>
        Calls.Add(nameof(AddShortcut) + ":" + profile.Value);

    public void RelaunchElevated() => Calls.Add(nameof(RelaunchElevated));

    public void CancelNotice() => Calls.Add(nameof(CancelNotice));
}
