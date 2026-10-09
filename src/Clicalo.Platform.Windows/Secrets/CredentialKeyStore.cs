using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Clicalo.Application.Ports;
using Clicalo.Domain.Privacy;
using Windows.Win32;
using Windows.Win32.Security.Credentials;

namespace Clicalo.Platform.Windows.Secrets;

/// <summary>
/// <see cref="IAiKeyStore"/> in the Windows Credential Manager (ADR-0008, user decision D5): a generic credential of
/// the current user, persisted on this machine only (it never roams with the profile), stored as UTF-16. The key is
/// never written to the document, the backups or the log; the buffers that carried it are cleared.
/// </summary>
public sealed class CredentialKeyStore : IAiKeyStore
{
    private const string UserName = "Clicalo";

    // ERROR_NOT_FOUND: there was no key to delete.
    private const int ErrorNotFound = 1168;

    /// <summary>Creates the store.</summary>
    /// <param name="target">The target name, such as <c>Clicalo/ai/anthropic</c>.</param>
    public CredentialKeyStore(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        Target = target;
    }

    /// <inheritdoc />
    public string Target { get; }

    /// <inheritdoc />
    public bool HasKey() => Read() is not null;

    /// <inheritdoc />
    public unsafe bool Save(Sensitive<string> key)
    {
        if (string.IsNullOrWhiteSpace(key.Value))
        {
            return false;
        }

        var blob = Encoding.Unicode.GetBytes(key.Value.Trim());
        try
        {
            fixed (char* target = Target)
            fixed (char* user = UserName)
            fixed (byte* data = blob)
            {
                var credential = new CREDENTIALW
                {
                    Type = CRED_TYPE.CRED_TYPE_GENERIC,
                    TargetName = target,
                    CredentialBlobSize = (uint)blob.Length,
                    CredentialBlob = data,
                    Persist = CRED_PERSIST.CRED_PERSIST_LOCAL_MACHINE,
                    UserName = user,
                };
                return PInvoke.CredWrite(&credential, 0);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(blob);
        }
    }

    /// <inheritdoc />
    public unsafe Sensitive<string>? Read()
    {
        CREDENTIALW* credential = null;
        fixed (char* target = Target)
        {
            if (!PInvoke.CredRead(target, CRED_TYPE.CRED_TYPE_GENERIC, 0, &credential))
            {
                return null;
            }
        }

        try
        {
            var size = (int)credential->CredentialBlobSize;
            if (size <= 0 || credential->CredentialBlob is null)
            {
                return null;
            }

            var text = new string((char*)credential->CredentialBlob, 0, size / sizeof(char));
            return text.Length == 0 ? null : new Sensitive<string>(text, RedactionKind.Secret);
        }
        finally
        {
            if (credential->CredentialBlob is not null)
            {
                new Span<byte>(credential->CredentialBlob, (int)credential->CredentialBlobSize).Clear();
            }

            PInvoke.CredFree(credential);
        }
    }

    /// <inheritdoc />
    public unsafe bool Delete()
    {
        fixed (char* target = Target)
        {
            if (PInvoke.CredDelete(target, CRED_TYPE.CRED_TYPE_GENERIC, 0))
            {
                return true;
            }
        }

        return Marshal.GetLastPInvokeError() == ErrorNotFound;
    }
}
