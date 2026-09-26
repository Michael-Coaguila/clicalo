namespace Clicalo.Generators.Tokens;

/// <summary>A motion duration and its value when the user (or Windows) asks for reduced motion (TEM-006).</summary>
internal sealed class MotionModel(string key, int milliseconds, int reducedMilliseconds, string use)
{
    public string Key { get; } = key;

    public int Milliseconds { get; } = milliseconds;

    public int ReducedMilliseconds { get; } = reducedMilliseconds;

    public string Use { get; } = use;
}
