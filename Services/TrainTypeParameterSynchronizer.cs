// ============================================================================
// TrainTypeParameterSynchronizer.cs - Sincronización DB ↔ PLC de parámetros de tren
// ============================================================================
// Lógica pura (sin EF, sin ADS) para mantener coherentes los parámetros de un
// TrainType guardado en la DB con la definición actual del Excel (hoja TrainRecipe):
//
//   • La CLAVE de identidad de un parámetro es su variable PLC (columna E / I / Q),
//     NO su posición en el Excel. Emparejar por posición (BOOL_3, DECIMAL_7...)
//     provocaba que, al insertar/mover filas en el Excel, el valor de un slot se
//     escribiera en la variable equivocada.
//   • Los parámetros que ya no existen en el Excel (huérfanos) se eliminan de la DB
//     para que nunca se escriban valores obsoletos al PLC.
//   • Al escribir al PLC sólo se envían parámetros cuya variable sigue definida en
//     el Excel; se devuelve un aviso con las variables del Excel que el slot aún no
//     tiene guardadas (slot guardado con una versión anterior del Excel / software).
//
// Grupos: "Bool" (columnas C-E), "Decimal" (G-M), "Gantry" (O-V).
// ============================================================================

using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    public static class TrainTypeParameterSynchronizer
    {
        public const string GroupBool = "Bool";
        public const string GroupDecimal = "Decimal";
        public const string GroupGantry = "Gantry";

        /// <summary>
        /// Resultado de una sincronización DB ↔ PLC
        /// </summary>
        public sealed class SyncResult
        {
            public int Added { get; init; }
            public int Updated { get; init; }
            /// <summary>Parámetros que estaban en la DB y ya no existen en el Excel (deben borrarse).</summary>
            public IReadOnlyList<TrainTypeParameter> Removed { get; init; } = Array.Empty<TrainTypeParameter>();
        }

        /// <summary>
        /// Normaliza una variable PLC para usarla como clave de emparejamiento.
        /// </summary>
        public static string NormalizeVariable(string? plcVariable)
            => (plcVariable ?? string.Empty).Trim().ToUpperInvariant();

        /// <summary>
        /// Construye el código estable de un parámetro: GRUPO_índice (BOOL_0, DECIMAL_3, GANTRY_9...)
        /// </summary>
        public static string BuildCode(string groupName, int index)
            => $"{groupName.ToUpperInvariant()}_{index}";

        /// <summary>
        /// Crea una entidad TrainTypeParameter a partir de un parámetro leído del PLC.
        /// </summary>
        public static TrainTypeParameter ToEntity(PlcTrainParameterData source)
        {
            var entity = new TrainTypeParameter();
            Apply(entity, source);
            return entity;
        }

        /// <summary>
        /// Copia TODOS los metadatos y el valor del parámetro PLC sobre la entidad.
        /// Actualizar sólo Value (comportamiento anterior) dejaba PlcVariable/Name obsoletos.
        /// </summary>
        public static void Apply(TrainTypeParameter target, PlcTrainParameterData source)
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
            target.GroupName = source.GroupName;
        }

        /// <summary>
        /// Sincroniza la colección de parámetros persistidos de un TrainType con los
        /// parámetros leídos del PLC según la definición actual del Excel.
        /// Empareja por variable PLC (case-insensitive). Los parámetros existentes que no
        /// se emparejan se devuelven en <see cref="SyncResult.Removed"/> y se quitan de
        /// <paramref name="existing"/>; el llamador debe eliminarlos del DbContext.
        /// </summary>
        public static SyncResult Sync(ICollection<TrainTypeParameter> existing, IEnumerable<PlcTrainParameterData> fromPlc)
        {
            ArgumentNullException.ThrowIfNull(existing);
            ArgumentNullException.ThrowIfNull(fromPlc);

            var matched = new HashSet<TrainTypeParameter>(ReferenceEqualityComparer.Instance);
            int added = 0, updated = 0;

            foreach (var plcParam in fromPlc)
            {
                var key = NormalizeVariable(plcParam.PlcVariable);
                if (key.Length == 0) continue;

                // Emparejar por variable PLC, ignorando entidades ya consumidas
                // (si el Excel repite una variable, cada fila obtiene su propia entidad)
                var target = existing.FirstOrDefault(p =>
                    !matched.Contains(p) && NormalizeVariable(p.PlcVariable) == key);

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
        /// Devuelve el conjunto de variables PLC que define el Excel para la receta de tren
        /// (Bool + Decimal + Gantry config). Vacío si no hay configuración.
        /// </summary>
        public static HashSet<string> GetConfiguredVariables(TrainRecipeConfiguration? config)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (config == null) return set;

            foreach (var p in config.BoolParameters)
                AddIfNotEmpty(set, p.PlcVariable);
            foreach (var p in config.DecimalParameters)
                AddIfNotEmpty(set, p.PlcVariable);
            foreach (var p in config.GantryConfigParameters)
                AddIfNotEmpty(set, p.PlcVariable);

            return set;
        }

        /// <summary>
        /// Filtra los parámetros de un TrainType que se pueden escribir al PLC:
        /// sólo aquellos con variable PLC definida Y presente en el Excel actual.
        /// Si no hay Excel (config nula o sin variables) se devuelven todos los que tengan variable,
        /// para no bloquear la escritura por un fallo de carga de configuración.
        /// En <paramref name="warnings"/> se informa de:
        ///   - variables del Excel que el slot no tiene guardadas (hay que re-guardar el slot desde el PLC)
        ///   - parámetros de la DB ignorados por no existir ya en el Excel
        /// </summary>
        public static List<TrainTypeParameter> GetWritableParameters(
            IEnumerable<TrainTypeParameter> parameters,
            TrainRecipeConfiguration? config,
            out List<string> warnings)
        {
            warnings = new List<string>();
            var withVariable = parameters
                .Where(p => !string.IsNullOrWhiteSpace(p.PlcVariable))
                .ToList();

            var configured = GetConfiguredVariables(config);
            if (configured.Count == 0)
            {
                return withVariable;
            }

            var inDb = new HashSet<string>(withVariable.Select(p => NormalizeVariable(p.PlcVariable)));

            var missingInDb = configured
                .Where(v => !inDb.Contains(v))
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToList();
            if (missingInDb.Count > 0)
            {
                warnings.Add(
                    $"El slot no tiene guardadas {missingInDb.Count} variable(s) definidas en el Excel " +
                    $"(re-guardar el slot desde el PLC): {string.Join(", ", missingInDb)}");
            }

            var writable = new List<TrainTypeParameter>(withVariable.Count);
            foreach (var p in withVariable)
            {
                if (configured.Contains(NormalizeVariable(p.PlcVariable)))
                {
                    writable.Add(p);
                }
                else
                {
                    warnings.Add($"Parámetro '{p.Name}' ({p.PlcVariable}) ignorado: ya no existe en el Excel");
                }
            }

            return writable;
        }

        private static void AddIfNotEmpty(HashSet<string> set, string? variable)
        {
            var key = NormalizeVariable(variable);
            if (key.Length > 0) set.Add(key);
        }

        /// <summary>
        /// Parsea el Value persistido de un parámetro numérico de forma tolerante:
        /// primero cultura invariante (formato canónico "1.5"); si falla o si el texto
        /// usa coma decimal sin punto ("1,5", guardado por versiones anteriores con cultura
        /// del SO), se normaliza la coma a punto. Nunca interpreta la coma como miles.
        /// </summary>
        public static double ParseDouble(string? value, double fallback = 0.0)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;

            var text = value.Trim();
            const System.Globalization.NumberStyles styles =
                System.Globalization.NumberStyles.Float; // sin AllowThousands

            if (text.Contains(',') && !text.Contains('.'))
            {
                text = text.Replace(',', '.');
            }

            return double.TryParse(text, styles, System.Globalization.CultureInfo.InvariantCulture, out var d)
                ? d
                : fallback;
        }

        /// <summary>
        /// Convierte el Value persistido al tipo CLR que espera el driver ADS según DataType.
        /// </summary>
        public static (object Value, Type Type) ToPlcValue(TrainTypeParameter param)
        {
            switch (param.DataType?.Trim().ToUpperInvariant())
            {
                case "BOOL":
                    return (bool.TryParse(param.Value, out var b) && b, typeof(bool));
                case "INT":
                case "INTEGER":
                case "DINT":
                    return ((int)Math.Round(ParseDouble(param.Value)), typeof(int));
                case "LREAL":
                case "REAL":
                case "DOUBLE":
                    return (ParseDouble(param.Value), typeof(double));
                default:
                    return (param.Value ?? string.Empty, typeof(string));
            }
        }
    }
}
