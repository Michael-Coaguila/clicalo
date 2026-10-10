namespace Clicalo.Domain.Document;

/// <summary>State of the welcome (BIE-*): finishing it is a single one-way step (<c>FinishOnboarding</c>).</summary>
/// <param name="Completed">Whether the welcome was finished.</param>
public sealed record OnboardingState(bool Completed)
{
    /// <summary>
    /// What the last welcome answered (BIE-010, schema 1.1); <see langword="null"/> when no welcome recorded them (a new
    /// document, or one written by a 1.0 version): a repeated welcome then reads what it can from the settings.
    /// </summary>
    public WelcomeAnswers? Answers { get; init; }
}
