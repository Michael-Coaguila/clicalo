namespace Clicalo.Domain.Document;

/// <summary>State of the welcome (BIE-*): finishing it is a single one-way step (<c>FinishOnboarding</c>).</summary>
/// <param name="Completed">Whether the welcome was finished.</param>
public sealed record OnboardingState(bool Completed);
