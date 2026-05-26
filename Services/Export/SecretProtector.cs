// ============================================================================
// SecretProtector.cs — Cifrado de secretos at-rest (CRA compliance)
// ============================================================================
// Usa ASP.NET Core Data Protection. En Windows, el key-ring se cifra
// automáticamente con DPAPI (LocalMachine). No requiere NuGet adicional.
//
// Purpose fijo "Aquafrisch.Export.SmtpPassword.v1" → si cambias el algoritmo
// puedes incrementar la versión para forzar re-cifrado controlado.
// ============================================================================

using Microsoft.AspNetCore.DataProtection;

namespace SW.PC.API.Backend.Services.Export;

public interface ISecretProtector
{
    /// <summary>Cifra plain. Devuelve "" si plain es null/empty.</summary>
    string Protect(string? plain);

    /// <summary>Descifra cipher. Devuelve null si no se puede.</summary>
    string? Unprotect(string? cipher);
}

public sealed class SecretProtector : ISecretProtector
{
    private const string Purpose = "Aquafrisch.Export.SmtpPassword.v1";
    private readonly IDataProtector _protector;

    public SecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        return _protector.Protect(plain);
    }

    public string? Unprotect(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return null;
        try { return _protector.Unprotect(cipher); }
        catch { return null; }
    }
}
