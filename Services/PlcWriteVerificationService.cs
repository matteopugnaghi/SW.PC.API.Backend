// ============================================================================
// PlcWriteVerificationService.cs - Escritura verificada al PLC (ADS)
// ============================================================================
// Problema que resuelve (incidente de campo, nov-2026):
//   • ITwinCATService.WriteVariableAsync devuelve FALSE en caso de error ADS (no lanza).
//     Varias rutas hacían `await Write(...); successCount++;` → una escritura rechazada
//     se contaba como OK y la API respondía "escrito correctamente".
//   • Un valor que nunca se escribe (p.ej. parámetro no persistido en DB) no genera
//     ningún error → invisible en logs.
//
// Solución:
//   1. PlcWriteBatch.WriteAsync: comprueba el resultado del driver y LANZA si es false,
//      de modo que los catch existentes contabilicen el error.
//   2. VerifyAsync: RELEE cada variable escrita y la compara con el valor enviado.
//      Cualquier discrepancia se registra en:
//        - L3 (ILogger Error/Warning → visor de logs, no retentivo)
//        - L2 (OperationLog SQLite, retentivo) con acción PlcWriteVerifyFailed y detalle JSON
//   3. LogIntegrityWarningsAsync: avisos de coherencia DB↔Excel↔PLC → L3 + L2 (IntegrityWarning)
//
// La verificación debe ejecutarse ANTES de activar el trigger de escritura del PLC,
// mientras los valores aún no han sido consumidos/modificados por el programa PLC.
// ============================================================================

