namespace Clicalo.Analyzers;

/// <summary>
/// Metadata names the rules are bound to. They are the contract between the analyzers and the product code:
/// renaming or moving one of these types silently disables its rule, so the names are documented in
/// <c>docs/guides/analyzers.md</c> and must change together with it.
/// </summary>
internal static class KnownTypeNames
{
    // ---- Clícalo contracts --------------------------------------------------------------------------------

    public const string NonActivatingWindow = "Clicalo.UI.Wpf.Windowing.NonActivatingWindow";

    public const string SensitiveAttribute = "Clicalo.Domain.Privacy.SensitiveAttribute";

    public const string SensitiveOfT = "Clicalo.Domain.Privacy.Sensitive`1";

    public const string SecretText = "Clicalo.Domain.Privacy.SecretText";

    public const string Timings = "Clicalo.Domain.Timings";

    public const string DestructiveCommand = "Clicalo.Domain.Commands.IDestructiveCommand";

    public const string ConfirmationToken = "Clicalo.Application.Confirmation.ConfirmationToken";

    public const string TwoStepConfirm = "Clicalo.Application.Confirmation.TwoStepConfirm";

    // ---- Third-party and framework types ----------------------------------------------------------------

    public const string Logger = "Microsoft.Extensions.Logging.ILogger";

    public const string LoggerMessageAttribute =
        "Microsoft.Extensions.Logging.LoggerMessageAttribute";

    public const string Exception = "System.Exception";

    public const string EventSource = "System.Diagnostics.Tracing.EventSource";

    public const string Debug = "System.Diagnostics.Debug";

    public const string Trace = "System.Diagnostics.Trace";

    public const string TimeSpan = "System.TimeSpan";

    public const string DateTime = "System.DateTime";

    public const string DateTimeOffset = "System.DateTimeOffset";

    public const string TimeOnly = "System.TimeOnly";

    public const string Timeout = "System.Threading.Timeout";

    public const string WpfColor = "System.Windows.Media.Color";

    public const string WpfColors = "System.Windows.Media.Colors";

    public const string WpfBrushes = "System.Windows.Media.Brushes";

    public const string AutomationProperties = "System.Windows.Automation.AutomationProperties";
}
