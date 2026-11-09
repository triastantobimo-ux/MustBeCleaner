namespace MustBeCleaner.Adapters;

/// <summary>
/// Verifies an ECDSA P-256 / SHA-256 detached signature (IEEE P1363, 64 bytes) against one locally
/// configured trusted public key (SubjectPublicKeyInfo DER). No certificate chain, no network (R23.5, ADR 0032).
/// </summary>
public sealed class EcdsaProvenanceVerifier : IProvenanceVerifier
{
    private readonly byte[] _trustedKey;

    public EcdsaProvenanceVerifier(byte[] trustedPublicKeySpki)
    {
        ArgumentNullException.ThrowIfNull(trustedPublicKeySpki);
        _trustedKey = (byte[])trustedPublicKeySpki.Clone();
    }

    public bool Verify(ReadOnlyMemory<byte> manifest, ReadOnlyMemory<byte> signature)
    {
        try
        {
            using var key = System.Security.Cryptography.ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(_trustedKey, out _);
            return key.VerifyData(
                manifest.Span, signature.Span,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
