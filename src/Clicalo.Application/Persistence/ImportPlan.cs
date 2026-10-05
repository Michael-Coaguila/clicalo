using Clicalo.Domain.Document;

namespace Clicalo.Application.Persistence;

/// <summary>A pure import plan: the document to dispatch and what it changes (blueprint §6.3: the use case plans, a command applies).</summary>
/// <param name="Next">The document after the import.</param>
/// <param name="Summary">What changes.</param>
public sealed record ImportPlan(UserDocument Next, ImportSummary Summary);
