namespace Clicalo.Presentation.Welcome;

/// <summary>
/// The question of step 0 on the first start of a new installation that found data from before (NFR-010, proposal
/// P6): keeping the data is the default; «Empezar de cero» needs two taps and saves a backup first (REG-04, REG-08).
/// </summary>
/// <param name="Title">[reinstallT].</param>
/// <param name="Description">[reinstallD].</param>
/// <param name="KeepText">[reinstallKeep], the option in force.</param>
/// <param name="FreshText">[reinstallFresh], or [confirmB] while the first tap is armed.</param>
/// <param name="FreshArmed">Whether the first tap of «Empezar de cero» is armed.</param>
/// <param name="Done">[reinstallDone] once the person started from scratch (the two options are gone); empty before.</param>
public sealed record WelcomeReinstallCard(
    string Title,
    string Description,
    string KeepText,
    string FreshText,
    bool FreshArmed,
    string Done
);
