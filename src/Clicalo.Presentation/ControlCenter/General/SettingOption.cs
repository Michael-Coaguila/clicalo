namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>
/// One card or button of a choice of «General y panel» (GEN-002 to GEN-012): the value it writes, what it shows, its
/// accessible name and whether it is the current one (shown with the accent outline and the Toggle state, ACC-003).
/// </summary>
/// <typeparam name="T">The type of the setting.</typeparam>
/// <param name="Value">The value it writes.</param>
/// <param name="Label">What it shows.</param>
/// <param name="Name">Its accessible name.</param>
/// <param name="Selected">Whether it is the current value.</param>
/// <param name="Cells">What its miniature draws: the rows of «Filas visibles» or the columns of «Columnas»; 0 otherwise.</param>
public sealed record SettingOption<T>(
    T Value,
    string Label,
    string Name,
    bool Selected,
    int Cells = 0
);
