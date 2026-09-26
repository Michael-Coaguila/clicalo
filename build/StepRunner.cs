namespace Clicalo.Build;

/// <summary>
/// Runs the named steps of a verb, announces each one with a short line and remembers the first failure,
/// so the final line can say where the run stopped. In GitHub Actions each step is a collapsible group.
/// </summary>
internal sealed class StepRunner(TextWriter output, bool gitHubActions)
{
    /// <summary>The first failure of the run, or <see langword="null"/>.</summary>
    public StepFailure? Failure { get; private set; }

    /// <summary>Runs <paramref name="action"/> as the step <paramref name="step"/>.</summary>
    public async Task RunAsync(string step, string purpose, Func<Task> action)
    {
        var header = Messages.StepStarted(step, purpose);
        await output.WriteLineAsync(gitHubActions ? "::group::" + header : header);
        try
        {
            await action();
        }
        catch (StepFailedException exception)
        {
            Failure ??= new StepFailure(step, exception.Details);
            throw;
        }
        catch (Exception exception) when (Failure is null)
        {
            Failure = new StepFailure(step, FailureDetails.FromException(exception));
            throw;
        }
        finally
        {
            if (gitHubActions)
            {
                await output.WriteLineAsync("::endgroup::");
            }
        }
    }
}
