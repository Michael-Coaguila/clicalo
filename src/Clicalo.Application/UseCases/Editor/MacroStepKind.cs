namespace Clicalo.Application.UseCases.Editor;

/// <summary>The four «+» buttons under the steps of a macro (EDI-013).</summary>
public enum MacroStepKind
{
    /// <summary>+ [stKeys]: an empty combination, open in the combination box.</summary>
    Keys,

    /// <summary>+ [stWait]: <c>Timings.Macro.MacroWaitDefault</c> (500 ms).</summary>
    Wait,

    /// <summary>+ [stText]: an empty text.</summary>
    Text,

    /// <summary>+ [stMouse]: a right click.</summary>
    Mouse,
}
