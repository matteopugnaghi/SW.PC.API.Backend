# ============================================================================
# add-maintenance-export-translations.ps1
# ----------------------------------------------------------------------------
# Añade las traducciones (SPA/ENG/FRA/ITA) del Export Manager Wizard
# integrado en la vista Mantenimiento (StatisticsView → MaintenanceTab).
# Aplica a todos los Projects/*/translations/translations.json.
# ============================================================================

$ErrorActionPreference = 'Stop'

$additions = @{
    'EXPORT_MAINTENANCE' = [ordered]@{
        # Botón + título + cabecera ------------------------------------------
        'statistics.maintenance.export.button' = @{
            SPA = 'Exportar'; ENG = 'Export'; FRA = 'Exporter'; ITA = 'Esporta'
        }
        'statistics.maintenance.export.button.tooltip' = @{
            SPA = 'Exportar estado y/o historial de intervenciones'
            ENG = 'Export status and/or intervention history'
            FRA = 'Exporter l''état et/ou l''historique des interventions'
            ITA = 'Esporta stato e/o cronologia interventi'
        }
        'statistics.maintenance.export.title' = @{
            SPA = 'Mantenimiento — Estado y intervenciones'
            ENG = 'Maintenance — Status and interventions'
            FRA = 'Maintenance — État et interventions'
            ITA = 'Manutenzione — Stato e interventi'
        }
        'statistics.maintenance.export.subtitle' = @{
            SPA = '{count} elementos visibles'
            ENG = '{count} visible elements'
            FRA = '{count} éléments visibles'
            ITA = '{count} elementi visibili'
        }
        'statistics.maintenance.export.header.scope' = @{
            SPA = 'Datos extraídos de la base local SQLite (sin acceso al PLC).'
            ENG = 'Data extracted from local SQLite database (no PLC access).'
            FRA = 'Données extraites de la base SQLite locale (sans accès PLC).'
            ITA = 'Dati estratti dal database SQLite locale (senza accesso PLC).'
        }

        # Dataset 1 — elements health ----------------------------------------
        'maintenance.export.dataset.elements_health.label' = @{
            SPA = 'Estado de salud (vida útil + mantenimiento)'
            ENG = 'Health status (lifecycle + maintenance)'
            FRA = 'État de santé (durée de vie + maintenance)'
            ITA = 'Stato di salute (vita utile + manutenzione)'
        }
        'maintenance.export.dataset.elements_health.desc' = @{
            SPA = 'Una fila por variable de cada elemento, con valor actual, baseline, consumido, umbrales y estado.'
            ENG = 'One row per variable per element, with current value, baseline, consumed, thresholds and status.'
            FRA = 'Une ligne par variable de chaque élément, avec valeur actuelle, baseline, consommé, seuils et état.'
            ITA = 'Una riga per variabile di ogni elemento, con valore attuale, baseline, consumato, soglie e stato.'
        }

        # Dataset 2 — interventions ------------------------------------------
        'maintenance.export.dataset.interventions.label' = @{
            SPA = 'Historial de intervenciones'
            ENG = 'Intervention history'
            FRA = 'Historique des interventions'
            ITA = 'Cronologia interventi'
        }
        'maintenance.export.dataset.interventions.desc' = @{
            SPA = 'Una fila por intervención (mantenimiento, reemplazo o inspección) con consumibles asociados.'
            ENG = 'One row per intervention (maintenance, replacement or inspection) with associated consumables.'
            FRA = 'Une ligne par intervention (maintenance, remplacement ou inspection) avec les consommables associés.'
            ITA = 'Una riga per intervento (manutenzione, sostituzione o ispezione) con i consumabili associati.'
        }

        # Columnas dataset 1 -------------------------------------------------
        'maintenance.export.col.elementName'        = @{ SPA = 'Elemento';            ENG = 'Element';            FRA = 'Élément';            ITA = 'Elemento' }
        'maintenance.export.col.sku'                = @{ SPA = 'SKU';                 ENG = 'SKU';                FRA = 'SKU';                ITA = 'SKU' }
        'maintenance.export.col.manufacturer'       = @{ SPA = 'Fabricante';          ENG = 'Manufacturer';       FRA = 'Fabricant';          ITA = 'Produttore' }
        'maintenance.export.col.model'              = @{ SPA = 'Modelo';              ENG = 'Model';              FRA = 'Modèle';             ITA = 'Modello' }
        'maintenance.export.col.varName'            = @{ SPA = 'Variable';            ENG = 'Variable';           FRA = 'Variable';           ITA = 'Variabile' }
        'maintenance.export.col.unit'               = @{ SPA = 'Unidad';              ENG = 'Unit';               FRA = 'Unité';              ITA = 'Unità' }
        'maintenance.export.col.taskType'           = @{ SPA = 'Tipo';                ENG = 'Type';               FRA = 'Type';               ITA = 'Tipo' }
        'maintenance.export.col.currentValue'       = @{ SPA = 'Valor actual';        ENG = 'Current value';      FRA = 'Valeur actuelle';    ITA = 'Valore attuale' }
        'maintenance.export.col.baseline'           = @{ SPA = 'Baseline';            ENG = 'Baseline';           FRA = 'Baseline';           ITA = 'Baseline' }
        'maintenance.export.col.consumed'           = @{ SPA = 'Consumido';           ENG = 'Consumed';           FRA = 'Consommé';           ITA = 'Consumato' }
        'maintenance.export.col.warning'            = @{ SPA = 'Umbral atención';     ENG = 'Warning threshold';  FRA = 'Seuil d''attention';  ITA = 'Soglia attenzione' }
        'maintenance.export.col.critical'           = @{ SPA = 'Umbral crítico';      ENG = 'Critical threshold'; FRA = 'Seuil critique';     ITA = 'Soglia critica' }
        'maintenance.export.col.healthPct'          = @{ SPA = 'Salud %';             ENG = 'Health %';           FRA = 'Santé %';            ITA = 'Salute %' }
        'maintenance.export.col.status'             = @{ SPA = 'Estado';              ENG = 'Status';             FRA = 'État';               ITA = 'Stato' }
        'maintenance.export.col.lastInterventionAt' = @{ SPA = 'Última intervención'; ENG = 'Last intervention';  FRA = 'Dernière intervention'; ITA = 'Ultimo intervento' }
        'maintenance.export.col.lastReadingAt'      = @{ SPA = 'Última lectura';      ENG = 'Last reading';       FRA = 'Dernière lecture';   ITA = 'Ultima lettura' }

        # Columnas dataset 2 -------------------------------------------------
        'maintenance.export.intervention.col.performedAt'      = @{ SPA = 'Fecha';            ENG = 'Date';            FRA = 'Date';            ITA = 'Data' }
        'maintenance.export.intervention.col.elementName'      = @{ SPA = 'Elemento';         ENG = 'Element';         FRA = 'Élément';         ITA = 'Elemento' }
        'maintenance.export.intervention.col.taskName'         = @{ SPA = 'Tarea';            ENG = 'Task';            FRA = 'Tâche';           ITA = 'Attività' }
        'maintenance.export.intervention.col.interventionType' = @{ SPA = 'Tipo';             ENG = 'Type';            FRA = 'Type';            ITA = 'Tipo' }
        'maintenance.export.intervention.col.performedByUser'  = @{ SPA = 'Operador';         ENG = 'Operator';        FRA = 'Opérateur';       ITA = 'Operatore' }
        'maintenance.export.intervention.col.performedByRole'  = @{ SPA = 'Rol';              ENG = 'Role';            FRA = 'Rôle';            ITA = 'Ruolo' }
        'maintenance.export.intervention.col.workOrderRef'     = @{ SPA = 'Orden de trabajo'; ENG = 'Work order';      FRA = 'Ordre de travail'; ITA = 'Ordine di lavoro' }
        'maintenance.export.intervention.col.accumulatedValue' = @{ SPA = 'Valor acumulado';  ENG = 'Accumulated value'; FRA = 'Valeur cumulée'; ITA = 'Valore accumulato' }
        'maintenance.export.intervention.col.partsUsed'        = @{ SPA = 'Consumibles';      ENG = 'Consumables';     FRA = 'Consommables';    ITA = 'Consumabili' }
        'maintenance.export.intervention.col.notes'            = @{ SPA = 'Notas';            ENG = 'Notes';           FRA = 'Notes';           ITA = 'Note' }

        # Filtros + estados --------------------------------------------------
        'maintenance.export.filter.healthStatus' = @{
            SPA = 'Estado de salud'; ENG = 'Health status'; FRA = 'État de santé'; ITA = 'Stato di salute'
        }
        'maintenance.export.status.all'      = @{ SPA = 'Todos';    ENG = 'All';        FRA = 'Tous';     ITA = 'Tutti' }
        'maintenance.export.status.critical' = @{ SPA = 'Crítico';  ENG = 'Critical';   FRA = 'Critique'; ITA = 'Critico' }
        'maintenance.export.status.warning'  = @{ SPA = 'Atención'; ENG = 'Warning';    FRA = 'Attention'; ITA = 'Attenzione' }
        'maintenance.export.status.ok'       = @{ SPA = 'OK';       ENG = 'OK';         FRA = 'OK';       ITA = 'OK' }
        'maintenance.export.status.unknown'  = @{ SPA = 'Desconocido'; ENG = 'Unknown'; FRA = 'Inconnu';  ITA = 'Sconosciuto' }
    }
}

