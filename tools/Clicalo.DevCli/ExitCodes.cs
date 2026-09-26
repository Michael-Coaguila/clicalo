namespace Clicalo.DevCli;

/// <summary>Process exit codes shared by every verb.</summary>
internal static class ExitCodes
{
    /// <summary>The verb ran and found nothing wrong.</summary>
    public const int Success = 0;

    /// <summary>The verb ran and found problems (listed on the console).</summary>
    public const int Failure = 1;

    /// <summary>Unknown verb or invalid options; the help is printed.</summary>
    public const int Usage = 2;
}
