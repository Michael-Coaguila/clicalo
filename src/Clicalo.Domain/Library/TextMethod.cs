namespace Clicalo.Domain.Library;

/// <summary>How a text is sent (EJE-008).</summary>
public enum TextMethod
{
    /// <summary>Character by character as Unicode, independent of the layout (the default).</summary>
    Unicode,

    /// <summary>Through the clipboard, restored afterwards and kept out of the clipboard history.</summary>
    Paste,
}
