using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;

namespace Clicalo.Application.Tests.UseCases;

/// <summary>A touch keyboard that records what the search asked of it and always accepts.</summary>
internal sealed class FakeTouchKeyboard : ITouchKeyboard
{
    public event EventHandler? OccludedAreaChanged
    {
        add { }
        remove { }
    }

    public PhysicalRect OccludedArea => PhysicalRect.Empty;

    public List<string> Calls { get; } = [];

    public ValueTask<bool> ShowKeyboardAsync(
        WindowToken window,
        CancellationToken cancellationToken
    )
    {
        Calls.Add("show " + window);
        return ValueTask.FromResult(true);
    }

    public ValueTask HideKeyboardAsync(CancellationToken cancellationToken)
    {
        Calls.Add("hide");
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> StartDictationAsync(CancellationToken cancellationToken)
    {
        Calls.Add("dictate");
        return ValueTask.FromResult(true);
    }
}
