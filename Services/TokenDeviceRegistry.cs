// ============================================================================
// TokenDeviceRegistry.cs — Cache en memoria de tokens de dispositivo válidos
// ============================================================================
// Los tokens viven en la BD del proyecto activo (project.db) pero la resolución
// ocurre en rutas calientes (OriginContext.FromHttpContext, por petición), así
// que se cachean los hashes válidos en memoria:
//   - Carga al arranque (Program.cs) y en cada cambio de proyecto.
//   - Recarga tras cada mutación (crear/revocar/eliminar en CertificateController).
//   - Revocación efectiva al instante: la mutación recarga el cache.
// LastUsedAt se actualiza con throttle (>=10 min) y fire-and-forget para no
// penalizar el request. Gated por MtlsState.Enabled en el llamador.
// ============================================================================

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;

namespace SW.PC.API.Backend.Services;

public static class TokenDeviceRegistry
{
    private static readonly ConcurrentDictionary<string, (int Id, string Name)> _byHash = new();
    private static readonly ConcurrentDictionary<int, DateTime> _lastTouch = new();
    private static IProjectDbContextFactory? _dbFactory;
    private static readonly TimeSpan TouchThrottle = TimeSpan.FromMinutes(10);

    /// <summary>Número de dispositivos activos cacheados (diagnóstico).</summary>
    public static int ActiveCount => _byHash.Count;

    public static void Initialize(IProjectDbContextFactory dbFactory) => _dbFactory = dbFactory;

    /// <summary>Recarga el cache desde la BD del proyecto activo (solo no revocados).</summary>
    public static async Task ReloadAsync()
    {
        var factory = _dbFactory;
        if (factory == null) return;
        try
        {
            await using var db = factory.CreateDbContext();
            // Idempotente: garantiza la tabla aunque la init general aún no haya corrido
            await AquafrischDbContextFactory.EnsureTokenDevicesTableAsync(db);
            var devices = await db.TokenDevices
                .Where(d => !d.Revoked)
                .Select(d => new { d.Id, d.TokenHash, d.Name })
                .ToListAsync();

            _byHash.Clear();
            foreach (var d in devices)
                _byHash[d.TokenHash] = (d.Id, d.Name);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TokenDevices] ReloadAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// Resuelve un token en claro al nombre del dispositivo registrado.
    /// null si el token no existe o está revocado.
    /// </summary>
    public static string? Resolve(string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        if (!_byHash.TryGetValue(HashToken(rawToken), out var device)) return null;
        TouchLastUsed(device.Id);
        return device.Name;
    }

    /// <summary>SHA256 hex del token normalizado (trim + lowercase).</summary>
    public static string HashToken(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Genera un token nuevo (160 bits aleatorios, hex — URL-safe).</summary>
    public static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();

    private static void TouchLastUsed(int id)
    {
        var factory = _dbFactory;
        if (factory == null) return;
        var now = DateTime.Now;
        if (_lastTouch.TryGetValue(id, out var last) && now - last < TouchThrottle) return;
        _lastTouch[id] = now;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var db = factory.CreateDbContext();
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE TokenDevices SET LastUsedAt = {0} WHERE Id = {1}", now, id);
            }
            catch { /* best-effort: no afecta al request */ }
        });
    }
}
