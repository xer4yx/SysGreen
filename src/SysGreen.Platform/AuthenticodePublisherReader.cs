using System.Security.Cryptography.X509Certificates;
using SysGreen.Core.Abstractions;
using SysGreen.Core.Knowledge;

namespace SysGreen.Platform;

/// <summary>
/// Reads an executable's Authenticode signer from its embedded signature and returns the
/// publisher common name (via the tested <see cref="PublisherName.Normalize"/>). Humble object:
/// unsigned/missing/unreadable files yield null. Not unit-tested (file + crypto I/O boundary).
/// </summary>
public sealed class AuthenticodePublisherReader : IExecutablePublisherReader
{
    public string? ReadPublisher(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath)) return null;

        try
        {
            // SYSLIB0057 obsoletes cert-data loading in favor of X509CertificateLoader, but that
            // has no equivalent for extracting an *Authenticode signer* from a PE file — this is
            // the correct API for that purpose, so the warning is suppressed locally.
#pragma warning disable SYSLIB0057
            using var cert = X509Certificate.CreateFromSignedFile(executablePath);
#pragma warning restore SYSLIB0057
            return PublisherName.Normalize(cert.Subject);
        }
        catch
        {
            return null; // unsigned, malformed, or access denied
        }
    }
}
