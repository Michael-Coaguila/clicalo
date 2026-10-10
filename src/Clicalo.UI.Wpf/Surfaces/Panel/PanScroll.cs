namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// Scrolling a zone of the panel with the finger without firing what is under it (TAC-004). The panel reads raw
/// pointer input, so WPF never pans its scroll viewers: a contact that goes down inside the zone and moves vertically
/// past the cancel distance scrolls the content by as much as the finger moved, and from then on it is a scroll, not a
/// tap, until it lifts. Pure: positions are physical pixels and the offset is in device-independent pixels.
/// </summary>
public sealed class PanScroll
{
    private uint? _contact;
    private double _originY;
    private double _startOffset;
    private bool _scrolling;

    /// <summary>Whether a contact is being followed.</summary>
    public bool IsTracking => _contact is not null;

    /// <summary>Whether the contact followed already scrolled.</summary>
    public bool IsScrolling => _scrolling;

    /// <summary>A contact went down inside the zone.</summary>
    /// <param name="contact">The pointer id.</param>
    /// <param name="y">Where, in physical pixels.</param>
    /// <param name="offset">The scroll offset of the zone now, in device-independent pixels.</param>
    /// <returns>Whether this contact is followed from now on (the first one wins).</returns>
    public bool Down(uint contact, double y, double offset)
    {
        if (_contact is not null)
        {
            return false;
        }

        _contact = contact;
        _originY = y;
        _startOffset = offset;
        _scrolling = false;
        return true;
    }

    /// <summary>The contact moved.</summary>
    /// <param name="contact">The pointer id.</param>
    /// <param name="y">Where, in physical pixels.</param>
    /// <param name="thresholdPx">The cancel distance, in physical pixels: moving past it starts the scroll.</param>
    /// <param name="scale">Physical pixels per device-independent pixel.</param>
    /// <returns>The offset to scroll to, or <see langword="null"/> while the contact is not scrolling.</returns>
    public double? Move(uint contact, double y, double thresholdPx, double scale)
    {
        if (_contact != contact)
        {
            return null;
        }

        var dy = y - _originY;
        _scrolling |= Math.Abs(dy) > thresholdPx;
        return _scrolling ? _startOffset - (dy / (scale > 0 ? scale : 1)) : null;
    }

    /// <summary>The contact lifted or was cancelled.</summary>
    /// <param name="contact">The pointer id.</param>
    /// <returns>Whether it scrolled: it is not a tap.</returns>
    public bool Up(uint contact)
    {
        if (_contact != contact)
        {
            return false;
        }

        var scrolled = _scrolling;
        _contact = null;
        _scrolling = false;
        return scrolled;
    }
}
