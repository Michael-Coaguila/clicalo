using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// The product's <see cref="LowLevelInjector"/> behind the safety rules of the desktop tests (M2-ownership.md, rule 6):
/// before every batch the probe must own the foreground, the batch must be balanced (every key and button it presses
/// it also releases), and outside continuous integration it never holds right Ctrl or right Alt (the maintainer's
/// dictation and voice tools capture them). Anything else is refused before a single event is sent.
/// </summary>
/// <param name="probeWindow">The only window input may go to.</param>
/// <param name="allowUnbalanced">Continuous integration only: batches that leave something down (the chaos tests).</param>
internal sealed class GuardedProbeSender(nint probeWindow, bool allowUnbalanced = false)
    : ILowLevelSender
{
    private readonly LowLevelInjector _inner = new();

    public int SentBatches { get; private set; }

    public SendResult Send(ReadOnlySpan<LowLevelInput> inputs)
    {
        EnsureProbeInFront();
        if (!allowUnbalanced)
        {
            EnsureBalanced(inputs);
        }

        EnsureNoReservedKey(inputs);
        EnsureProbeInFront();
        SentBatches++;
        return _inner.Send(inputs);
    }

    private void EnsureProbeInFront()
    {
        if (!ForegroundWindows.IsForeground(probeWindow))
        {
            throw new InjectionRefusedException(
                "The probe is not in the foreground ("
                    + ForegroundWindows.Describe()
                    + "). Nothing was injected."
            );
        }
    }

    private static void EnsureBalanced(ReadOnlySpan<LowLevelInput> inputs)
    {
        var down = new HashSet<PhysicalKey>();
        var buttons = LedgerMouseButtons.None;
        foreach (var input in inputs)
        {
            switch (input.Kind)
            {
                case LowLevelInputKind.KeyDown:
                    down.Add(input.Key);
                    break;
                case LowLevelInputKind.KeyUp:
                    down.Remove(input.Key);
                    break;
                case LowLevelInputKind.MouseButtonDown:
                    buttons |= input.Button;
                    break;
                case LowLevelInputKind.MouseButtonUp:
                    buttons &= ~input.Button;
                    break;
            }
        }

        if (down.Count > 0 || buttons != LedgerMouseButtons.None)
        {
            throw new InjectionRefusedException(
                "The batch would leave something down; desktop tests only send balanced batches. Nothing was injected."
            );
        }
    }

    private static void EnsureNoReservedKey(ReadOnlySpan<LowLevelInput> inputs)
    {
        if (DesktopTestEnvironment.IsContinuousIntegration)
        {
            return;
        }

        foreach (var input in inputs)
        {
            if (input.Kind is not (LowLevelInputKind.KeyDown or LowLevelInputKind.KeyUp))
            {
                continue;
            }

            var key = input.Key;
            var extended =
                (key.Attributes & LedgerKeyAttributes.Extended) != LedgerKeyAttributes.None;
            if (key.Vk is 0xA3 or 0xA5 || (extended && key.Scan is 0x1D or 0x38))
            {
                throw new InjectionRefusedException(
                    "Right Ctrl and AltGr are only injected in continuous integration. Nothing was injected."
                );
            }
        }
    }
}
