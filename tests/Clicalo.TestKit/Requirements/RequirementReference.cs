namespace Clicalo.TestKit.Requirements;

/// <summary>A test (or test class) that declares <c>[Trait("Req", Id)]</c>.</summary>
/// <param name="Id">The requirement identifier, for example <c>EJE-003</c>.</param>
/// <param name="Test">The declaring member: <c>Namespace.Class.Method</c>, or <c>Namespace.Class</c> for class-level traits.</param>
public sealed record RequirementReference(string Id, string Test);
