using Clicalo.Domain.Library;

namespace Clicalo.Domain.Execution;

/// <summary>What the Shell thread starts for a web or app action (EJE-011), never through an interpreter.</summary>
public abstract record LaunchRequest
{
    private LaunchRequest() { }

    /// <summary>Open an address with the default browser.</summary>
    /// <param name="Address">A valid http or https address.</param>
    public sealed record OpenUrl(Uri Address) : LaunchRequest;

    /// <summary>Start an app, a Store app or a document.</summary>
    /// <param name="Target">What to start.</param>
    public sealed record StartApp(AppTarget Target) : LaunchRequest;
}
