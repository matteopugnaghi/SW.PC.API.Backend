// ============================================================================
// IExportDatasetRegistry.cs — Indexa providers por DatasetId y por Source
// ============================================================================
// Registro Scoped: se construye con la colección de IExportDatasetProvider
// registrados en DI. ExportService y los controllers lo consultan para:
//   - Resolver un DatasetId concreto (ejecución de ExportTask).
//   - Listar datasets disponibles para un Source (Step 0 del wizard).
// ============================================================================

using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export;

public interface IExportDatasetRegistry
{
    /// <summary>Devuelve el provider por su DatasetId, o null si no existe.</summary>
    IExportDatasetProvider? Get(string datasetId);

    /// <summary>Devuelve todos los providers registrados para un Source.</summary>
    IReadOnlyList<IExportDatasetProvider> GetBySource(string source);

    /// <summary>Devuelve todos los providers registrados.</summary>
    IReadOnlyList<IExportDatasetProvider> GetAll();
}

public class ExportDatasetRegistry : IExportDatasetRegistry
{
    private readonly Dictionary<string, IExportDatasetProvider> _byId;
    private readonly List<IExportDatasetProvider> _all;

    public ExportDatasetRegistry(IEnumerable<IExportDatasetProvider> providers)
    {
        _all = providers.ToList();
        _byId = new Dictionary<string, IExportDatasetProvider>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in _all)
        {
            if (string.IsNullOrWhiteSpace(p.DatasetId))
            {
                Console.WriteLine($"[Export] Provider {p.GetType().Name} tiene DatasetId vacío — ignorado.");
                continue;
            }
            if (_byId.ContainsKey(p.DatasetId))
            {
                Console.WriteLine($"[Export] DatasetId duplicado: '{p.DatasetId}' ({p.GetType().Name}) — se ignora.");
                continue;
            }
            _byId[p.DatasetId] = p;
        }
    }

    public IExportDatasetProvider? Get(string datasetId)
        => string.IsNullOrWhiteSpace(datasetId) ? null
           : _byId.TryGetValue(datasetId, out var p) ? p : null;

    public IReadOnlyList<IExportDatasetProvider> GetBySource(string source)
        => string.IsNullOrWhiteSpace(source)
           ? Array.Empty<IExportDatasetProvider>()
           : _all.Where(p => string.Equals(p.Source, source, StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<IExportDatasetProvider> GetAll() => _all;
}
