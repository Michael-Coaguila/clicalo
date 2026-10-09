namespace Clicalo.Presentation.Panel.Search;

/// <summary>
/// How the user reached a control of the search, as the view knows it. It picks the rights ladder of the keyboard lease
/// (blueprint §3.6): the view never sees the foreground types of Application (§4.2).
/// </summary>
public enum SearchTrigger
{
    /// <summary>Finger, pen or mouse through the panel's pointer layer.</summary>
    Touch,

    /// <summary>UI Automation: Voice access, Narrator, Windows Speech Recognition or a switch.</summary>
    Automation,
}
