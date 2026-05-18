// Inyecta traducciones de GroupCard / GroupDetailModal / EpicDatePicker
// (Phase 5: 4 columnas fijas + labels hardcoded del modal del chart)
const fs = require('fs');
const path = require('path');

const FILE = path.join(__dirname, '..', 'Projects', 'A72.TOUTWP', 'translations', 'translations.json');
const json = JSON.parse(fs.readFileSync(FILE, 'utf8'));

const NEW = {
  // Columnas fijas (4 toggleables + comunes) — usadas en card preview, modal table y export
  'statistics.column.inicio':         { SPA: 'Inicio',   ENG: 'Start',     FRA: 'Début',     ITA: 'Inizio' },
  'statistics.column.fin':            { SPA: 'Fin',      ENG: 'End',       FRA: 'Fin',       ITA: 'Fine' },
  'statistics.column.duracion':       { SPA: 'Duración', ENG: 'Duration',  FRA: 'Durée',     ITA: 'Durata' },
  'statistics.column.duracion_short': { SPA: 'Dur.',     ENG: 'Dur.',      FRA: 'Durée',     ITA: 'Dur.' },
  'statistics.column.alarmas':        { SPA: 'Alarmas',  ENG: 'Alarms',    FRA: 'Alarmes',   ITA: 'Allarmi' },
  'statistics.column.fecha':          { SPA: 'Fecha',    ENG: 'Date',      FRA: 'Date',      ITA: 'Data' },
  'statistics.column.estado':         { SPA: 'Estado',   ENG: 'Status',    FRA: 'État',      ITA: 'Stato' },
  'statistics.column.razon_fin':      { SPA: 'Razón fin', ENG: 'End reason', FRA: 'Raison fin', ITA: 'Motivo fine' },

  // Modal chrome (header, botones)
  'statistics.modal.loading':                { SPA: 'Cargando datos del grupo…', ENG: 'Loading group data…', FRA: 'Chargement des données du groupe…', ITA: 'Caricamento dati del gruppo…' },
  'statistics.modal.header.group_label':     { SPA: 'Grupo',    ENG: 'Group',   FRA: 'Groupe',   ITA: 'Gruppo' },
  'statistics.modal.button.export':          { SPA: 'Exportar', ENG: 'Export',  FRA: 'Exporter', ITA: 'Esporta' },
  'statistics.modal.button.export_tooltip':  { SPA: 'Exportar datos del modal (Imprimir / QR)', ENG: 'Export modal data (Print / QR)', FRA: 'Exporter les données du modal (Imprimer / QR)', ITA: 'Esporta dati del modal (Stampa / QR)' },
  'statistics.modal.button.back':            { SPA: 'Volver',   ENG: 'Back',    FRA: 'Retour',   ITA: 'Indietro' },

  // Modal: sección Variables (SuperAdmin)
  'statistics.modal.variables.title':         { SPA: 'Variables ({{count}})', ENG: 'Variables ({{count}})', FRA: 'Variables ({{count}})', ITA: 'Variabili ({{count}})' },
  'statistics.modal.variables.empty':         { SPA: 'ℹ Este grupo no tiene variables.', ENG: 'ℹ This group has no variables.', FRA: 'ℹ Ce groupe n\'a aucune variable.', ITA: 'ℹ Questo gruppo non ha variabili.' },
  'statistics.modal.variables.col.varname':   { SPA: 'VarName', ENG: 'VarName', FRA: 'NomVar',  ITA: 'NomeVar' },
  'statistics.modal.variables.col.origin':    { SPA: 'Origen',  ENG: 'Origin',  FRA: 'Origine', ITA: 'Origine' },
  'statistics.modal.variables.col.type':      { SPA: 'Tipo',    ENG: 'Type',    FRA: 'Type',    ITA: 'Tipo' },
  'statistics.modal.variables.col.unit':      { SPA: 'Unidad',  ENG: 'Unit',    FRA: 'Unité',   ITA: 'Unità' },
  'statistics.modal.variables.col.warn_crit': { SPA: 'Warn / Crit', ENG: 'Warn / Crit', FRA: 'Avert / Crit', ITA: 'Avv / Crit' },
  'statistics.modal.variables.computed':      { SPA: 'Computed', ENG: 'Computed', FRA: 'Calculé', ITA: 'Calcolato' },

  // Modal: banner Snapshots/Ciclos + chips + filtros
  'statistics.modal.label.snapshots':         { SPA: 'Snapshots', ENG: 'Snapshots', FRA: 'Captures', ITA: 'Snapshot' },
  'statistics.modal.label.cycles':            { SPA: 'Ciclos',    ENG: 'Cycles',    FRA: 'Cycles',   ITA: 'Cicli' },
  'statistics.modal.label.manual':            { SPA: 'MANUAL',    ENG: 'MANUAL',    FRA: 'MANUEL',   ITA: 'MANUALE' },
  'statistics.modal.label.from':              { SPA: 'Desde',     ENG: 'From',      FRA: 'De',       ITA: 'Da' },
  'statistics.modal.label.to':                { SPA: 'Hasta',     ENG: 'To',        FRA: 'À',        ITA: 'A' },
  'statistics.modal.tooltip.clear_filter':    { SPA: 'Limpiar filtro', ENG: 'Clear filter', FRA: 'Effacer le filtre', ITA: 'Pulisci filtro' },
  'statistics.modal.tooltip.manual_capture':  { SPA: 'Captura manual', ENG: 'Manual capture', FRA: 'Capture manuelle', ITA: 'Acquisizione manuale' },
  'statistics.modal.tooltip.frequency':       { SPA: 'Frecuencia {{freq}}', ENG: 'Frequency {{freq}}', FRA: 'Fréquence {{freq}}', ITA: 'Frequenza {{freq}}' },
  'statistics.modal.tooltip.range':           { SPA: 'Rango {{from}} → {{to}}', ENG: 'Range {{from}} → {{to}}', FRA: 'Plage {{from}} → {{to}}', ITA: 'Intervallo {{from}} → {{to}}' },
  'statistics.modal.tooltip.last_at':         { SPA: 'Último: {{date}}', ENG: 'Last: {{date}}', FRA: 'Dernier : {{date}}', ITA: 'Ultimo: {{date}}' },
  'statistics.modal.tooltip.vars_count':      { SPA: '{{count}} variables', ENG: '{{count}} variables', FRA: '{{count}} variables', ITA: '{{count}} variabili' },

  // Modal: botones snapshot/borrado
  'statistics.modal.button.snapshot_now':        { SPA: '📸 Snapshot ahora', ENG: '📸 Snapshot now', FRA: '📸 Capture maintenant', ITA: '📸 Snapshot ora' },
  'statistics.modal.button.snapshot_capturing':  { SPA: '⏳ Capturando…',    ENG: '⏳ Capturing…',   FRA: '⏳ Capture…',          ITA: '⏳ Acquisizione…' },
  'statistics.modal.tooltip.snapshot_now':       { SPA: 'Capturar ahora un snapshot de las variables Continuous', ENG: 'Capture a snapshot of Continuous variables now', FRA: 'Capturer maintenant un instantané des variables Continuous', ITA: 'Acquisisci ora uno snapshot delle variabili Continuous' },
  'statistics.modal.button.delete_all':          { SPA: 'Borrar todos', ENG: 'Delete all', FRA: 'Tout supprimer', ITA: 'Elimina tutti' },
  'statistics.modal.tooltip.delete_all_snapshots': { SPA: 'Eliminar TODOS los snapshots del grupo (hard delete, irreversible)', ENG: 'Delete ALL snapshots of the group (hard delete, irreversible)', FRA: 'Supprimer TOUS les instantanés du groupe (suppression définitive, irréversible)', ITA: 'Elimina TUTTI gli snapshot del gruppo (hard delete, irreversibile)' },
  'statistics.modal.tooltip.delete_all_cycles':    { SPA: 'Eliminar TODOS los ciclos del grupo (soft delete)', ENG: 'Delete ALL cycles of the group (soft delete)', FRA: 'Supprimer TOUS les cycles du groupe (suppression réversible)', ITA: 'Elimina TUTTI i cicli del gruppo (soft delete)' },
  'statistics.modal.button.hard_purge':           { SPA: 'Vaciar BD', ENG: 'Wipe DB', FRA: 'Vider la BD', ITA: 'Svuota DB' },
  'statistics.modal.tooltip.hard_purge':          { SPA: '⚠️ Vaciar físicamente la BD del grupo (ciclos + readings + snapshots Continuous + alarmas). IRREVERSIBLE.', ENG: '⚠️ Physically wipe the group DB (cycles + readings + Continuous snapshots + alarms). IRREVERSIBLE.', FRA: '⚠️ Vider physiquement la BD du groupe (cycles + lectures + instantanés Continuous + alarmes). IRRÉVERSIBLE.', ITA: '⚠️ Svuotare fisicamente il DB del gruppo (cicli + readings + snapshot Continuous + allarmi). IRREVERSIBILE.' },

  // Modal: tabla ciclos + popover alarmas + delete ciclo
  'statistics.modal.alarms_popover.title':     { SPA: 'Alarmas del ciclo ({{count}})', ENG: 'Cycle alarms ({{count}})', FRA: 'Alarmes du cycle ({{count}})', ITA: 'Allarmi del ciclo ({{count}})' },
  'statistics.modal.tooltip.delete_cycle':     { SPA: 'Eliminar este ciclo (soft delete)', ENG: 'Delete this cycle (soft delete)', FRA: 'Supprimer ce cycle (suppression réversible)', ITA: 'Elimina questo ciclo (soft delete)' },

  // Export modal
  'statistics.export.section.cycles':    { SPA: 'CICLOS',    ENG: 'CYCLES',    FRA: 'CYCLES',     ITA: 'CICLI' },
  'statistics.export.section.snapshots': { SPA: 'SNAPSHOTS', ENG: 'SNAPSHOTS', FRA: 'CAPTURES',   ITA: 'SNAPSHOT' },

  // EpicDatePicker
  'statistics.datepicker.today': { SPA: 'Hoy',     ENG: 'Today', FRA: 'Aujourd\'hui', ITA: 'Oggi' },
  'statistics.datepicker.clear': { SPA: 'Limpiar', ENG: 'Clear', FRA: 'Effacer',      ITA: 'Pulisci' },
};

json.translations = json.translations || {};
let added = 0, skipped = 0;
for (const [k, v] of Object.entries(NEW)) {
  if (json.translations[k]) { skipped++; continue; }
  json.translations[k] = v;
  added++;
}

// Merge en pages.STATISTICS.labels sin duplicados (no reemplazar lo existente)
json.pages = json.pages || {};
json.pages.STATISTICS = json.pages.STATISTICS || { labels: [] };
const existing = new Set(json.pages.STATISTICS.labels || []);
for (const k of Object.keys(NEW)) existing.add(k);
json.pages.STATISTICS.labels = Array.from(existing);

fs.writeFileSync(FILE, JSON.stringify(json, null, 2), 'utf8');
console.log(`✅ Phase 5 — Added: ${added}, Skipped (existed): ${skipped}, Total in STATISTICS page: ${json.pages.STATISTICS.labels.length}`);
