// ============================================================================
// TrainTypePlcWriter.cs - Escritura DB → PLC de un tipo de tren (receta completa)
// ============================================================================
// Única implementación de la carga de una receta de tren al PLC. Antes había 4 copias
// (write-to-plc por id, alternate, select/active y auto-carga) con comportamientos
// distintos: sólo una escribía las tablas Gantry, ninguna escribía min/max height,
// ninguna comprobaba el resultado del driver ADS ni releía lo escrito.
//
// Orden de escritura (igual que el editor TrainRecipe):
//   1. Nombre, GantryTableCount, LineNumber
//   2. Parámetros Bool / Decimal / Gantry-config (sólo los definidos en el Excel actual)
//   3. Tablas de interpolación Gantry: LineCount, puntos START/END de cada línea
//      (FunctionType, Position_X, Position_Y, Speed_Y), min_height y max_height
//   4. VERIFICACIÓN por relectura (IPlcWriteVerificationService) → L3 + L2 si falla
//   5. Trigger de escritura (TRUE) — después de verificar, el PLC lo consume
// ============================================================================

using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    public sealed class TrainTypePlcWriteOptions
    {
        /// <summary>Escribir también las tablas de interpolación Gantry (requiere TrainType.GantryData cargado).</summary>
        public bool IncludeGantryTables { get; init; } = true;

        /// <summary>Transformación de nombre de variable (p.ej. prefijo PLC alternativo). Null = identidad.</summary>
        public Func<string, string>? VariableMapper { get; init; }

        /// <summary>Etiqueta para logs/contexto (p.ej. "PLC", "PLC ALT", "PLC2 auto-carga").</summary>
        public string ContextLabel { get; init; } = "PLC";

        /// <summary>Número de línea a escribir; por defecto TrainType.DisplayOrder.</summary>
        public int? LineNumberOverride { get; init; }

        /// <summary>Escribir el trigger de escritura al final.</summary>
        public bool WriteTrigger { get; init; } = true;
    }

    public sealed class TrainTypePlcWriteResult
    {
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public PlcWriteVerificationResult? Verification { get; set; }
        public bool Success => ErrorCount == 0;

        public string Message => Success
            ? $"Escrito y verificado en PLC correctamente ({SuccessCount} variables)"
            : $"Escritura con errores: {SuccessCount} OK, {ErrorCount} errores";
    }

    public interface ITrainTypePlcWriter
    {
        Task<TrainTypePlcWriteResult> WriteAsync(
            TrainType trainType,
            TrainRecipeConfiguration? trainConfig,
            TrainTypePlcWriteOptions options,
            string username);
    }

    public sealed class TrainTypePlcWriter : ITrainTypePlcWriter
    {
        private readonly IPlcWriteVerificationService _verifier;
        private readonly ILogger<TrainTypePlcWriter> _logger;

        public TrainTypePlcWriter(IPlcWriteVerificationService verifier, ILogger<TrainTypePlcWriter> logger)
        {
            _verifier = verifier;
            _logger = logger;
        }

        public async Task<TrainTypePlcWriteResult> WriteAsync(
            TrainType trainType,
            TrainRecipeConfiguration? trainConfig,
            TrainTypePlcWriteOptions options,
            string username)
        {
            ArgumentNullException.ThrowIfNull(trainType);
            ArgumentNullException.ThrowIfNull(options);

            var result = new TrainTypePlcWriteResult();
            var map = options.VariableMapper ?? (v => v);
            var context = $"Tipo de tren '{trainType.Name}' ({trainType.Code}) → {options.ContextLabel}";
            var batch = _verifier.CreateBatch(context);

            if (trainConfig == null)
            {
                result.Warnings.Add("No se pudo cargar la configuración TrainRecipe del Excel: sólo se escriben los parámetros persistidos en DB");
            }

            // ---------- 1. Cabecera ----------
            await TryWriteAsync(batch, result, "TrainName", map, trainConfig?.TrainNamePlcVariable, trainType.Name, typeof(string));

            var countToWrite = (trainType.GantryTableCount == 1 || trainType.GantryTableCount == 2) ? trainType.GantryTableCount : 1;
            await TryWriteAsync(batch, result, "GantryTableCount", map, trainConfig?.GantryTableCountPlcVariable, countToWrite, typeof(int));

            var lineNumber = options.LineNumberOverride ?? trainType.DisplayOrder;
            await TryWriteAsync(batch, result, "LineNumber", map, trainConfig?.LineNumberPlcVariable, lineNumber, typeof(int));

            // ---------- 2. Parámetros ----------
            var writable = TrainTypeParameterSynchronizer.GetWritableParameters(trainType.Parameters, trainConfig, out var paramWarnings);
            result.Warnings.AddRange(paramWarnings);

            foreach (var param in writable)
            {
                var (value, type) = TrainTypeParameterSynchronizer.ToPlcValue(param);
                await TryWriteAsync(batch, result, $"{param.GroupName ?? "Param"} '{param.Name}'", map, param.PlcVariable, value, type);
            }

            // ---------- 3. Tablas Gantry ----------
            if (options.IncludeGantryTables)
            {
                await WriteGantryTablesAsync(batch, result, trainType, trainConfig, map);
            }

            // ---------- 4. Verificación ----------
            result.Verification = await _verifier.VerifyAsync(batch, OperationCategory.Recipe, username);
            if (!result.Verification.Success)
            {
                var verifyErrors = result.Verification.ToErrorStrings();
                result.Errors.AddRange(verifyErrors);
                result.ErrorCount += verifyErrors.Count;
            }

            await _verifier.LogIntegrityWarningsAsync(OperationCategory.Recipe, context, result.Warnings, username);

            // ---------- 5. Trigger ----------
            if (options.WriteTrigger && !string.IsNullOrEmpty(trainConfig?.WriteTriggerPlcVariable))
            {
                // El PLC lo pone a FALSE al consumir la receta → no se verifica por relectura
                var triggerBatch = _verifier.CreateBatch(context + " (trigger)");
                await TryWriteAsync(triggerBatch, result, "WriteTrigger", map, trainConfig.WriteTriggerPlcVariable, true, typeof(bool));
            }

            _logger.Log(result.Success ? LogLevel.Information : LogLevel.Error,
                "🚆 {Context}: {Success} OK, {Errors} errores, {Warnings} avisos. {Verification}",
                context, result.SuccessCount, result.ErrorCount, result.Warnings.Count, result.Verification.Summary);

            return result;
        }

        private async Task WriteGantryTablesAsync(
            PlcWriteBatch batch,
            TrainTypePlcWriteResult result,
            TrainType trainType,
            TrainRecipeConfiguration? trainConfig,
            Func<string, string> map)
        {
            var configuredTables = trainConfig?.GantryInterpolationTables?.Where(t => t.IsConfigured).ToList()
                                   ?? new List<GantryInterpolationTable>();
            if (configuredTables.Count == 0)
            {
                return;
            }

            var gantryData = trainType.GantryData ?? new List<TrainTypeGantryData>();
            var rowsByTable = gantryData
                .GroupBy(g => g.TableId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderBy(r => r.RowNumber).ToList(), StringComparer.OrdinalIgnoreCase);

            // Tablas en DB que ya no existen en el Excel → no se escriben (aviso)
            foreach (var orphan in rowsByTable.Keys.Where(k => !configuredTables.Any(t => string.Equals(t.TableId, k, StringComparison.OrdinalIgnoreCase))))
            {
                result.Warnings.Add($"Tabla Gantry '{orphan}' guardada en DB pero no configurada en el Excel: ignorada");
            }

            foreach (var table in configuredTables)
            {
                if (!rowsByTable.TryGetValue(table.TableId, out var rows) || rows.Count == 0)
                {
                    result.Warnings.Add($"Tabla Gantry '{table.TableId}' configurada en el Excel sin datos guardados en DB (re-guardar el slot desde el PLC): el PLC conserva los valores anteriores");
                    continue;
                }

                var enabled = rows.Where(r => r.EnableLine).OrderBy(r => r.RowNumber).ToList();

                // LineCount
                await TryWriteAsync(batch, result, $"Gantry {table.TableId} LineCount", map, table.LineCountPlcVariable, enabled.Count, typeof(int));

                if (enabled.Count == 0)
                {
                    result.Warnings.Add($"Tabla Gantry '{table.TableId}' sin líneas habilitadas en DB");
                    continue;
                }

                // Puntos: modelo encadenado (START de línea N = punto N, END = punto N+1)
                for (int i = 0; i < enabled.Count; i++)
                {
                    var row = enabled[i];
                    int startIndex = GantryInterpolationTable.GetStartPointIndex(row.RowNumber);
                    int endIndex = GantryInterpolationTable.GetEndPointIndex(row.RowNumber);
                    sbyte functionType = SyncronToFunctionType(row.Syncron);
                    string label = $"Gantry {table.TableId} L{row.RowNumber}";

                    await TryWriteAsync(batch, result, $"{label} Start.FunctionType", map, table.GetFunctionTypePlcVariable(startIndex), functionType, typeof(sbyte));
                    await TryWriteAsync(batch, result, $"{label} Start.Position_X", map, table.GetPositionXPlcVariable(startIndex), row.Master1xStart, typeof(double));
                    await TryWriteAsync(batch, result, $"{label} Start.Position_Y", map, table.GetPositionYPlcVariable(startIndex), row.Slave1yStart, typeof(double));
                    await TryWriteAsync(batch, result, $"{label} Start.Speed_Y", map, table.GetSpeedYPlcVariable(startIndex), row.SpeedSlaveY1Start, typeof(double));

                    // El END de la última línea no es START de ninguna otra: escribir también su FunctionType
                    // (el editor lo hace para todas las líneas; aquí la siguiente línea lo sobrescribe igual)
                    bool isLast = i == enabled.Count - 1;
                    if (isLast)
                    {
                        await TryWriteAsync(batch, result, $"{label} End.FunctionType", map, table.GetFunctionTypePlcVariable(endIndex), functionType, typeof(sbyte));
                    }
                    await TryWriteAsync(batch, result, $"{label} End.Position_X", map, table.GetPositionXPlcVariable(endIndex), row.Master1xEnd, typeof(double));
                    await TryWriteAsync(batch, result, $"{label} End.Position_Y", map, table.GetPositionYPlcVariable(endIndex), row.Slave1yEnd, typeof(double));
                    await TryWriteAsync(batch, result, $"{label} End.Speed_Y", map, table.GetSpeedYPlcVariable(endIndex), row.SpeedSlaveY1End, typeof(double));
                }

                // min_height / max_height: igual que el editor (Position_X del START de la primera
                // línea y del END de la última). Antes NO se escribían al cargar un slot → valores obsoletos.
                var minHeight = enabled.First().Master1xStart;
                var maxHeight = enabled.Last().Master1xEnd;
                await TryWriteAsync(batch, result, $"Gantry {table.TableId} min_height", map, table.GetMinHeightPlcVariable(), minHeight, typeof(double));
                await TryWriteAsync(batch, result, $"Gantry {table.TableId} max_height", map, table.GetMaxHeightPlcVariable(), maxHeight, typeof(double));

                _logger.LogDebug("🚆 Tabla {TableId}: {Count} líneas escritas (min={Min}, max={Max})", table.TableId, enabled.Count, minHeight, maxHeight);
            }
        }

        /// <summary>
        /// Escribe una variable (si está configurada) contabilizando éxito/error en el resultado.
        /// </summary>
        private static async Task TryWriteAsync(
            PlcWriteBatch batch,
            TrainTypePlcWriteResult result,
            string label,
            Func<string, string> map,
            string? variable,
            object value,
            Type type)
        {
            if (string.IsNullOrWhiteSpace(variable)) return;

            var target = map(variable.Trim());
            try
            {
                await batch.WriteAsync(target, value, type);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.ErrorCount++;
                result.Errors.Add($"{label} ({target}): {ex.Message}");
            }
        }

        public static sbyte SyncronToFunctionType(string? syncron) => syncron switch
        {
            "Syncron" => 1,
            "Polynom 3" => 3,
            "Polynom 5" => 5,
            _ => 1
        };

        public static string FunctionTypeToSyncron(int functionType) => functionType switch
        {
            1 => "Syncron",
            3 => "Polynom 3",
            5 => "Polynom 5",
            _ => "Syncron"
        };

        /// <summary>
        /// Aplica el prefijo del PLC alternativo (Excel TrainRecipe celda A14, ej. "st_TrainRecipe2"):
        /// sustituye la subcadena "st_TrainRecipe" en la variable. Misma semántica que la auto-carga
        /// y que WashRecipe ("st_WashRecipe"). Si la variable no contiene el token se devuelve intacta.
        /// </summary>
        public static string ApplyAlternatePrefix(string variable, string? alternatePrefix)
        {
            if (string.IsNullOrEmpty(variable) || string.IsNullOrEmpty(alternatePrefix))
                return variable;
            return variable.Replace("st_TrainRecipe", alternatePrefix, StringComparison.Ordinal);
        }
    }
}
