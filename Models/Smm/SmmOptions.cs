namespace SW.PC.API.Backend.Models.Smm
{
    /// <summary>
    /// Opciones del módulo SMM (Statistics &amp; Maintenance Module) y AquarIA.
    /// Bind desde sección <c>AquarIA</c> de <c>appsettings.json</c>.
    /// Decisiones FROZEN: DEC-022 (feature flag Tier).
    /// </summary>
    public class SmmOptions
    {
        public const string SectionName = "AquarIA";

        /// <summary>
        /// Edición/gama del producto. Valores válidos: "Gama1" (BASIC) | "Gama2" (PRO).
        /// Default: "Gama1". Controla activación de servicios/UI Gama 2.
        /// </summary>
        public string Tier { get; set; } = "Gama1";

        /// <summary>True si Tier == "Gama2".</summary>
        public bool IsPro =>
            string.Equals(Tier, "Gama2", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>True si Tier == "Gama1" (default).</summary>
        public bool IsBasic => !IsPro;
    }
}
