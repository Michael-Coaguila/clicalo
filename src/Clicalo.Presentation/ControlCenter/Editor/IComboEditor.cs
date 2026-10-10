using Clicalo.Domain.Keys;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>
/// What the combination box asks of whoever owns the combination it shows (EDI-007 to EDI-010): the editor of a
/// shortcut, or a row of the preview of Plantillas (PLA-016).
/// </summary>
public interface IComboEditor
{
    /// <summary>A key of the picker or a modifier (EDI-008).</summary>
    /// <param name="key">The key.</param>
    void TapKey(KeyId key);

    /// <summary>× of a key chip (EDI-007).</summary>
    /// <param name="index">The zero-based key.</param>
    void RemoveKey(int index);

    /// <summary>⌫ (EDI-007).</summary>
    void RemoveLastKey();

    /// <summary>↺ Limpiar (EDI-007).</summary>
    void ClearKeys();

    /// <summary>A group of the key picker (EDI-008).</summary>
    /// <param name="group">The group.</param>
    void ChooseGroup(KeyGroup group);

    /// <summary>[keepOld] (REP-006).</summary>
    void KeepOldCombination();

    /// <summary>[done]: closes the edition of the step or of the row.</summary>
    void StopEditingStep();

    /// <summary>«Grabar con teclado» or [cancel] while it records (EDI-010).</summary>
    void ToggleRecording();
}
