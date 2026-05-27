# ============================================================================
# add-export-translations.ps1
# ----------------------------------------------------------------------------
# Añade las traducciones (SPA/ENG/FRA/ITA) de las nuevas funcionalidades del
# Export Manager Wizard (presets de nombre, tokens, estrategia "Si el archivo
# ya existe", diálogos táctiles) a todos los translations.json de Projects/*.
# ============================================================================

$ErrorActionPreference = 'Stop'

# --- Nuevas claves: namespace -> { key -> {SPA, ENG, FRA, ITA} } -------------
$additions = @{
    'EXPORT_WIZARD' = [ordered]@{
        'exportWizard.about' = @{
            SPA = '¿Qué es esto?'
            ENG = 'What is this?'
            FRA = 'Qu''est-ce que c''est ?'
            ITA = 'Cos''è questo?'
        }
        'exportWizard.previewEmpty' = @{
            SPA = 'No hay datos todavía para previsualizar. La exportación generará un archivo con la cabecera y 0 filas.'
            ENG = 'No data yet to preview. The export will produce a file with the header and 0 rows.'
            FRA = 'Aucune donnée à prévisualiser pour l''instant. L''export produira un fichier avec l''en-tête et 0 ligne.'
            ITA = 'Nessun dato da visualizzare in anteprima. L''esportazione produrrà un file con intestazione e 0 righe.'
        }

        # --- Presets de nombre de archivo ------------------------------------
        'exportWizard.filenamePresets' = @{
            SPA = 'Presets:'; ENG = 'Presets:'; FRA = 'Préréglages :'; ITA = 'Preset:'
        }
        'exportWizard.preset.date' = @{
            SPA = 'Por fecha';        ENG = 'By date';        FRA = 'Par date';       ITA = 'Per data'
        }
        'exportWizard.preset.datetime' = @{
            SPA = 'Fecha y hora';     ENG = 'Date and time';  FRA = 'Date et heure';  ITA = 'Data e ora'
        }
        'exportWizard.preset.unique' = @{
            SPA = 'Único (UUID)';     ENG = 'Unique (UUID)';  FRA = 'Unique (UUID)';  ITA = 'Univoco (UUID)'
        }
        'exportWizard.preset.byProject' = @{
            SPA = 'Por proyecto';     ENG = 'By project';     FRA = 'Par projet';     ITA = 'Per progetto'
        }
        'exportWizard.preset.byYearMonth' = @{
            SPA = 'Año/Mes';          ENG = 'Year/Month';     FRA = 'Année/Mois';     ITA = 'Anno/Mese'
        }
        'exportWizard.preset.byTask' = @{
            SPA = 'Por tarea';        ENG = 'By task';        FRA = 'Par tâche';      ITA = 'Per attività'
        }

        # --- Tokens disponibles ----------------------------------------------
        'exportWizard.tokensTitle' = @{
            SPA = 'Tokens disponibles'
            ENG = 'Available tokens'
            FRA = 'Jetons disponibles'
            ITA = 'Token disponibili'
        }
        'exportWizard.tokensHint' = @{
            SPA = '(click para añadir)'
            ENG = '(click to insert)'
            FRA = '(cliquer pour insérer)'
            ITA = '(clic per inserire)'
        }
        'exportWizard.tokensFootnote' = @{
            SPA = 'La extensión (.xlsx, .csv, .json…) se añade automáticamente. Recomendado incluir {datetime} o {uuid} para evitar sobreescrituras.'
            ENG = 'The extension (.xlsx, .csv, .json…) is added automatically. Including {datetime} or {uuid} is recommended to avoid overwrites.'
            FRA = 'L''extension (.xlsx, .csv, .json…) est ajoutée automatiquement. Inclure {datetime} ou {uuid} est recommandé pour éviter les écrasements.'
            ITA = 'L''estensione (.xlsx, .csv, .json…) viene aggiunta automaticamente. Si consiglia di includere {datetime} o {uuid} per evitare sovrascritture.'
        }
        'exportWizard.tk.fecha'    = @{ SPA = 'Fecha actual (yyyy-MM-dd)'; ENG = 'Current date (yyyy-MM-dd)';    FRA = 'Date actuelle (yyyy-MM-dd)';    ITA = 'Data corrente (yyyy-MM-dd)' }
        'exportWizard.tk.hora'     = @{ SPA = 'Hora actual (HH-mm-ss)';    ENG = 'Current time (HH-mm-ss)';      FRA = 'Heure actuelle (HH-mm-ss)';     ITA = 'Ora corrente (HH-mm-ss)' }
        'exportWizard.tk.datetime' = @{ SPA = 'Fecha + hora (yyyy-MM-dd_HH-mm-ss)'; ENG = 'Date + time (yyyy-MM-dd_HH-mm-ss)'; FRA = 'Date + heure (yyyy-MM-dd_HH-mm-ss)'; ITA = 'Data + ora (yyyy-MM-dd_HH-mm-ss)' }
        'exportWizard.tk.year'     = @{ SPA = 'Año (yyyy)';                ENG = 'Year (yyyy)';                  FRA = 'Année (yyyy)';                  ITA = 'Anno (yyyy)' }
        'exportWizard.tk.month'    = @{ SPA = 'Mes (MM)';                  ENG = 'Month (MM)';                   FRA = 'Mois (MM)';                     ITA = 'Mese (MM)' }
        'exportWizard.tk.day'      = @{ SPA = 'Día (dd)';                  ENG = 'Day (dd)';                     FRA = 'Jour (dd)';                     ITA = 'Giorno (dd)' }
        'exportWizard.tk.uuid'     = @{ SPA = 'Identificador aleatorio único (8 chars)'; ENG = 'Unique random identifier (8 chars)'; FRA = 'Identifiant aléatoire unique (8 car.)'; ITA = 'Identificatore casuale univoco (8 car.)' }
        'exportWizard.tk.source'   = @{ SPA = 'Módulo origen (sbom, integrity-certificate, …)'; ENG = 'Source module (sbom, integrity-certificate, …)'; FRA = 'Module source (sbom, integrity-certificate, …)'; ITA = 'Modulo di origine (sbom, integrity-certificate, …)' }
        'exportWizard.tk.dataset'  = @{ SPA = 'Identificador del dataset'; ENG = 'Dataset identifier';           FRA = 'Identifiant du dataset';        ITA = 'Identificatore del dataset' }
        'exportWizard.tk.format'   = @{ SPA = 'Formato (xlsx, csv, json, …)'; ENG = 'Format (xlsx, csv, json, …)'; FRA = 'Format (xlsx, csv, json, …)'; ITA = 'Formato (xlsx, csv, json, …)' }
        'exportWizard.tk.project'  = @{ SPA = 'ID del proyecto activo';    ENG = 'Active project ID';            FRA = 'ID du projet actif';            ITA = 'ID del progetto attivo' }
        'exportWizard.tk.taskName' = @{ SPA = 'Nombre de la tarea';        ENG = 'Task name';                    FRA = 'Nom de la tâche';               ITA = 'Nome dell''attività' }
        'exportWizard.tk.taskId'   = @{ SPA = 'ID numérico de la tarea';   ENG = 'Numeric task ID';              FRA = 'ID numérique de la tâche';      ITA = 'ID numerico dell''attività' }

        # --- Estrategia "Si el archivo ya existe" ----------------------------
        'exportWizard.onFileExists' = @{
            SPA = 'Si el archivo ya existe'
            ENG = 'If the file already exists'
            FRA = 'Si le fichier existe déjà'
            ITA = 'Se il file esiste già'
        }
        'exportWizard.onFileExists.overwrite' = @{
            SPA = 'Sobreescribir (reemplazar el anterior)'
            ENG = 'Overwrite (replace the previous one)'
            FRA = 'Écraser (remplacer le précédent)'
            ITA = 'Sovrascrivere (sostituire il precedente)'
        }
        'exportWizard.onFileExists.rename' = @{
            SPA = 'Incremental — añadir _001, _002, … (no borra los anteriores)'
            ENG = 'Incremental — append _001, _002, … (keeps previous files)'
            FRA = 'Incrémental — ajouter _001, _002, … (conserve les précédents)'
            ITA = 'Incrementale — aggiungere _001, _002, … (mantiene i precedenti)'
        }
        'exportWizard.onFileExists.skip' = @{
            SPA = 'Omitir — no escribir si ya existe'
            ENG = 'Skip — do not write if it already exists'
            FRA = 'Ignorer — ne pas écrire s''il existe déjà'
            ITA = 'Salta — non scrivere se esiste già'
        }
        'exportWizard.onFileExists.overwriteHint' = @{
            SPA = '⚠ Cada ejecución reemplaza el archivo anterior. Combina con tokens {datetime} o {uuid} en el nombre para evitarlo.'
            ENG = '⚠ Each run replaces the previous file. Combine with {datetime} or {uuid} tokens in the name to avoid it.'
            FRA = '⚠ Chaque exécution remplace le fichier précédent. Combinez avec les jetons {datetime} ou {uuid} dans le nom pour l''éviter.'
            ITA = '⚠ Ogni esecuzione sostituisce il file precedente. Combina con i token {datetime} o {uuid} nel nome per evitarlo.'
        }
        'exportWizard.onFileExists.renameHint' = @{
            SPA = '✓ Crea ficheros incrementales: ej. report.xlsx, report_001.xlsx, report_002.xlsx… Útil para histórico sin perder datos.'
            ENG = '✓ Creates incremental files: e.g. report.xlsx, report_001.xlsx, report_002.xlsx… Useful for history without losing data.'
            FRA = '✓ Crée des fichiers incrémentaux : par ex. report.xlsx, report_001.xlsx, report_002.xlsx… Utile pour l''historique sans perte de données.'
            ITA = '✓ Crea file incrementali: es. report.xlsx, report_001.xlsx, report_002.xlsx… Utile per lo storico senza perdere dati.'
        }
        'exportWizard.onFileExists.skipHint' = @{
            SPA = 'Si ya existe un archivo con ese nombre, no se sobreescribe ni se crea uno nuevo.'
            ENG = 'If a file with that name already exists, it is neither overwritten nor a new one created.'
            FRA = 'Si un fichier portant ce nom existe déjà, il n''est ni écrasé ni remplacé par un nouveau.'
            ITA = 'Se esiste già un file con quel nome, non viene sovrascritto né ne viene creato uno nuovo.'
        }

        # --- Resumen ---------------------------------------------------------
        'exportWizard.s.onFileExists' = @{
            SPA = 'Si existe'
            ENG = 'If exists'
            FRA = 'Si existant'
            ITA = 'Se esiste'
        }
    }

    'EXPORT_TASKS' = [ordered]@{
        'exportTasks.confirmDeleteTitle' = @{
            SPA = '⚠️ Eliminar tarea'
            ENG = '⚠️ Delete task'
            FRA = '⚠️ Supprimer la tâche'
            ITA = '⚠️ Elimina attività'
        }
        'exportTasks.confirmDeleteBtn' = @{
            SPA = 'Eliminar'; ENG = 'Delete'; FRA = 'Supprimer'; ITA = 'Elimina'
        }
    }

    'EXPORT_DEST' = [ordered]@{
        'exportDest.folder.confirmDeleteTitle' = @{
            SPA = '⚠️ Eliminar perfil de carpeta'
            ENG = '⚠️ Delete folder profile'
            FRA = '⚠️ Supprimer le profil de dossier'
            ITA = '⚠️ Elimina profilo cartella'
        }
        'exportDest.email.confirmDeleteTitle' = @{
            SPA = '⚠️ Eliminar cuenta SMTP'
            ENG = '⚠️ Delete SMTP account'
            FRA = '⚠️ Supprimer le compte SMTP'
            ITA = '⚠️ Elimina account SMTP'
        }
        'exportDest.email.testPromptTitle' = @{
            SPA = '✉️ Probar ''{name}'''
            ENG = '✉️ Test ''{name}'''
            FRA = '✉️ Tester « {name} »'
            ITA = '✉️ Prova ''{name}'''
        }
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

    # Leer SIEMPRE como UTF-8 (PS 5.1 por defecto usa ANSI/CP1252 y corrompe acentos)
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
                if ($labels -contains $key) {
                    $skipped++
                } else {
                    $labels.Add($key)
                    $added++
                }
            } else {
                $added++
            }

            # Añadir / sustituir traducción
            $entry = $additions[$pageName][$key]
            $obj = [ordered]@{
                SPA = $entry.SPA
                ENG = $entry.ENG
                FRA = $entry.FRA
                ITA = $entry.ITA
            }
            if ($json.translations.PSObject.Properties.Name.Contains($key)) {
                $json.translations.$key = [pscustomobject]$obj
            } else {
                $json.translations | Add-Member -NotePropertyName $key -NotePropertyValue ([pscustomobject]$obj)
            }
        }
        if ($pageExists) { $page.labels = $labels.ToArray() }
    }

    # Actualizar lastModified
    $json.metadata.lastModified = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

    # Guardar de vuelta (mismo estilo que el resto del archivo: ConvertTo-Json -Depth 100)
    $out = $json | ConvertTo-Json -Depth 100
    # Backup
    Copy-Item -Path $file.FullName -Destination "$($file.FullName).bak" -Force
    # Escribir como UTF-8 SIN BOM (formato original del archivo)
    [System.IO.File]::WriteAllText($file.FullName, $out, $utf8NoBom)
    Write-Host "   ✓ añadidas $added · existentes $skipped (backup: $($file.Name).bak)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Hecho." -ForegroundColor Green
