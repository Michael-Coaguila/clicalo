using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Welcome;

/// <summary>Everything the welcome shows (docs/06), projected at once.</summary>
/// <param name="Step">The step in view, from 0.</param>
/// <param name="StepCount">The number of steps (the progress bars).</param>
/// <param name="StepName">[obStep], the accessible name of the progress bars.</param>
/// <param name="WindowTitle">[welcomeTitle].</param>
/// <param name="AppName">[appName], next to the logo of step 0.</param>
/// <param name="Tagline">[tagline].</param>
/// <param name="Title">The title of the step.</param>
/// <param name="Body">The text under the title; empty on step 4.</param>
/// <param name="Story">[story1].</param>
/// <param name="CreatorName">[creatorName].</param>
/// <param name="CreatorInitials">[creatorInitials].</param>
/// <param name="CreatorRole">[creatorRole].</param>
/// <param name="Reinstall">
/// The question of a reinstallation that found data from before (step 0, P6), or <see langword="null"/>.
/// </param>
/// <param name="Languages">Español and English (step 0).</param>
/// <param name="Uses">The five options of step 1.</param>
/// <param name="Changes">
/// What [Siguiente] of step 1 changes and keeps on a repeated welcome (BIE-010), or <see langword="null"/>.
/// </param>
/// <param name="KeyboardLine">The detected keyboard and the programs language of step 2 (BIE-006).</param>
/// <param name="Kit">«Basics» and the templates of step 2, in the order of the kit.</param>
/// <param name="Views">The three views of step 3.</param>
/// <param name="Sizes">The three sizes of step 4.</param>
/// <param name="CopyLabel">[copy], the label of the sample buttons.</param>
/// <param name="Themes">The four themes of step 4.</param>
/// <param name="CanBack">Whether [Atrás] shows (not on step 0).</param>
/// <param name="BackText">[back].</param>
/// <param name="SkipText">[skip].</param>
/// <param name="NextText">[next], or [finish] on the last step.</param>
public sealed record WelcomeScreen(
    int Step,
    int StepCount,
    string StepName,
    string WindowTitle,
    string AppName,
    string Tagline,
    string Title,
    string Body,
    string Story,
    string CreatorName,
    string CreatorInitials,
    string CreatorRole,
    WelcomeReinstallCard? Reinstall,
    ValueList<WelcomeOption> Languages,
    ValueList<WelcomeOption> Uses,
    WelcomeChangesNote? Changes,
    string KeyboardLine,
    ValueList<WelcomeOption> Kit,
    ValueList<WelcomeViewCard> Views,
    ValueList<WelcomeSizeCard> Sizes,
    string CopyLabel,
    ValueList<WelcomeOption> Themes,
    bool CanBack,
    string BackText,
    string SkipText,
    string NextText
);
