namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// The finger that scrolls a zone of a <see cref="TouchSurface"/> (TAC-004): the pointer layer of the product consumes
/// the touch, so the surface follows it itself. A contact that goes down on the zone and moves along it past the
/// threshold (the «cancel if you slide» distance, PAN-004) scrolls by as much as it moves and is no longer a tap, so
/// nothing under it is activated; one that stays within the threshold scrolls nothing and stays a tap. Pure: the
/// surface gives it the samples and applies the offset.
/// </summary>
public sealed class TouchSurfaceScroll
{
    private uint? _contact;
    private int _origin;
    private double _startOffset;
    private bool _scrolling;

    /// <summary>Whether a contact is scrolling the zone now.</summary>
    public bool IsScrolling => _scrolling;

    /// <summary>A contact went down on the zone; the newest one takes over.</summary>
    /// <param name="contact">The pointer id.</param>
    /// <param name="position">Where, along the scroll axis, in physical pixels.</param>
    /// <param name="offset">The scroll offset of the zone now, in logical pixels.</param>
    public void Down(uint contact, int position, double offset)
    {
        _contact = contact;
        _origin = position;
        _startOffset = offset;
        _scrolling = false;
    }

    /// <summary>The contact moved.</summary>
    /// <param name="contact">The pointer id.</param>
    /// <param name="position">Where, along the scroll axis, in physical pixels.</param>
    /// <param name="thresholdPx">The distance past which it scrolls, in physical pixels.</param>
    /// <param name="scale">Physical pixels per logical pixel.</param>
    /// <returns>The scroll offset to apply, in logical pixels; <see langword="null"/> while it has not scrolled.</returns>
    public double? Move(uint contact, int position, double thresholdPx, double scale)
    {
        if (_contact != contact)
        {
            return null;
        }

        var moved = position - _origin;
        _scrolling |= Math.Abs(moved) > thresholdPx;
        return _scrolling ? _startOffset - (moved / (scale > 0 ? scale : 1)) : null;
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
