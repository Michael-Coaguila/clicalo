using System.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// A shortcut that shows the discreet answer to an ignored touch (TAC-003): a tile of the panel, or a shortcut of the
/// bar of the Tab view and of its windows. The view draws one slight outline over it while <see cref="IsIgnored"/>.
/// </summary>
public interface IIgnoredTouchState : INotifyPropertyChanged
{
    /// <summary>Whether the touch filter just ignored a touch on it.</summary>
    bool IsIgnored { get; }
}