# ============================================================================
# Aplicar a todos los Projects/*/translations/translations.json
# ============================================================================
$projectsRoot = Join-Path $PSScriptRoot '..\Projects'
$files = Get-ChildItem -Path $projectsRoot -Recurse -Filter 'translations.json' |
         Where-Object { $_.FullName -match '\\translations\\translations\.json$' }

foreach ($file in $files) {
    Write-Host ""
    Write-Host "→ $($file.FullName)" -ForegroundColor Cyan

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $raw = [System.IO.File]::ReadAllText($file.FullName, $utf8NoBom)
    $json = $raw | ConvertFrom-Json

    $added = 0
    $skipped = 0

    foreach ($pageName in $additions.Keys) {
        $pageExists = $json.pages.PSObject.Properties.Name.Contains($pageName)
        if ($pageExists) {
            $page = $json.pages.$pageName
            $labels = New-Object System.Collections.Generic.List[string]
            foreach ($l in $page.labels) { $labels.Add([string]$l) }
        } else {
            Write-Host "   ℹ Página '$pageName' no existe — sólo se añaden traducciones" -ForegroundColor DarkGray
            $labels = $null
        }

        foreach ($key in $additions[$pageName].Keys) {
            if ($labels -ne $null) {
                if ($labels -contains $key) { $skipped++ } else { $labels.Add($key); $added++ }
            } else { $added++ }

            $entry = $additions[$pageName][$key]
            $obj = [ordered]@{
                SPA = $entry.SPA; ENG = $entry.ENG; FRA = $entry.FRA; ITA = $entry.ITA
            }
            if ($json.translations.PSObject.Properties.Name.Contains($key)) {
                $json.translations.$key = [pscustomobject]$obj
            } else {
                $json.translations | Add-Member -NotePropertyName $key -NotePropertyValue ([pscustomobject]$obj)
            }
        }
        if ($pageExists) { $page.labels = $labels.ToArray() }
    }

    $json.metadata.lastModified = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

    $out = $json | ConvertTo-Json -Depth 100
    Copy-Item -Path $file.FullName -Destination "$($file.FullName).bak" -Force
    [System.IO.File]::WriteAllText($file.FullName, $out, $utf8NoBom)
    Write-Host "   ✓ añadidas $added · existentes $skipped (backup: $($file.Name).bak)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Hecho." -ForegroundColor Green
