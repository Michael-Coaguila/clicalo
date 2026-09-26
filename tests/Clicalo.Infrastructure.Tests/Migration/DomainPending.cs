namespace Clicalo.Infrastructure.Tests.Migration;

/// <summary>
/// Runs a check that needs the model factories of the domain package (<c>KeyChord.Create</c>,
/// <c>ShortcutLibrary.CreateValidated</c>, <c>SettingsSchema.Defaults</c>…). Until that package is merged into
/// <c>m2/skeleton</c> those members throw <see cref="NotImplementedException"/> and the check is skipped with a visible
/// reason; afterwards it runs as a normal test (M2-ownership, «Orden de integración»).
/// </summary>
internal static class DomainPending
{
    public const string Reason =
        "Needs the domain package (KeyChord.Create, ShortcutLibrary.CreateValidated, SettingsSchema.Defaults, "
        + "CanonicalChord): it runs once m2/domain is merged into m2/skeleton.";

    public static void Run(Action check)
    {
        ArgumentNullException.ThrowIfNull(check);
        try
        {
            check();
        }
        catch (NotImplementedException)
        {
            Assert.Skip(Reason);
        }
    }

    public static async Task RunAsync(Func<Task> check)
    {
        ArgumentNullException.ThrowIfNull(check);
        try
        {
            await check();
        }
        catch (NotImplementedException)
        {
            Assert.Skip(Reason);
        }
    }
}
