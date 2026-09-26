namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>Who closed a repetition.</summary>
internal enum RepetitionSource
{
    /// <summary>The laboratory counted it from a tap, a command, a lease or a forced activation.</summary>
    Automatic,

    /// <summary>The maintainer closed it with «Funcionó» or «Falló».</summary>
    User,
}
