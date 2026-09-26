namespace Clicalo.Build;

/// <summary>A second-level section of the failure report.</summary>
/// <param name="Heading">Plain-text heading, read by Narrator when navigating by headings.</param>
/// <param name="Body">Markdown body, already escaped.</param>
internal sealed record ReportSection(string Heading, string Body);
