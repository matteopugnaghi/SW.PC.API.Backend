namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// Vistas HMI disponibles para suscripción de variables PLC.
    /// Cada vista agrupa variables específicas definidas en la hoja Variables_View del Excel.
    /// El nombre debe coincidir EXACTAMENTE con el valor en la columna "View" del Excel.
    /// </summary>
    public static class HmiViews
    {
        // Vistas principales
        public const string MAIN = "MAIN";
        public const string ALARMS = "ALARMS";
        public const string STATISTICS = "STATISTICS";
        public const string RECIPES = "RECIPES";
        public const string CONFIG = "CONFIG";
        
        // Vista de modo semiautomático - controles manuales
        public const string SEMIAUTOMATIC = "SEMIAUTOMATIC";
        
        // Vistas adicionales (paneles que se abren temporalmente)
        public const string MODEL_DETAIL = "MODEL_DETAIL";
        public const string SCREEN_PANEL = "SCREEN_PANEL";
        
        // Todas las variables (uso interno/debug)
        public const string ALL = "ALL";
        
        /// <summary>
        /// Lista de todas las vistas válidas conocidas.
        /// Nota: El sistema también acepta vistas dinámicas del Excel.
        /// </summary>
        public static readonly string[] KnownViews = new[]
        {
            MAIN, ALARMS, STATISTICS, RECIPES, CONFIG,
            SEMIAUTOMATIC,
            MODEL_DETAIL, SCREEN_PANEL,
            ALL
        };
        
        /// <summary>
        /// Normaliza el nombre de la vista (mayúsculas, sin espacios)
        /// </summary>
        public static string Normalize(string? viewName)
        {
            if (string.IsNullOrWhiteSpace(viewName))
                return MAIN;
            
            return viewName.Trim().ToUpperInvariant();
        }
        
        /// <summary>
        /// Verifica si una vista es conocida (no significa que sea inválida si no está)
        /// </summary>
        public static bool IsKnown(string? viewName)
        {
            if (string.IsNullOrWhiteSpace(viewName))
                return false;
            
            var normalized = Normalize(viewName);
            return KnownViews.Contains(normalized);
        }
    }
}
