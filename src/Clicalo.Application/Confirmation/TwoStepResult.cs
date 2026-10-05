namespace Clicalo.Application.Confirmation;

/// <summary>What a tap on a destructive control produced.</summary>
public abstract record TwoStepResult
{
    private TwoStepResult() { }

    /// <summary>First tap: the control shows «[delConfirm]» until <paramref name="Until"/>.</summary>
    /// <param name="Subject">What is armed.</param>
    /// <param name="Until">When it disarms.</param>
    public sealed record Armed(ConfirmationSubject Subject, DateTimeOffset Until) : TwoStepResult;

    /// <summary>Second tap in time on the same subject.</summary>
    /// <param name="Token">The proof to dispatch the operation with.</param>
    public sealed record Confirmed(ConfirmationToken Token) : TwoStepResult;
}