using System.Globalization;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// El driver ADS rechazó la escritura (WriteVariableAsync devolvió false).
    /// </summary>
    public sealed class PlcWriteException : Exception
    {
        public string Variable { get; }

        public PlcWriteException(string variable)
            : base($"Escritura rechazada por el driver ADS para '{variable}' (ver log TwinCATService)")
        {
            Variable = variable;
        }
    }

    public sealed record PlcWriteRecord(string Variable, object Value, Type DataType);

    public sealed record PlcWriteMismatch(string Variable, string Expected, string Actual);

    public sealed class PlcWriteVerificationResult
    {
        public string Context { get; init; } = string.Empty;
        /// <summary>Variables escritas con éxito por el driver y relegidas.</summary>
        public int Written { get; init; }
        /// <summary>Variables cuyo valor releído coincide.</summary>
        public int Verified { get; init; }
        public IReadOnlyList<PlcWriteMismatch> Mismatches { get; init; } = Array.Empty<PlcWriteMismatch>();
        /// <summary>Variables que no se pudieron releer (error ADS).</summary>
        public IReadOnlyList<string> ReadErrors { get; init; } = Array.Empty<string>();
        /// <summary>Escrituras rechazadas o con excepción (registradas por el batch).</summary>
        public IReadOnlyList<string> WriteFailures { get; init; } = Array.Empty<string>();

        public bool Success => Mismatches.Count == 0 && ReadErrors.Count == 0 && WriteFailures.Count == 0;

        /// <summary>Resumen legible para respuestas API.</summary>
        public string Summary =>
            Success
                ? $"Verificación OK: {Verified}/{Written} variables coinciden"
                : $"Verificación FALLIDA: {Mismatches.Count} discrepancias, {ReadErrors.Count} no verificables, {WriteFailures.Count} escrituras rechazadas";

        public List<string> ToErrorStrings()
        {
            var list = new List<string>();
            list.AddRange(Mismatches.Select(m => $"VERIFY {m.Variable}: esperado={m.Expected} leído={m.Actual}"));
            list.AddRange(ReadErrors.Select(r => $"VERIFY {r}"));
            return list;
        }
    }

    /// <summary>
    /// Lote de escrituras hacia el PLC. Registra lo escrito para poder verificarlo después.
    /// </summary>
    public sealed class PlcWriteBatch
    {
        private readonly ITwinCATService _plc;
        private readonly List<PlcWriteRecord> _records = new();
        private readonly List<string> _failures = new();

        internal PlcWriteBatch(ITwinCATService plc, string context)
        {
            _plc = plc;
            Context = context;
        }

        public string Context { get; }
        public IReadOnlyList<PlcWriteRecord> Records => _records;
        public IReadOnlyList<string> Failures => _failures;

        /// <summary>
        /// Escribe y comprueba el resultado del driver. Lanza <see cref="PlcWriteException"/>
        /// si el driver devuelve false, o propaga la excepción del driver.
        /// </summary>
        public async Task WriteAsync(string variable, object value, Type dataType)
        {
            bool ok;
            try
            {
                ok = await _plc.WriteVariableAsync(variable, value, dataType);
            }
            catch (Exception ex)
            {
                _failures.Add($"{variable}: {ex.Message}");
                throw;
            }

            if (!ok)
            {
                _failures.Add($"{variable}: rechazada por el driver ADS");
                throw new PlcWriteException(variable);
            }

            _records.Add(new PlcWriteRecord(variable, value, dataType));
        }

        /// <summary>
        /// Registra una escritura ya realizada y confirmada por el llamador (success == true).
        /// </summary>
        public void Record(string variable, object value, Type dataType)
            => _records.Add(new PlcWriteRecord(variable, value, dataType));

        /// <summary>
        /// Registra un fallo de escritura detectado por el llamador.
        /// </summary>
        public void RecordFailure(string variable, string reason)
            => _failures.Add($"{variable}: {reason}");
    }

    /// <summary>
    /// Comparación tolerante entre el valor enviado al PLC y el valor releído.
    /// </summary>
    public static class PlcValueComparer
    {
        public const double DoubleRelativeTolerance = 1e-9;
        public const double FloatRelativeTolerance = 1e-5;

        public static bool AreEqual(object? expected, object? actual, Type dataType)
        {
            if (actual == null) return false;

            try
            {
                if (dataType == typeof(bool))
                    return Convert.ToBoolean(expected, CultureInfo.InvariantCulture) == Convert.ToBoolean(actual, CultureInfo.InvariantCulture);

                if (dataType == typeof(string))
                    return string.Equals(
                        (expected?.ToString() ?? string.Empty).TrimEnd('\0').Trim(),
                        (actual.ToString() ?? string.Empty).TrimEnd('\0').Trim(),
                        StringComparison.Ordinal);

                if (dataType == typeof(float))
                    return NearlyEqual(Convert.ToDouble(expected, CultureInfo.InvariantCulture),
                                       Convert.ToDouble(actual, CultureInfo.InvariantCulture),
                                       FloatRelativeTolerance);

                if (dataType == typeof(double) || dataType == typeof(decimal))
                    return NearlyEqual(Convert.ToDouble(expected, CultureInfo.InvariantCulture),
                                       Convert.ToDouble(actual, CultureInfo.InvariantCulture),
                                       DoubleRelativeTolerance);

                // Enteros de cualquier ancho (sbyte, short, int, long, ushort, uint...)
                return Convert.ToInt64(expected, CultureInfo.InvariantCulture) == Convert.ToInt64(actual, CultureInfo.InvariantCulture);
            }
            catch
            {
                return false;
            }
        }

        public static string Format(object? value)
        {
            if (value == null) return "null";
            return value switch
            {
                double d => d.ToString("R", CultureInfo.InvariantCulture),
                float f => f.ToString("R", CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? "null"
            };
        }

        private static bool NearlyEqual(double a, double b, double relTol)
        {
            if (a == b) return true;
            if (double.IsNaN(a) || double.IsNaN(b)) return false;
            var scale = Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
            return Math.Abs(a - b) <= relTol * scale;
        }
    }

    public interface IPlcWriteVerificationService
    {
        /// <summary>Crea un lote de escrituras para un contexto (p.ej. "TrainType 'Metro' → PLC").</summary>
        PlcWriteBatch CreateBatch(string context);

        /// <summary>
        /// Relee todas las variables del lote y compara. Registra discrepancias en L3 y L2.
        /// </summary>
        Task<PlcWriteVerificationResult> VerifyAsync(PlcWriteBatch batch, OperationCategory category, string? user);

        /// <summary>
        /// Registra avisos de coherencia (DB↔Excel↔PLC) en L3 (warning) y L2 (IntegrityWarning, retentivo).
        /// No hace nada si la lista está vacía.
        /// </summary>
        Task LogIntegrityWarningsAsync(OperationCategory category, string context, IReadOnlyCollection<string> warnings, string? user);
    }

    public sealed class PlcWriteVerificationService : IPlcWriteVerificationService
    {
        private readonly ITwinCATService _plc;
        private readonly IOperationLogService _operationLog;
        private readonly ILogger<PlcWriteVerificationService> _logger;

        private const int MaxDetailItems = 50;

        public PlcWriteVerificationService(
            ITwinCATService plc,
            IOperationLogService operationLog,
            ILogger<PlcWriteVerificationService> logger)
        {
            _plc = plc;
            _operationLog = operationLog;
            _logger = logger;
        }

        public PlcWriteBatch CreateBatch(string context) => new(_plc, context);

        public async Task<PlcWriteVerificationResult> VerifyAsync(PlcWriteBatch batch, OperationCategory category, string? user)
        {
            ArgumentNullException.ThrowIfNull(batch);

            var mismatches = new List<PlcWriteMismatch>();
            var readErrors = new List<string>();
            int verified = 0;

            foreach (var rec in batch.Records)
            {
                object? actual;
                try
                {
                    actual = await _plc.ReadVariableAsync(rec.Variable, rec.DataType);
                }
                catch (Exception ex)
                {
                    readErrors.Add($"{rec.Variable}: {ex.Message}");
                    continue;
                }

                if (PlcValueComparer.AreEqual(rec.Value, actual, rec.DataType))
                {
                    verified++;
                }
                else
                {
                    mismatches.Add(new PlcWriteMismatch(rec.Variable, PlcValueComparer.Format(rec.Value), PlcValueComparer.Format(actual)));
                }
            }

            var result = new PlcWriteVerificationResult
            {
                Context = batch.Context,
                Written = batch.Records.Count,
                Verified = verified,
                Mismatches = mismatches,
                ReadErrors = readErrors,
                WriteFailures = batch.Failures.ToList()
            };

            if (result.Success)
            {
                _logger.LogInformation("🔎 [VERIFICACIÓN PLC] {Context}: OK — {Verified}/{Written} variables coinciden tras relectura",
                    batch.Context, verified, batch.Records.Count);
                return result;
            }

            // ---- L3: visible en el visor de logs (Warning+) ----
            _logger.LogError("🔎 [VERIFICACIÓN PLC] {Context}: FALLIDA — {Mismatch} discrepancias, {ReadErr} no verificables, {WriteFail} escrituras rechazadas (de {Written} escritas)",
                batch.Context, mismatches.Count, readErrors.Count, batch.Failures.Count, batch.Records.Count);
            foreach (var m in mismatches)
                _logger.LogError("   ✖ {Var}: esperado={Expected} leído={Actual}", m.Variable, m.Expected, m.Actual);
            foreach (var r in readErrors)
                _logger.LogError("   ✖ no verificable: {Detail}", r);
            foreach (var f in batch.Failures)
                _logger.LogError("   ✖ escritura rechazada: {Detail}", f);

            // ---- L2: retentivo (SQLite) ----
            try
            {
                var details = new Dictionary<string, object>
                {
                    ["context"] = batch.Context,
                    ["written"] = batch.Records.Count,
                    ["verified"] = verified,
                    ["mismatches"] = mismatches.Take(MaxDetailItems).Select(m => new { variable = m.Variable, expected = m.Expected, actual = m.Actual }).ToList(),
                    ["readErrors"] = readErrors.Take(MaxDetailItems).ToList(),
                    ["writeFailures"] = batch.Failures.Take(MaxDetailItems).ToList()
                };

                await _operationLog.LogAsync(
                    category,
                    OperationAction.PlcWriteVerifyFailed,
                    $"{batch.Context}: {result.Summary}",
                    user,
                    details);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔎 [VERIFICACIÓN PLC] No se pudo registrar el fallo de verificación en el Operation Log (L2)");
            }

            return result;
        }

        public async Task LogIntegrityWarningsAsync(OperationCategory category, string context, IReadOnlyCollection<string> warnings, string? user)
        {
            if (warnings == null || warnings.Count == 0) return;

            foreach (var w in warnings)
                _logger.LogWarning("🧩 [INTEGRIDAD] {Context}: {Warning}", context, w);

            try
            {
                await _operationLog.LogAsync(
                    category,
                    OperationAction.IntegrityWarning,
                    $"{context}: {warnings.Count} aviso(s) de integridad — {warnings.First()}",
                    user,
                    new Dictionary<string, object> { ["warnings"] = warnings.Take(MaxDetailItems).ToList() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🧩 [INTEGRIDAD] No se pudo registrar el aviso de integridad en el Operation Log (L2)");
            }
        }
    }
}
