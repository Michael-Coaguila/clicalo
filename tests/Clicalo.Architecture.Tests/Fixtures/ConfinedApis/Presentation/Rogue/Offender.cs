using System.Diagnostics;
using Clicalo.Architecture.Tests.Fixtures.ConfinedApis.External.Windows.Win32;

namespace Clicalo.Architecture.Tests.Fixtures.ConfinedApis.Presentation.Rogue;

/// <summary>Fixture: every confined API used where it is not allowed. Never executed.</summary>
public static class Offender
{
    public static bool TakeForeground() =>
        PInvoke.SetForegroundWindow(1) && PInvoke.AttachThreadInput(1, 2, true);

    public static uint Inject() => PInvoke.SendInput(1);

    public static void Quit() => Environment.Exit(1);

    public static DateTime Now() => DateTime.Now;

    public static void Nap() => Thread.Sleep(TimeSpan.FromMilliseconds(1));

    public static Task Wait() => Task.Delay(TimeSpan.FromSeconds(1));

    public static int Block(Task<int> task) => task.Result;

    public static void Print() => Console.WriteLine("confined");

    public static void Save(string path) => File.WriteAllText(path, "data");

    public static Stream Open(string path) => new FileStream(path, FileMode.Create);

    public static Process? Run() => Process.Start("notepad.exe");
}
