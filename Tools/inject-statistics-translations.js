// Inyecta traducciones de StatisticsView en translations.json (SPA/ENG/FRA/ITA)
const fs = require('fs');
const path = require('path');

const FILE = path.join(__dirname, '..', 'Projects', 'A72.TOUTWP', 'translations', 'translations.json');
const json = JSON.parse(fs.readFileSync(FILE, 'utf8'));

const NEW = {
  // Tabs
  'statistics.tab.dashboard':   { SPA: 'Dashboard',     ENG: 'Dashboard',    FRA: 'Tableau de bord', ITA: 'Dashboard' },
  'statistics.tab.variables':   { SPA: 'Variables',     ENG: 'Variables',    FRA: 'Variables',       ITA: 'Variabili' },
  'statistics.tab.maintenance': { SPA: 'Mantenimiento', ENG: 'Maintenance',  FRA: 'Maintenance',     ITA: 'Manutenzione' },
  'statistics.tab.predictions': { SPA: 'Predicciones',  ENG: 'Predictions',  FRA: 'Prédictions',     ITA: 'Predizioni' },

  // Health labels
  'statistics.health.no_data':  { SPA: 'Sin datos', ENG: 'No data',   FRA: 'Aucune donnée', ITA: 'Nessun dato' },
  'statistics.health.ok':       { SPA: 'OK',        ENG: 'OK',        FRA: 'OK',            ITA: 'OK' },
  'statistics.health.warning':  { SPA: 'Atención',  ENG: 'Warning',   FRA: 'Attention',     ITA: 'Attenzione' },
  'statistics.health.upcoming': { SPA: 'Próxima',   ENG: 'Upcoming',  FRA: 'À venir',       ITA: 'Prossima' },
  'statistics.health.critical': { SPA: 'Crítico',   ENG: 'Critical',  FRA: 'Critique',      ITA: 'Critico' },

  // Header KPI
  'statistics.header.kpi.totals':     { SPA: 'TOTALES',    ENG: 'TOTALS',     FRA: 'TOTAUX',     ITA: 'TOTALI' },
  'statistics.header.kpi.percycle':   { SPA: 'POR CICLO',  ENG: 'PER CYCLE',  FRA: 'PAR CYCLE',  ITA: 'PER CICLO' },
  'statistics.header.kpi.continuous': { SPA: 'CONTINUO',   ENG: 'CONTINUOUS', FRA: 'CONTINU',    ITA: 'CONTINUO' },

  // Buttons
  'statistics.button.sync_excel':       { SPA: 'SYNC EXCEL',    ENG: 'SYNC EXCEL',    FRA: 'SYNC EXCEL',    ITA: 'SYNC EXCEL' },
  'statistics.button.sync_running':     { SPA: 'SYNC…',         ENG: 'SYNC…',         FRA: 'SYNC…',         ITA: 'SYNC…' },
  'statistics.button.purge':            { SPA: 'PURGAR',        ENG: 'PURGE',         FRA: 'PURGER',        ITA: 'PURGA' },
  'statistics.button.loading':          { SPA: 'CARGANDO',      ENG: 'LOADING',       FRA: 'CHARGEMENT',    ITA: 'CARICAMENTO' },
  'statistics.button.reload':           { SPA: 'RECARGAR',      ENG: 'RELOAD',        FRA: 'RECHARGER',     ITA: 'RICARICA' },
  'statistics.button.back':             { SPA: 'VOLVER',        ENG: 'BACK',          FRA: 'RETOUR',        ITA: 'INDIETRO' },
  'statistics.button.snapshot_plc':     { SPA: '📷 Snapshot PLC', ENG: '📷 PLC Snapshot', FRA: '📷 Capture PLC', ITA: '📷 Snapshot PLC' },
  'statistics.button.snapshot_running': { SPA: '📷 Capturando…',  ENG: '📷 Capturing…',   FRA: '📷 Capture…',    ITA: '📷 Acquisizione…' },

  // Tooltips
  'statistics.tooltip.sync_excel': { SPA: 'Sincroniza SMM_Groups/Variables/Elements/Consumables desde ProjectConfig.xlsm', ENG: 'Sync SMM_Groups/Variables/Elements/Consumables from ProjectConfig.xlsm', FRA: 'Synchronise SMM_Groups/Variables/Elements/Consumables depuis ProjectConfig.xlsm', ITA: 'Sincronizza SMM_Groups/Variables/Elements/Consumables da ProjectConfig.xlsm' },
  'statistics.tooltip.purge':      { SPA: 'Sincroniza Y borra de la BD lo que ya no esté en el Excel (cascada destructiva). Solo SuperAdmin.', ENG: 'Sync AND delete from DB what is no longer in Excel (destructive cascade). SuperAdmin only.', FRA: 'Synchronise ET supprime de la BD ce qui n\'est plus dans Excel (cascade destructive). SuperAdmin uniquement.', ITA: 'Sincronizza E cancella dal DB ciò che non è più in Excel (cascata distruttiva). Solo SuperAdmin.' },
  'statistics.tooltip.reload':     { SPA: 'Recargar datos', ENG: 'Reload data', FRA: 'Recharger les données', ITA: 'Ricarica dati' },
  'statistics.tooltip.back':       { SPA: 'Volver al panel principal', ENG: 'Back to main panel', FRA: 'Retour au panneau principal', ITA: 'Torna al pannello principale' },

  // Purge dialog
  'statistics.purge.title':          { SPA: '⚠ PURGA DESTRUCTIVA (Sync Excel)', ENG: '⚠ DESTRUCTIVE PURGE (Excel Sync)', FRA: '⚠ PURGE DESTRUCTIVE (Sync Excel)', ITA: '⚠ PURGA DISTRUTTIVA (Sync Excel)' },
  'statistics.purge.message':        { SPA: 'Esta acción sincroniza con el Excel Y BORRA de la base de datos todo lo que ya no esté presente (cascada: grupos→variables→lecturas, elementos→intervenciones, etc.). IRREVERSIBLE.', ENG: 'This action syncs with Excel AND DELETES from the database everything no longer present (cascade: groups→variables→readings, elements→interventions, etc.). IRREVERSIBLE.', FRA: 'Cette action synchronise avec Excel ET SUPPRIME de la base de données tout ce qui n\'est plus présent (cascade: groupes→variables→lectures, éléments→interventions, etc.). IRRÉVERSIBLE.', ITA: 'Questa azione sincronizza con Excel E CANCELLA dal database tutto ciò che non è più presente (cascata: gruppi→variabili→letture, elementi→interventi, ecc.). IRREVERSIBILE.' },
  'statistics.purge.confirm':        { SPA: 'Purgar y sincronizar', ENG: 'Purge and sync', FRA: 'Purger et synchroniser', ITA: 'Purga e sincronizza' },
  'statistics.purge.double_confirm': { SPA: 'Última confirmación: ¿purgar y resincronizar AHORA?', ENG: 'Final confirmation: purge and resync NOW?', FRA: 'Dernière confirmation : purger et resynchroniser MAINTENANT ?', ITA: 'Ultima conferma: purgare e risincronizzare ORA?' },

  // Error
  'statistics.error.loading': { SPA: 'Error cargando datos SMM', ENG: 'Error loading SMM data', FRA: 'Erreur lors du chargement des données SMM', ITA: 'Errore nel caricamento dei dati SMM' },

  // Dashboard
  'statistics.dashboard.loading':    { SPA: 'Cargando…', ENG: 'Loading…', FRA: 'Chargement…', ITA: 'Caricamento…' },
  'statistics.dashboard.empty':      { SPA: 'ℹ Sin grupos SMM definidos en el Excel del proyecto.', ENG: 'ℹ No SMM groups defined in the project Excel.', FRA: 'ℹ Aucun groupe SMM défini dans l\'Excel du projet.', ITA: 'ℹ Nessun gruppo SMM definito nell\'Excel del progetto.' },
  'statistics.dashboard.empty_hint': { SPA: 'Añade hojas Stats_Groups / Stats_Variables a ProjectConfig.xlsm y pulsa ⬇ Sincronizar Excel.', ENG: 'Add Stats_Groups / Stats_Variables sheets to ProjectConfig.xlsm and click ⬇ Sync Excel.', FRA: 'Ajoutez les feuilles Stats_Groups / Stats_Variables à ProjectConfig.xlsm et cliquez sur ⬇ Synchroniser Excel.', ITA: 'Aggiungi i fogli Stats_Groups / Stats_Variables a ProjectConfig.xlsm e premi ⬇ Sincronizza Excel.' },

  // Variables
  'statistics.variables.empty_groups':   { SPA: 'ℹ Sin grupos SMM. Configura el Excel primero.', ENG: 'ℹ No SMM groups. Configure Excel first.', FRA: 'ℹ Aucun groupe SMM. Configurez d\'abord l\'Excel.', ITA: 'ℹ Nessun gruppo SMM. Configura prima l\'Excel.' },
  'statistics.variables.section_groups': { SPA: 'Grupos',                 ENG: 'Groups',              FRA: 'Groupes',              ITA: 'Gruppi' },
  'statistics.variables.select_group':   { SPA: 'Selecciona un grupo para ver sus variables.', ENG: 'Select a group to view its variables.', FRA: 'Sélectionnez un groupe pour voir ses variables.', ITA: 'Seleziona un gruppo per vedere le sue variabili.' },
  'statistics.variables.loading_vars':   { SPA: 'Cargando variables…',    ENG: 'Loading variables…',  FRA: 'Chargement des variables…', ITA: 'Caricamento variabili…' },
  'statistics.variables.empty':          { SPA: 'ℹ Sin variables en este grupo.', ENG: 'ℹ No variables in this group.', FRA: 'ℹ Aucune variable dans ce groupe.', ITA: 'ℹ Nessuna variabile in questo gruppo.' },
  'statistics.variables.computed':       { SPA: 'Computed', ENG: 'Computed', FRA: 'Calculé', ITA: 'Calcolato' },
  'statistics.variables.col.varname':    { SPA: 'VarName',  ENG: 'VarName',  FRA: 'NomVar',  ITA: 'NomeVar' },
  'statistics.variables.col.type':       { SPA: 'Tipo',     ENG: 'Type',     FRA: 'Type',    ITA: 'Tipo' },
  'statistics.variables.col.origin':     { SPA: 'Origen',   ENG: 'Origin',   FRA: 'Origine', ITA: 'Origine' },
  'statistics.variables.col.unit':       { SPA: 'Unidad',   ENG: 'Unit',     FRA: 'Unité',   ITA: 'Unità' },
  'statistics.variables.col.warning':    { SPA: 'Warning',  ENG: 'Warning',  FRA: 'Avertissement', ITA: 'Avviso' },
  'statistics.variables.col.critical':   { SPA: 'Critical', ENG: 'Critical', FRA: 'Critique', ITA: 'Critico' },

  // Predictions
  'statistics.predictions.loading':      { SPA: 'Cargando…',                ENG: 'Loading…',                FRA: 'Chargement…',                ITA: 'Caricamento…' },
  'statistics.predictions.pro_required': { SPA: 'requerida para predicciones.', ENG: 'required for predictions.', FRA: 'requise pour les prédictions.', ITA: 'richiesta per le predizioni.' },
  'statistics.predictions.pro_hint':     { SPA: 'BASIC recopila datos localmente. PRO añade análisis predictivo, anomalías y recomendaciones inteligentes.', ENG: 'BASIC collects data locally. PRO adds predictive analysis, anomaly detection and smart recommendations.', FRA: 'BASIC collecte les données localement. PRO ajoute l\'analyse prédictive, la détection d\'anomalies et des recommandations intelligentes.', ITA: 'BASIC raccoglie i dati localmente. PRO aggiunge analisi predittiva, rilevamento anomalie e raccomandazioni intelligenti.' },
  'statistics.predictions.empty':        { SPA: 'ℹ Sin predicciones activas.', ENG: 'ℹ No active predictions.', FRA: 'ℹ Aucune prédiction active.', ITA: 'ℹ Nessuna predizione attiva.' },
  'statistics.predictions.col.type':        { SPA: 'Tipo',       ENG: 'Type',       FRA: 'Type',         ITA: 'Tipo' },
  'statistics.predictions.col.severity':    { SPA: 'Severidad',  ENG: 'Severity',   FRA: 'Sévérité',     ITA: 'Severità' },
  'statistics.predictions.col.description': { SPA: 'Descripción', ENG: 'Description', FRA: 'Description', ITA: 'Descrizione' },
  'statistics.predictions.col.confidence':  { SPA: 'Confianza',  ENG: 'Confidence', FRA: 'Confiance',    ITA: 'Confidenza' },
  'statistics.predictions.col.created':     { SPA: 'Creada',     ENG: 'Created',    FRA: 'Créée',        ITA: 'Creata' },

  // Maintenance
  'statistics.maintenance.loading_elements':     { SPA: 'Cargando elementos…', ENG: 'Loading elements…', FRA: 'Chargement des éléments…', ITA: 'Caricamento elementi…' },
  'statistics.maintenance.empty':                { SPA: 'ℹ Sin elementos físicos definidos. Añade hoja Stats_Elements al Excel y sincroniza.', ENG: 'ℹ No physical elements defined. Add Stats_Elements sheet to Excel and sync.', FRA: 'ℹ Aucun élément physique défini. Ajoutez la feuille Stats_Elements à l\'Excel et synchronisez.', ITA: 'ℹ Nessun elemento fisico definito. Aggiungi il foglio Stats_Elements all\'Excel e sincronizza.' },
  'statistics.maintenance.config_warnings_title':{ SPA: '⚠ Configuración de mantenimiento — revisar Excel', ENG: '⚠ Maintenance configuration — review Excel', FRA: '⚠ Configuration de maintenance — vérifier l\'Excel', ITA: '⚠ Configurazione manutenzione — verificare Excel' },
  'statistics.maintenance.loading_health':       { SPA: '⚡ Cargando estado de salud… {{done}} / {{total}} elementos ({{pct}}%)', ENG: '⚡ Loading health status… {{done}} / {{total}} elements ({{pct}}%)', FRA: '⚡ Chargement de l\'état de santé… {{done}} / {{total}} éléments ({{pct}}%)', ITA: '⚡ Caricamento stato di salute… {{done}} / {{total}} elementi ({{pct}}%)' },
  'statistics.maintenance.search_placeholder':   { SPA: '🔍 Buscar por nombre, SKU, fabricante, modelo…', ENG: '🔍 Search by name, SKU, manufacturer, model…', FRA: '🔍 Rechercher par nom, SKU, fabricant, modèle…', ITA: '🔍 Cerca per nome, SKU, produttore, modello…' },
  'statistics.maintenance.clear':                { SPA: 'Limpiar',  ENG: 'Clear',  FRA: 'Effacer',  ITA: 'Pulisci' },
  'statistics.maintenance.no_results':           { SPA: 'Sin resultados con esos filtros.', ENG: 'No results with those filters.', FRA: 'Aucun résultat avec ces filtres.', ITA: 'Nessun risultato con questi filtri.' },
  'statistics.maintenance.snapshot_tooltip':     { SPA: 'Captura ahora del PLC y refresca las barras de salud', ENG: 'Capture now from PLC and refresh health bars', FRA: 'Capture maintenant depuis l\'API PLC et rafraîchit les barres de santé', ITA: 'Cattura ora dal PLC e aggiorna le barre di salute' },

  // Maintenance filter chips
  'statistics.maintenance.filter.all':      { SPA: 'Todos',       ENG: 'All',       FRA: 'Tous',         ITA: 'Tutti' },
  'statistics.maintenance.filter.critical': { SPA: '🔴 Crítico',  ENG: '🔴 Critical', FRA: '🔴 Critique', ITA: '🔴 Critico' },
  'statistics.maintenance.filter.warning':  { SPA: '🟡 Atención', ENG: '🟡 Warning',  FRA: '🟡 Attention', ITA: '🟡 Attenzione' },
  'statistics.maintenance.filter.ok':       { SPA: '🟢 OK',       ENG: '🟢 OK',       FRA: '🟢 OK',        ITA: '🟢 OK' },
  'statistics.maintenance.filter.unknown':  { SPA: '⚪ Sin datos', ENG: '⚪ No data',  FRA: '⚪ Aucune donnée', ITA: '⚪ Nessun dato' },

  // Maintenance sort options
  'statistics.maintenance.sort.health_asc':  { SPA: 'Salud ↑ (peor primero)',  ENG: 'Health ↑ (worst first)',  FRA: 'Santé ↑ (pire en premier)',  ITA: 'Salute ↑ (peggiore prima)' },
  'statistics.maintenance.sort.health_desc': { SPA: 'Salud ↓ (mejor primero)', ENG: 'Health ↓ (best first)',  FRA: 'Santé ↓ (meilleur en premier)', ITA: 'Salute ↓ (migliore prima)' },
  'statistics.maintenance.sort.eta_asc':     { SPA: '⏳ Vencen antes',         ENG: '⏳ Due soonest',          FRA: '⏳ Échéance la plus proche', ITA: '⏳ Scadenza più vicina' },
  'statistics.maintenance.sort.name':        { SPA: 'Nombre A-Z',              ENG: 'Name A-Z',                FRA: 'Nom A-Z',                    ITA: 'Nome A-Z' },

  // Maintenance destructive button labels
  'statistics.maintenance.delete_all': { SPA: 'Borrar todos', ENG: 'Delete all', FRA: 'Tout supprimer', ITA: 'Elimina tutti' },
  'statistics.maintenance.hard_purge': { SPA: 'Vaciar BD',    ENG: 'Wipe DB',    FRA: 'Vider la BD',    ITA: 'Svuota DB' },

  // delete_all dialog
  'statistics.maintenance.delete_all.title':              { SPA: '🗑 Resetear TODAS las barras de mantenimiento', ENG: '🗑 Reset ALL maintenance bars', FRA: '🗑 Réinitialiser TOUTES les barres de maintenance', ITA: '🗑 Resetta TUTTE le barre di manutenzione' },
  'statistics.maintenance.delete_all.message':            { SPA: 'Esta acción elimina TODAS las intervenciones del proyecto, dejando todas las barras al 100% (estado "máquina nueva"). Los baselines se resetean y se inicializa un nuevo lifecycle. IRREVERSIBLE.', ENG: 'This action deletes ALL interventions in the project, leaving all bars at 100% ("new machine" state). Baselines are reset and a new lifecycle is initialized. IRREVERSIBLE.', FRA: 'Cette action supprime TOUTES les interventions du projet, laissant toutes les barres à 100% (état « machine neuve »). Les baselines sont réinitialisées et un nouveau cycle de vie est lancé. IRRÉVERSIBLE.', ITA: 'Questa azione elimina TUTTI gli interventi del progetto, lasciando tutte le barre al 100% (stato "macchina nuova"). I baseline vengono resettati e viene inizializzato un nuovo lifecycle. IRREVERSIBILE.' },
  'statistics.maintenance.delete_all.confirm':            { SPA: 'Resetear',  ENG: 'Reset',  FRA: 'Réinitialiser', ITA: 'Resetta' },
  'statistics.maintenance.delete_all.reason_placeholder': { SPA: 'Motivo del reset (mín. 10 caracteres)', ENG: 'Reset reason (min. 10 characters)', FRA: 'Motif de la réinitialisation (min. 10 caractères)', ITA: 'Motivo del reset (min. 10 caratteri)' },
  'statistics.maintenance.delete_all.tooltip':            { SPA: 'Eliminar TODAS las intervenciones del proyecto (resetea baselines, "máquina nueva"). Irreversible.', ENG: 'Delete ALL interventions in the project (resets baselines, "new machine"). Irreversible.', FRA: 'Supprimer TOUTES les interventions du projet (réinitialise les baselines, « machine neuve »). Irréversible.', ITA: 'Elimina TUTTI gli interventi del progetto (resetta i baseline, "macchina nuova"). Irreversibile.' },

  // hard_purge dialog
  'statistics.maintenance.hard_purge.title':              { SPA: '🔥 VACIAR BD de mantenimiento', ENG: '🔥 WIPE maintenance DB', FRA: '🔥 VIDER la BD de maintenance', ITA: '🔥 SVUOTA DB di manutenzione' },
  'statistics.maintenance.hard_purge.message':            { SPA: 'Esta acción borra FÍSICAMENTE de la base de datos: intervenciones, lifecycles y predicciones. No deja registros históricos. IRREVERSIBLE.', ENG: 'This action PHYSICALLY deletes from the database: interventions, lifecycles and predictions. No historical records left. IRREVERSIBLE.', FRA: 'Cette action SUPPRIME PHYSIQUEMENT de la base de données : interventions, cycles de vie et prédictions. Aucun historique conservé. IRRÉVERSIBLE.', ITA: 'Questa azione cancella FISICAMENTE dal database: interventi, lifecycle e predizioni. Nessun record storico rimane. IRREVERSIBILE.' },
  'statistics.maintenance.hard_purge.confirm':            { SPA: 'Vaciar BD', ENG: 'Wipe DB', FRA: 'Vider la BD', ITA: 'Svuota DB' },
  'statistics.maintenance.hard_purge.double_confirm':     { SPA: 'Última confirmación: ¿borrar todo de la BD de mantenimiento AHORA?', ENG: 'Final confirmation: delete everything from the maintenance DB NOW?', FRA: 'Dernière confirmation : tout supprimer de la BD de maintenance MAINTENANT ?', ITA: 'Ultima conferma: cancellare tutto dal DB di manutenzione ORA?' },
  'statistics.maintenance.hard_purge.reason_placeholder': { SPA: 'Motivo del HARD PURGE (mín. 10 caracteres)', ENG: 'HARD PURGE reason (min. 10 characters)', FRA: 'Motif du HARD PURGE (min. 10 caractères)', ITA: 'Motivo dell\'HARD PURGE (min. 10 caratteri)' },
  'statistics.maintenance.hard_purge.tooltip':            { SPA: '⚠️ Vaciar físicamente la BD de mantenimiento (intervenciones + lifecycles + predicciones). IRREVERSIBLE.', ENG: '⚠️ Physically wipe the maintenance DB (interventions + lifecycles + predictions). IRREVERSIBLE.', FRA: '⚠️ Vider physiquement la BD de maintenance (interventions + cycles de vie + prédictions). IRRÉVERSIBLE.', ITA: '⚠️ Svuotare fisicamente il DB di manutenzione (interventi + lifecycle + predizioni). IRREVERSIBILE.' },
};

json.translations = json.translations || {};
let added = 0, skipped = 0;
for (const [k, v] of Object.entries(NEW)) {
  if (json.translations[k]) { skipped++; continue; }
  json.translations[k] = v;
  added++;
}

json.pages = json.pages || {};
json.pages.STATISTICS = { labels: Object.keys(NEW) };

fs.writeFileSync(FILE, JSON.stringify(json, null, 2), 'utf8');
console.log(`✅ Added: ${added}, Skipped (existed): ${skipped}, Total in STATISTICS page: ${Object.keys(NEW).length}`);
