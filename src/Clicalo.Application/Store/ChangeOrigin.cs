namespace Clicalo.Application.Store;

/// <summary>What produced a document change.</summary>
public enum ChangeOrigin
{
    /// <summary>A dispatched command.</summary>
    Command,

    /// <summary>An undo.</summary>
    Undo,
}
