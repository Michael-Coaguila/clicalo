using Clicalo.Domain.Document;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Application.UseCases.Welcome;

/// <summary>What passing step 1 of the welcome does (<see cref="WelcomeEffects.Plan"/>, BIE-005, BIE-010).</summary>
/// <param name="Settings">The settings afterwards.</param>
/// <param name="Baseline">
/// What the welcome leaves in the four settings: the new value where it applied, the previous baseline where the
/// person had changed the setting by hand, so that change is still recognized the next time.
/// </param>
/// <param name="Changes">The settings that change.</param>
/// <param name="Kept">The settings that would change but stay as the person left them.</param>
public sealed record WelcomeStepPlan(
    UserSettings Settings,
    WelcomeBaseline Baseline,
    ValueList<WelcomeSetting> Changes,
    ValueList<WelcomeSetting> Kept
);
