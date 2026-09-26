namespace Clicalo.Domain.Migration.V1;

/// <summary>The variants of a v1 button (catalog §7.2).</summary>
public enum V1ButtonKind
{
    /// <summary>(a) <c>hotkey</c> without <c>type</c>: the 210 real buttons.</summary>
    ImplicitHotkey,

    /// <summary>(b) <c>type: hotkey</c> with <c>hotkey</c>.</summary>
    ExplicitHotkey,

    /// <summary>(c) a hotkey written in <c>action</c> instead of <c>hotkey</c>.</summary>
    ActionHotkey,

    /// <summary>(d) <c>type: url</c>.</summary>
    Url,

    /// <summary>(e) <c>type: app</c> (v1 ran it through an interpreter: unsafe).</summary>
    App,

    /// <summary>(f) <c>type: separator</c>.</summary>
    Separator,

    /// <summary>Any other <c>type</c>: kept in the report.</summary>
    Unknown,
}
