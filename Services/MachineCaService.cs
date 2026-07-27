// ============================================================================
// MachineCaService.cs — CA de máquinas para identidad mTLS (Aquafrisch Machine CA)
// ============================================================================
// CA raíz DEDICADA (separada del certificado HTTPS del servidor) usada para:
//   1. FIRMAR los CSR de los equipos cliente durante el enrollment
//      (POST /api/certificate/enroll con código de registro de un solo uso).
//   2. VALIDAR los certificados cliente presentados en el handshake TLS
//      (Kestrel ClientCertificateValidation → MtlsState.ValidateClientCertificate).
//
// La CA se autogenera la primera vez (RSA 4096, BasicConstraints CA=true,
// pathLen=0, 20 años) y se persiste en {ContentRoot}/Data/mtls/machine-ca.pfx.
// ⚠️ La clave privada de la CA NUNCA sale del servidor (el ClientSetup solo
//    envía un CSR; la clave del equipo se genera no-exportable en su store).
// Nota de postura: el PFX se protege por ACL del sistema de ficheros, en línea
// con el resto de secretos del servidor (p. ej. Jwt:Key en appsettings).
// ============================================================================

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SW.PC.API.Backend.Services;

public static class MachineCaService
{
    private const string CaSubject = "CN=Aquafrisch Machine CA, O=Aquafrisch, C=ES";
    private static readonly object _lock = new();
    private static X509Certificate2? _cached;

    /// <summary>Ruta del PFX de la CA: {ContentRoot}/Data/mtls/machine-ca.pfx</summary>
    public static string GetCaPath(string contentRootPath) =>
        Path.Combine(contentRootPath, "Data", "mtls", "machine-ca.pfx");

    /// <summary>
    /// Carga la Machine CA desde disco o la crea si no existe (idempotente, thread-safe).
    /// </summary>
    public static X509Certificate2 LoadOrCreateCa(string contentRootPath)
    {
        lock (_lock)
        {
            if (_cached != null) return _cached;

            var path = GetCaPath(contentRootPath);
            if (File.Exists(path))
            {
                _cached = new X509Certificate2(path, (string?)null, X509KeyStorageFlags.EphemeralKeySet);
                return _cached;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using var rsa = RSA.Create(4096);
            var req = new CertificateRequest(new X500DistinguishedName(CaSubject), rsa,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // CA=true, pathLen=0 → solo puede firmar certificados finales (no sub-CAs)
            req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 0, true));
            req.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.DigitalSignature, true));
            req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

            var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            using var ca = req.CreateSelfSigned(notBefore, notBefore.AddYears(20));

            File.WriteAllBytes(path, ca.Export(X509ContentType.Pfx));

            // Recargar desde el PFX para asegurar clave persistida/usable
            _cached = new X509Certificate2(path, (string?)null, X509KeyStorageFlags.EphemeralKeySet);
            return _cached;
        }
    }

    /// <summary>
    /// Firma un CSR (PKCS#10, PEM o base64/DER) con la Machine CA.
    /// Verifica la firma del CSR (proof-of-possession), IGNORA sus extensiones
    /// y emite un certificado cliente (EKU=clientAuth) con el Subject del CSR.
    /// </summary>
    /// <returns>Certificado emitido (solo parte pública) + CN del sujeto.</returns>
    public static (byte[] CertDer, string MachineName, DateTimeOffset NotAfter) SignCsr(
        string csrText, X509Certificate2 ca, int validityYears = 5)
    {
        var csrDer = DecodeCsr(csrText);

        // LoadSigningRequest valida la firma del CSR por defecto (proof-of-possession).
        // No cargamos extensiones del CSR: las definimos nosotros (EKU clientAuth).
        var req = CertificateRequest.LoadSigningRequest(
            csrDer, HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.Default,
            RSASignaturePadding.Pkcs1);

        var machineName = GetCommonName(req.SubjectName)
            ?? throw new InvalidOperationException("El CSR no contiene CN (nombre de equipo).");

        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, true)); // id-kp-clientAuth

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = notBefore.AddYears(validityYears);
        var serial = RandomNumberGenerator.GetBytes(12);

        using var caKey = ca.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("La Machine CA no tiene clave privada RSA.");
        var generator = X509SignatureGenerator.CreateForRSA(caKey, RSASignaturePadding.Pkcs1);

        using var issued = req.Create(ca.SubjectName, generator, notBefore, notAfter, serial);
        return (issued.Export(X509ContentType.Cert), machineName, notAfter);
    }

    /// <summary>Acepta CSR en PEM ("-----BEGIN ... REQUEST-----") o base64 crudo.</summary>
    private static byte[] DecodeCsr(string csrText)
    {
        if (string.IsNullOrWhiteSpace(csrText))
            throw new InvalidOperationException("CSR vacío.");

        var text = csrText.Trim();
        if (text.Contains("-----BEGIN", StringComparison.Ordinal))
        {
            var lines = text.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("-----", StringComparison.Ordinal));
            text = string.Concat(lines);
        }
        else
        {
            text = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        }

        return Convert.FromBase64String(text);
    }

    private static string? GetCommonName(X500DistinguishedName dn)
    {
        foreach (var rdn in dn.EnumerateRelativeDistinguishedNames())
        {
            if (rdn.HasMultipleElements) continue;
            if (rdn.GetSingleElementType().Value == "2.5.4.3") // CN
                return rdn.GetSingleElementValue();
        }
        return null;
    }
}
