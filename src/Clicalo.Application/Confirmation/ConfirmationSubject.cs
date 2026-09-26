namespace Clicalo.Application.Confirmation;

/// <summary>What a two-step confirmation is about: the second tap must be on the same operation and target.</summary>
/// <param name="Operation">The operation (<c>DeleteShortcut</c>, <c>RestoreBackup</c>…).</param>
/// <param name="Target">The id it applies to.</param>
public readonly record struct ConfirmationSubject(string Operation, string Target);
