namespace Clicalo.Domain.Duplicates;

/// <summary>The advice of the expanded repeated card (REP-005), which also decides whether «[moveAlways]» is offered.</summary>
public enum DuplicateAdvice
{
    /// <summary>One appearance is in Always visible: the others are redundant ([dupAdvG]).</summary>
    AlwaysVisible,

    /// <summary>Every appearance has the same name ([dupAdvSame]).</summary>
    SameName,

    /// <summary>Different actions with the same combination; usually fine ([dupAdvDiff]).</summary>
    DifferentNames,
}
