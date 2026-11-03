// ============================================================================
// WashTypeParameterSynchronizer.cs - Sincronización DB ↔ PLC de parámetros de lavado
// ============================================================================
// Misma política que TrainTypeParameterSynchronizer:
//   • Identidad del parámetro = variable PLC (no la posición estación/índice del Excel).
//   • Los huérfanos (ya no están en el Excel) se eliminan al guardar desde PLC.
//   • Al escribir, sólo se envían variables definidas en el Excel actual y se avisa de las
//     que el slot no tiene guardadas.
//   • Conversión de Value tolerante a cultura (ParseDouble) — nunca coma como miles.
// ============================================================================

using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    public static class WashTypeParameterSynchronizer
    {
        public sealed class SyncResult
        {
            public int Added { get; init; }
            public int Updated { get; init; }
            public IReadOnlyList<WashTypeParameter> Removed { get; init; } = Array.Empty<WashTypeParameter>();
        }

        public static WashTypeParameter ToEntity(PlcParameterData source)
        {
            var entity = new WashTypeParameter();
            Apply(entity, source);
            return entity;
        }

        public static void Apply(WashTypeParameter target, PlcParameterData source)
        {
            target.ParameterCode = source.Code;
            target.Name = source.Name;
            target.DataType = source.DataType;
            target.Value = source.Value;
            target.MinValue = source.MinValue;
            target.MaxValue = source.MaxValue;
            target.Unit = source.Unit;
            target.PlcVariable = source.PlcVariable?.Trim();
            target.DisplayOrder = source.DisplayOrder;
        }

        public static SyncResult Sync(ICollection<WashTypeParameter> existing, IEnumerable<PlcParameterData> fromPlc)
        {
            ArgumentNullException.ThrowIfNull(existing);
            ArgumentNullException.ThrowIfNull(fromPlc);

            var matched = new HashSet<WashTypeParameter>(ReferenceEqualityComparer.Instance);
            int added = 0, updated = 0;

            foreach (var plcParam in fromPlc)
            {
                var key = TrainTypeParameterSynchronizer.NormalizeVariable(plcParam.PlcVariable);
                if (key.Length == 0) continue;

                var target = existing.FirstOrDefault(p =>
                    !matched.Contains(p) && TrainTypeParameterSynchronizer.NormalizeVariable(p.PlcVariable) == key);

                if (target == null)
                {
                    target = ToEntity(plcParam);
                    existing.Add(target);
                    added++;
                }
                else
                {
                    Apply(target, plcParam);
                    updated++;
                }

                matched.Add(target);
            }

            var removed = existing.Where(p => !matched.Contains(p)).ToList();
            foreach (var orphan in removed)
            {
                existing.Remove(orphan);
            }

            return new SyncResult { Added = added, Updated = updated, Removed = removed };
        }

        /// <summary>
        /// Variables PLC definidas en la hoja WashRecipe (todas las estaciones, Bool + Int).
        /// </summary>
        public static HashSet<string> GetConfiguredVariables(WashRecipeEditorConfiguration? config)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (config == null) return set;

            foreach (var station in config.Stations)
            {
                foreach (var p in station.BoolParameters.Where(p => p.IsConfigured))
                    Add(set, p.PlcVariable);
                foreach (var p in station.IntParameters.Where(p => p.IsConfigured))
                    Add(set, p.PlcVariable);
            }
            return set;
        }

        public static List<WashTypeParameter> GetWritableParameters(
            IEnumerable<WashTypeParameter> parameters,
            WashRecipeEditorConfiguration? config,
            out List<string> warnings)
        {
            warnings = new List<string>();
            var withVariable = parameters.Where(p => !string.IsNullOrWhiteSpace(p.PlcVariable)).ToList();

            var configured = GetConfiguredVariables(config);
            if (configured.Count == 0)
            {
                return withVariable;
            }

            var inDb = new HashSet<string>(withVariable.Select(p => TrainTypeParameterSynchronizer.NormalizeVariable(p.PlcVariable)));
            var missing = configured.Where(v => !inDb.Contains(v)).OrderBy(v => v, StringComparer.Ordinal).ToList();
            if (missing.Count > 0)
            {
                warnings.Add($"El slot no tiene guardadas {missing.Count} variable(s) definidas en el Excel (re-guardar el slot desde el PLC): {string.Join(", ", missing)}");
            }

            var writable = new List<WashTypeParameter>(withVariable.Count);
            foreach (var p in withVariable)
            {
                if (configured.Contains(TrainTypeParameterSynchronizer.NormalizeVariable(p.PlcVariable)))
                    writable.Add(p);
                else
                    warnings.Add($"Parámetro '{p.Name}' ({p.PlcVariable}) ignorado: ya no existe en el Excel");
            }
            return writable;
        }

        public static (object Value, Type Type) ToPlcValue(WashTypeParameter param)
        {
            switch (param.DataType?.Trim().ToUpperInvariant())
            {
                case "BOOL":
                    return (bool.TryParse(param.Value, out var b) && b, typeof(bool));
                case "INT":
                case "INTEGER":
                case "DINT":
                    return ((int)Math.Round(TrainTypeParameterSynchronizer.ParseDouble(param.Value)), typeof(int));
                case "LREAL":
                case "REAL":
                case "DOUBLE":
                    return (TrainTypeParameterSynchronizer.ParseDouble(param.Value), typeof(double));
                default:
                    return (param.Value ?? string.Empty, typeof(string));
            }
        }

        /// <summary>
        /// Prefijo del PLC alternativo (Excel WashRecipe A14): sustituye "st_WashRecipe".
        /// </summary>
        public static string ApplyAlternatePrefix(string variable, string? alternatePrefix)
        {
            if (string.IsNullOrEmpty(variable) || string.IsNullOrEmpty(alternatePrefix))
                return variable;
            return variable.Replace("st_WashRecipe", alternatePrefix, StringComparison.Ordinal);
        }

        private static void Add(HashSet<string> set, string? variable)
        {
            var key = TrainTypeParameterSynchronizer.NormalizeVariable(variable);
            if (key.Length > 0) set.Add(key);
        }
    }
}
