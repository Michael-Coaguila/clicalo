namespace Clicalo.Tools.SpikeLab.Input;

/// <summary>What the laboratory injector did with a batch.</summary>
/// <param name="Sent">True when the batch was handed to <c>SendInput</c> whole.</param>
/// <param name="Reason">Why it was refused (nothing was injected), or null when sent.</param>
internal sealed record InjectionOutcome(bool Sent, string? Reason)
{
    /// <summary>The batch was sent.</summary>
    public static InjectionOutcome Done { get; } = new(true, null);

    /// <summary>Nothing was injected because of <paramref name="reason"/>.</summary>
    public static InjectionOutcome Refused(string reason) => new(false, reason);
}
