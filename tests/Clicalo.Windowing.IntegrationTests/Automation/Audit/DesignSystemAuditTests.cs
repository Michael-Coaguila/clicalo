using Clicalo.TestKit.Windows.Rendering;

namespace Clicalo.Windowing.IntegrationTests.Automation.Audit;

/// <summary>
/// The base controls of direction C (D12) on one sheet (<see cref="DesignSystemSpecimen"/>), audited as the real
/// surfaces are (REG-02, REG-06, no text drawn cut) in the three looks, and saved to the gallery with
/// <c>CLICALO_GALLERY</c> in the dark, the light and the high contrast theme.
/// </summary>
public sealed class DesignSystemAuditTests
{
    [Theory]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "TEM-002")]
    [Trait("Req", "ACC-003")]
    [InlineData(AuditLook.Dark)]
    [InlineData(AuditLook.LightLargeText)]
    [InlineData(AuditLook.ContrastLargeText)]
    public void Every_base_control_follows_the_UIA_rules_keeps_44_and_cuts_no_text(
        AuditLook look
    ) =>
        WpfThread.Invoke(() =>
        {
            using var theme = AuditLooks.Theme(look);
            using var host = AuditHost.OfView(DesignSystemSpecimen.Build(), theme);
            SurfaceAudit.ShouldPass(
                host,
                "componentes base (" + look + ")",
                atLeast: 40,
                TouchInput.Wpf,
                "Sistema de diseño"
            );
        });
}
