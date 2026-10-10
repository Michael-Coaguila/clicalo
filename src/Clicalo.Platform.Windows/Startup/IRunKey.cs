namespace Clicalo.Platform.Windows.Startup;

/// <summary>The values of the user's <c>Run</c> key; tests use a dictionary.</summary>
internal interface IRunKey
{
    /// <summary>The value called <paramref name="name"/>, or null.</summary>
    /// <param name="name">The value name.</param>
    string? Read(string name);

    /// <summary>Writes the value <paramref name="name"/>.</summary>
    /// <param name="name">The value name.</param>
    /// <param name="command">The command line Windows runs at sign-in.</param>
    void Write(string name, string command);

    /// <summary>Removes the value <paramref name="name"/>, if there is one.</summary>
    /// <param name="name">The value name.</param>
    void Delete(string name);
}
