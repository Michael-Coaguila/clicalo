namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// A program installed on the computer, as «Elegir programa» offers it (EDI-014): desktop programs and Store apps
/// alike, as the Start menu lists them.
/// </summary>
/// <param name="Name">The name the Start menu shows.</param>
/// <param name="Target">
/// The text of the App field that opens it: <c>shell:AppsFolder\&lt;AUMID&gt;</c>, which <see cref="Targets.ParseApp"/>
/// reads as a Store app target and the launcher opens without a command interpreter.
/// </param>
public sealed record InstalledProgram(string Name, string Target);
