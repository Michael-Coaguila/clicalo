namespace Clicalo.Domain.Migration.V1;

/// <summary>Counts compared before and after the import (MIG-004: equal, separators, addresses and apps included).</summary>
/// <param name="Profiles">Profiles.</param>
/// <param name="Buttons">Buttons, separators included.</param>
/// <param name="Separators">Separators.</param>
/// <param name="Urls">Web buttons.</param>
/// <param name="Apps">App buttons.</param>
public sealed record V1Counts(int Profiles, int Buttons, int Separators, int Urls, int Apps)
{
    /// <summary>
    /// The counts of a v1 document as read: what the welcome announces before importing (BIE-002) and what the import
    /// must produce (MIG-004).
    /// </summary>
    /// <param name="document">The v1 document.</param>
    public static V1Counts Of(V1Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var buttons = 0;
        var separators = 0;
        var urls = 0;
        var apps = 0;
        foreach (var profile in document.Profiles)
        {
            foreach (var button in profile.Buttons)
            {
                buttons++;
                switch (button.Kind)
                {
                    case V1ButtonKind.Separator:
                        separators++;
                        break;
                    case V1ButtonKind.Url:
                        urls++;
                        break;
                    case V1ButtonKind.App:
                        apps++;
                        break;
                }
            }
        }

        return new V1Counts(document.Profiles.Count, buttons, separators, urls, apps);
    }
}
