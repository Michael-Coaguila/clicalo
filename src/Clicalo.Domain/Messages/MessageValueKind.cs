namespace Clicalo.Domain.Messages;

/// <summary>What a <see cref="MessageValue"/> holds.</summary>
public enum MessageValueKind
{
    /// <summary>Verbatim text, shown as is (user data such as a profile name).</summary>
    Text,

    /// <summary>A nested message, localized in the same language as the message that contains it.</summary>
    Message,

    /// <summary>A whole number.</summary>
    WholeNumber,

    /// <summary>A decimal number.</summary>
    DecimalNumber,
}
