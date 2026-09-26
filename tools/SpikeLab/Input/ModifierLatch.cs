namespace Clicalo.Tools.SpikeLab.Input;

/// <summary>
/// The latched modifiers of «Mayús» and «Mantener Ctrl». They are applied to the NEXT chord inside its own balanced
/// batch, so the laboratory never leaves a key down between batches: this is the gesture-free equivalent of a hold
/// (blueprint §8.6, «mantener → Toggle enclavado») and it keeps every release in the same batch as its press.
/// </summary>
internal sealed class ModifierLatch
{
    /// <summary>Raised after every change.</summary>
    public event EventHandler? Changed;

    /// <summary>State of «Mayús».</summary>
    public LatchState Shift { get; private set; }

    /// <summary>True while «Mantener Ctrl» is on.</summary>
    public bool Control { get; private set; }

    /// <summary>True when something is latched.</summary>
    public bool IsAnyLatched => Shift != LatchState.Off || Control;

    /// <summary>«Mayús»: Off → Once → Locked → Off.</summary>
    public LatchState CycleShift()
    {
        Shift = Shift switch
        {
            LatchState.Off => LatchState.Once,
            LatchState.Once => LatchState.Locked,
            _ => LatchState.Off,
        };
        OnChanged();
        return Shift;
    }

    /// <summary>«Mantener Ctrl»: toggles the latch.</summary>
    public bool ToggleControl()
    {
        Control = !Control;
        OnChanged();
        return Control;
    }

    /// <summary>
    /// The modifiers to add to the next chord. A «once» Shift is consumed: it goes back to Off after this chord.
    /// </summary>
    public LabModifiers Consume()
    {
        var modifiers = LabModifiers.None;
        if (Shift != LatchState.Off)
        {
            modifiers |= LabModifiers.Shift;
        }

        if (Control)
        {
            modifiers |= LabModifiers.Control;
        }

        if (Shift == LatchState.Once)
        {
            Shift = LatchState.Off;
            OnChanged();
        }

        return modifiers;
    }

    /// <summary>«Soltar todo»: nothing stays latched.</summary>
    public void Clear()
    {
        if (!IsAnyLatched)
        {
            return;
        }

        Shift = LatchState.Off;
        Control = false;
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
