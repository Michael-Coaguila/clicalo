using System.Runtime.CompilerServices;

// cl i18n-check runs exactly the validation of the generator (blueprint §8.5), so it reuses its Roslyn-free core.
[assembly: InternalsVisibleTo("Clicalo.DevCli")]
