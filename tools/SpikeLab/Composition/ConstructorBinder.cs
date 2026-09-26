using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.Tools.SpikeLab.Composition;

/// <summary>
/// Creates a type of the product whose constructor the laboratory cannot know while the M1 packages are being
/// integrated (<c>ForegroundOrchestrator</c> is written by the foreground package): it picks the public constructor
/// with the most parameters that the given services satisfy, by type. Loggers get their null implementations.
/// </summary>
internal static class ConstructorBinder
{
    /// <summary>
    /// Creates <paramref name="type"/> from <paramref name="services"/>, or says which parameter could not be
    /// satisfied. An exception thrown by the constructor itself propagates unwrapped.
    /// </summary>
    public static BindResult Create(Type type, IReadOnlyList<object> services)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(services);
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .ToArray();
        if (constructors.Length == 0)
        {
            return BindResult.Missing(type.Name + " has no public constructor.");
        }

        string? problem = null;
        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            var arguments = new object?[parameters.Length];
            var complete = true;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (TryResolve(parameters[i], services, out var value))
                {
                    arguments[i] = value;
                }
                else
                {
                    problem ??=
                        "No service for parameter «"
                        + parameters[i].Name
                        + "» ("
                        + parameters[i].ParameterType.Name
                        + ") of "
                        + type.Name
                        + ".";
                    complete = false;
                    break;
                }
            }

            if (complete)
            {
                try
                {
                    return BindResult.Created(constructor.Invoke(arguments));
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    throw;
                }
            }
        }

        return BindResult.Missing(problem ?? type.Name + " could not be created.");
    }

    private static bool TryResolve(
        ParameterInfo parameter,
        IReadOnlyList<object> services,
        out object? value
    )
    {
        var type = parameter.ParameterType;
        foreach (var service in services)
        {
            if (type.IsInstanceOfType(service))
            {
                value = service;
                return true;
            }
        }

        if (type == typeof(ILogger))
        {
            value = NullLogger.Instance;
            return true;
        }

        if (type == typeof(ILoggerFactory))
        {
            value = NullLoggerFactory.Instance;
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ILogger<>))
        {
            value = Activator.CreateInstance(
                typeof(NullLogger<>).MakeGenericType(type.GetGenericArguments())
            );
            return true;
        }

        if (parameter.HasDefaultValue)
        {
            value = parameter.DefaultValue;
            return true;
        }

        value = null;
        return false;
    }
}
