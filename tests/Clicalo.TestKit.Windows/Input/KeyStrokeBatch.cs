namespace Clicalo.TestKit.Windows.Input;

/// <summary>The rules every injected batch must follow, checked before anything is sent.</summary>
public static class KeyStrokeBatch
{
    /// <summary>Largest batch accepted: tests send minimal batches.</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// Throws <see cref="ArgumentException"/> unless <paramref name="batch"/> is non-empty, at most
    /// <see cref="MaxLength"/> long and balanced: no release without a press, no second press of a held key, and
    /// every press released before the batch ends. A balanced batch cannot leave a key down.
    /// </summary>
    public static void Validate(IReadOnlyList<KeyStroke> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.Count == 0 || batch.Count > MaxLength)
        {
            throw new ArgumentException(
                "A batch holds between 1 and " + MaxLength + " key strokes.",
                nameof(batch)
            );
        }

        var held = new List<(int Mode, int Code)>();
        foreach (var stroke in batch)
        {
            if (stroke.IsUnicode && stroke.IsScanCodeMode)
            {
                throw new ArgumentException(
                    "A key stroke cannot be both Unicode and scan code: " + stroke,
                    nameof(batch)
                );
            }

            if (
                !stroke.IsUnicode
                && !stroke.IsScanCodeMode
                && stroke.VirtualKey == VirtualKeyCode.None
            )
            {
                throw new ArgumentException(
                    "A virtual-key stroke needs a virtual key: " + stroke,
                    nameof(batch)
                );
            }

            var identity = stroke.KeyIdentity;
            if (stroke.IsKeyUp)
            {
                if (!held.Remove(identity))
                {
                    throw new ArgumentException(
                        "Release without a press in the same batch: " + stroke,
                        nameof(batch)
                    );
                }
            }
            else if (held.Contains(identity))
            {
                throw new ArgumentException(
                    "Second press of a key that is already down: " + stroke,
                    nameof(batch)
                );
            }
            else
            {
                held.Add(identity);
            }
        }

        if (held.Count > 0)
        {
            throw new ArgumentException(
                "The batch leaves "
                    + held.Count
                    + " key(s) down; include every release in the same batch.",
                nameof(batch)
            );
        }
    }
}
