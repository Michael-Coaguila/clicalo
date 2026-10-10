namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>«Volver a la versión anterior» (ACT-005): two taps, in warn while armed.</summary>
/// <param name="Title">[rollbackT].</param>
/// <param name="Description">[rollbackD].</param>
/// <param name="Button">[rollbackBtn], or [delConfirm] while armed.</param>
/// <param name="Armed">Whether the first tap armed it.</param>
public sealed record RollbackModel(string Title, string Description, string Button, bool Armed);
