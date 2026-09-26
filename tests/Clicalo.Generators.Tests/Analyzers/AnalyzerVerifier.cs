using System.Globalization;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Clicalo.Generators.Tests.Analyzers;

/// <summary>
/// Runs one Clícalo analyzer over test code plus the stubs of the product types it is bound to (the real types
/// arrive in M2; the rules match them by metadata name). Expected diagnostics are written as markup:
/// <c>{|CLC0001:panel.Show()|}</c>.
/// </summary>
internal static class AnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public static Task VerifyAsync(string source, params string[] stubs) =>
        Create(source, stubs).RunAsync(TestContext.Current.CancellationToken);

    public static CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> Create(
        string source,
        params string[] stubs
    )
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = RuntimeReferences.None,
            // A rule may have several descriptors under one id (CLC0006 text and color); markup checks id and span.
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        };
        test.TestState.AdditionalReferences.AddRange(RuntimeReferences.Framework);
        for (var i = 0; i < stubs.Length; i++)
        {
            test.TestState.Sources.Add(
                (string.Create(CultureInfo.InvariantCulture, $"/0/Stubs{i}.cs"), stubs[i])
            );
        }

        return test;
    }
}
