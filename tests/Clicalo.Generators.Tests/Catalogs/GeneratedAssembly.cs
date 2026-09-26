using System.Reflection;
using System.Runtime.Loader;

namespace Clicalo.Generators.Tests.Catalogs;

/// <summary>
/// Emits the output compilation of a generator run and loads it into a collectible context, so tests can read the
/// generated constants as the Domain would.
/// </summary>
internal sealed class GeneratedAssembly : IDisposable
{
    private readonly AssemblyLoadContext _context;

    private GeneratedAssembly(AssemblyLoadContext context, Assembly assembly)
    {
        _context = context;
        Assembly = assembly;
    }

    public Assembly Assembly { get; }

    public static GeneratedAssembly Load(GeneratorRun run)
    {
        using var stream = new MemoryStream();
        var emit = run.Output.Emit(stream);
        emit.Success.ShouldBeTrue(string.Join(Environment.NewLine, emit.Diagnostics));
        stream.Position = 0;
        var context = new AssemblyLoadContext(
            "Clicalo.Catalogs.GeneratorTests",
            isCollectible: true
        );
        return new GeneratedAssembly(context, context.LoadFromStream(stream));
    }

    /// <summary>Value of a public static field or property, such as <c>Clicalo.Domain.Timing.Timings+Touch.LongPress</c>.</summary>
    public object? Static(string typeName, string member)
    {
        var type = Assembly.GetType(typeName, throwOnError: true)!;
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static;
        return type.GetField(member, Flags)?.GetValue(null)
            ?? type.GetProperty(member, Flags)?.GetValue(null)
            ?? throw new MissingMemberException(typeName, member);
    }

    /// <summary>Invokes a public static method, returning its result and its arguments (for out parameters).</summary>
    public (object? Result, object?[] Arguments) Invoke(
        string typeName,
        string method,
        params object?[] arguments
    )
    {
        var type = Assembly.GetType(typeName, throwOnError: true)!;
        var result = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, arguments);
        return (result, arguments);
    }

    public static object? Property(object instance, string name) =>
        instance.GetType().GetProperty(name)!.GetValue(instance);

    public void Dispose() => _context.Unload();
}
