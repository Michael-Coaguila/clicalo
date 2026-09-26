namespace Clicalo.Domain.Migration.V1;

/// <summary>A v1 button as read, every key optional (MIG-002).</summary>
/// <param name="Kind">Which variant.</param>
/// <param name="Label">Visible text (<c>label</c>).</param>
/// <param name="Hotkey">The combination text (<c>hotkey</c>, or <c>action</c> in variant c).</param>
/// <param name="Action">The address or command (<c>action</c> in variants d and e).</param>
/// <param name="Color">The <c>#RRGGBB</c> colour, if any.</param>
/// <param name="RawType">The <c>type</c> text as written, for the report.</param>
public sealed record V1Button(
    V1ButtonKind Kind,
    string Label,
    string? Hotkey,
    string? Action,
    string? Color,
    string? RawType
);
