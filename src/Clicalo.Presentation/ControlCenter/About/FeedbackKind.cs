namespace Clicalo.Presentation.ControlCenter.About;

/// <summary>The four kinds of feedback (ACE-002), in the order of the 2 × 2 grid.</summary>
public enum FeedbackKind
{
    /// <summary>Sugerencia (<c>lightbulb</c>), the default.</summary>
    Suggestion,

    /// <summary>Algo falla (<c>bug_report</c>).</summary>
    Bug,

    /// <summary>Nueva función (<c>add_circle</c>).</summary>
    Idea,

    /// <summary>Agradecimiento (<c>favorite</c>).</summary>
    Thanks,
}
