namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// The file operations of the write protocol and the load chain (blueprint §6.5), one call per step so S11 can fail
/// each step on its own (<c>CrashingFileSystem</c>). The disk implementation is <see cref="AtomicFile"/>'s; nothing
/// else in the product writes files (§4.4).
/// </summary>
internal interface IAtomicFileSystem
{
    /// <summary>Whether <paramref name="path"/> exists.</summary>
    /// <param name="path">A file.</param>
    bool Exists(string path);

    /// <summary>Reads the whole file, or <see langword="null"/> when it does not exist.</summary>
    /// <param name="path">A file.</param>
    byte[]? ReadAllBytesOrNull(string path);

    /// <summary>Creates the folder and its parents when missing.</summary>
    /// <param name="path">A folder.</param>
    void CreateDirectory(string path);

    /// <summary>
    /// Creates or truncates <paramref name="path"/>, writes <paramref name="content"/> with
    /// <c>FILE_FLAG_WRITE_THROUGH</c> and flushes it to the disk (<c>FlushFileBuffers</c>).
    /// </summary>
    /// <param name="path">The temporary file.</param>
    /// <param name="content">The whole content.</param>
    void WriteThrough(string path, ReadOnlySpan<byte> content);

    /// <summary><c>ReplaceFileW(target, replacement, backup)</c>: the old target becomes <paramref name="backup"/>.</summary>
    /// <param name="target">The file replaced.</param>
    /// <param name="replacement">The file that takes its place.</param>
    /// <param name="backup">Where the old target goes (<c>.prev</c>).</param>
    void Replace(string target, string replacement, string backup);

    /// <summary>Renames <paramref name="source"/> to <paramref name="target"/>, which must not exist.</summary>
    /// <param name="source">The file moved.</param>
    /// <param name="target">Its new name.</param>
    void Move(string source, string target);

    /// <summary>Deletes a file when it exists (only obsolete copies: rotated backups, a superseded emergency copy).</summary>
    /// <param name="path">A file.</param>
    void Delete(string path);

    /// <summary>The files of <paramref name="directory"/> that match <paramref name="pattern"/>, or none.</summary>
    /// <param name="directory">A folder.</param>
    /// <param name="pattern">A file pattern such as <c>*.json</c>.</param>
    IReadOnlyList<string> Files(string directory, string pattern);
}
