// Run with: dotnet script Tools/GenerateTestCrl.csx
// Or: dotnet run -- generate-crl (if integrated)
// Generates a valid test CRL file

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

// 1. Create a self-signed CA certificate
using var rsa = RSA.Create(2048);
var req = new CertificateRequest("CN=Test CA Alstom", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.KeyCertSign, true));

var caCert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(10));

// 2. Build a CRL with 0 revoked certs
var crlBuilder = new CertificateRevocationListBuilder();
var crlNumber = new byte[] { 0x01 }; // CRL number = 1
var crlBytes = crlBuilder.Build(caCert, 1, DateTimeOffset.UtcNow.AddDays(30), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, 1);

// 3. Save CRL
var outputDir = Path.Combine(Path.GetTempPath(), "crl-test");
Directory.CreateDirectory(outputDir);
var crlPath = Path.Combine(outputDir, "test.crl");
File.WriteAllBytes(crlPath, crlBytes);
Console.WriteLine($"Valid CRL generated: {crlPath} ({crlBytes.Length} bytes)");
Console.WriteLine($"Issuer: CN=Test CA Alstom");
Console.WriteLine($"Next update: {DateTimeOffset.UtcNow.AddDays(30):yyyy-MM-dd}");
