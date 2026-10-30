namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>«Confirmación al tocar» (GEN-011): sound and flash, both on by default.</summary>
/// <param name="Caption">[secFeedback].</param>
/// <param name="Sound">[fbSound].</param>
/// <param name="Flash">[fbFlash].</param>
public sealed record FeedbackModel(string Caption, SwitchItem Sound, SwitchItem Flash);
