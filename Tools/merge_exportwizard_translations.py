#!/usr/bin/env python3
"""Merge ExportManagerWizard translations into project translations.json.

One-shot script: idempotent — re-running just overwrites the same keys.
Run from repo root:
    py Tools/merge_exportwizard_translations.py
"""
import json
import sys
from collections import OrderedDict
from pathlib import Path

TARGET = Path(__file__).resolve().parent.parent / "Projects" / "A72.TOUTWP" / "translations" / "translations.json"

# All exportWizard.* keys with SPA (verbatim from React fallbacks) + ENG/FRA/ITA.
ENTRIES = {
    # --- Navigation / buttons -------------------------------------------------
    "exportWizard.title":      {"SPA": "Nueva tarea de exportación",  "ENG": "New export task",          "FRA": "Nouvelle tâche d'exportation", "ITA": "Nuova attività di esportazione"},
    "exportWizard.titleEdit":  {"SPA": "Editar tarea de exportación", "ENG": "Edit export task",         "FRA": "Modifier la tâche d'exportation", "ITA": "Modifica attività di esportazione"},
    "exportWizard.subtitle":   {"SPA": "Módulo: {source}",            "ENG": "Module: {source}",         "FRA": "Module : {source}",            "ITA": "Modulo: {source}"},
    "exportWizard.cancel":     {"SPA": "Cancelar",                    "ENG": "Cancel",                   "FRA": "Annuler",                      "ITA": "Annulla"},
    "exportWizard.back":       {"SPA": "Atrás",                       "ENG": "Back",                     "FRA": "Retour",                       "ITA": "Indietro"},
    "exportWizard.next":       {"SPA": "Siguiente",                   "ENG": "Next",                     "FRA": "Suivant",                      "ITA": "Avanti"},
    "exportWizard.save":       {"SPA": "Guardar tarea",               "ENG": "Save task",                "FRA": "Enregistrer la tâche",         "ITA": "Salva attività"},

    # --- Stepper --------------------------------------------------------------
    "exportWizard.step.what":       {"SPA": "1. Qué",            "ENG": "1. What",           "FRA": "1. Quoi",          "ITA": "1. Cosa"},
    "exportWizard.step.format":     {"SPA": "2. Formato",        "ENG": "2. Format",         "FRA": "2. Format",        "ITA": "2. Formato"},
    "exportWizard.step.dest":       {"SPA": "3. Destinos",       "ENG": "3. Destinations",   "FRA": "3. Destinations",  "ITA": "3. Destinazioni"},
    "exportWizard.step.configure":  {"SPA": "4. Configurar",     "ENG": "4. Configure",      "FRA": "4. Configurer",    "ITA": "4. Configura"},
    "exportWizard.step.automation": {"SPA": "5. Automatización", "ENG": "5. Automation",     "FRA": "5. Automatisation","ITA": "5. Automazione"},
    "exportWizard.step.summary":    {"SPA": "6. Resumen",        "ENG": "6. Summary",        "FRA": "6. Résumé",        "ITA": "6. Riepilogo"},

    # --- Step 0: Qué ----------------------------------------------------------
    "exportWizard.noDatasets":         {"SPA": "Este módulo no ha declarado ningún dataset exportable.", "ENG": "This module has not declared any exportable dataset.", "FRA": "Ce module n'a déclaré aucun jeu de données exportable.", "ITA": "Questo modulo non ha dichiarato alcun dataset esportabile."},
    "exportWizard.dataset":            {"SPA": "Dataset",                                                "ENG": "Dataset",                                              "FRA": "Jeu de données",                                        "ITA": "Dataset"},
    "exportWizard.fields":             {"SPA": "Campos a incluir",                                       "ENG": "Fields to include",                                    "FRA": "Champs à inclure",                                      "ITA": "Campi da includere"},
    "exportWizard.noFields":           {"SPA": "(Dataset sin campos seleccionables — se exporta tal cual)", "ENG": "(Dataset with no selectable fields — exported as-is)", "FRA": "(Jeu de données sans champs sélectionnables — exporté tel quel)", "ITA": "(Dataset senza campi selezionabili — esportato così com'è)"},
    "exportWizard.filters":            {"SPA": "Filtros",                                                "ENG": "Filters",                                              "FRA": "Filtres",                                               "ITA": "Filtri"},
    "exportWizard.preview":            {"SPA": "Previsualizar (5 filas)",                                "ENG": "Preview (5 rows)",                                     "FRA": "Aperçu (5 lignes)",                                     "ITA": "Anteprima (5 righe)"},
    "exportWizard.previewUnavailable": {"SPA": "Preview no disponible para este dataset",                "ENG": "Preview not available for this dataset",               "FRA": "Aperçu non disponible pour ce jeu de données",          "ITA": "Anteprima non disponibile per questo dataset"},
    "exportWizard.previewLocalEmpty":  {"SPA": "Sin datos para previsualizar.",                          "ENG": "No data to preview.",                                  "FRA": "Aucune donnée à prévisualiser.",                        "ITA": "Nessun dato da visualizzare in anteprima."},
    "exportWizard.previewLocalNote":   {"SPA": "Este dataset se exporta directamente desde la pantalla actual — no hay preview previo.", "ENG": "This dataset is exported directly from the current screen — no prior preview available.", "FRA": "Ce jeu de données est exporté directement depuis l'écran actuel — aucun aperçu préalable disponible.", "ITA": "Questo dataset viene esportato direttamente dalla schermata corrente — nessuna anteprima preliminare disponibile."},
    "exportWizard.previewTotal":       {"SPA": "{n} filas totales · mostrando {shown}",                  "ENG": "{n} total rows · showing {shown}",                     "FRA": "{n} lignes au total · affichage de {shown}",            "ITA": "{n} righe totali · mostrate {shown}"},

    # --- Step 1: Formato ------------------------------------------------------
    "exportWizard.formatTitle":   {"SPA": "Formato de salida",                                                          "ENG": "Output format",                                                                "FRA": "Format de sortie",                                                                 "ITA": "Formato di output"},
    "exportWizard.noPdfNote":     {"SPA": "Nota: PDF no se ofrece aquí (usa el botón \"Imprimir\" del modal anfitrión).", "ENG": "Note: PDF is not offered here (use the \"Print\" button in the host modal).",    "FRA": "Note : le PDF n'est pas proposé ici (utilisez le bouton « Imprimer » de la modale hôte).", "ITA": "Nota: il PDF non è disponibile qui (usa il pulsante \"Stampa\" della finestra ospite)."},
    "exportWizard.pngHiddenNote": {"SPA": "PNG no está disponible para este origen: solo se ofrece cuando el contenido es un gráfico.", "ENG": "PNG is not available for this source: it is only offered when the content is a chart.", "FRA": "PNG n'est pas disponible pour cette source : il n'est proposé que lorsque le contenu est un graphique.", "ITA": "PNG non è disponibile per questa origine: viene offerto solo quando il contenuto è un grafico."},

    # --- Step 2: Destinos -----------------------------------------------------
    "exportWizard.destTitle":              {"SPA": "Marca los destinos (al menos uno)",                                                                "ENG": "Select destinations (at least one)",                                                       "FRA": "Sélectionnez les destinations (au moins une)",                                              "ITA": "Seleziona le destinazioni (almeno una)"},
    "exportWizard.manageDestinations":     {"SPA": "Gestionar destinos",                                                                                "ENG": "Manage destinations",                                                                       "FRA": "Gérer les destinations",                                                                     "ITA": "Gestisci destinazioni"},
    "exportWizard.dest.local":             {"SPA": "Carpeta local o de red",                                                                            "ENG": "Local or network folder",                                                                   "FRA": "Dossier local ou réseau",                                                                    "ITA": "Cartella locale o di rete"},
    "exportWizard.dest.localDisabledV2":   {"SPA": "No hay carpetas configuradas — pulsa \"Gestionar destinos\" para añadir una",                       "ENG": "No folders configured — click \"Manage destinations\" to add one",                          "FRA": "Aucun dossier configuré — cliquez sur « Gérer les destinations » pour en ajouter un",        "ITA": "Nessuna cartella configurata — premi \"Gestisci destinazioni\" per aggiungerne una"},
    "exportWizard.dest.localOkV2":         {"SPA": "{n} perfil(es) de carpeta",                                                                         "ENG": "{n} folder profile(s)",                                                                     "FRA": "{n} profil(s) de dossier",                                                                   "ITA": "{n} profilo/i di cartella"},
    "exportWizard.dest.email":             {"SPA": "Email",                                                                                              "ENG": "Email",                                                                                     "FRA": "E-mail",                                                                                      "ITA": "Email"},
    "exportWizard.dest.emailDisabledV2":   {"SPA": "No hay cuentas SMTP configuradas — pulsa \"Gestionar destinos\" para añadir una",                  "ENG": "No SMTP accounts configured — click \"Manage destinations\" to add one",                    "FRA": "Aucun compte SMTP configuré — cliquez sur « Gérer les destinations » pour en ajouter un",    "ITA": "Nessun account SMTP configurato — premi \"Gestisci destinazioni\" per aggiungerne uno"},
    "exportWizard.dest.emailOkV2":         {"SPA": "{n} cuenta(s) SMTP",                                                                                 "ENG": "{n} SMTP account(s)",                                                                       "FRA": "{n} compte(s) SMTP",                                                                          "ITA": "{n} account SMTP"},

    # --- Step 3: Configurar ---------------------------------------------------
    "exportWizard.filename":           {"SPA": "Nombre del archivo",                                                              "ENG": "File name",                                                          "FRA": "Nom du fichier",                                                       "ITA": "Nome del file"},
    "exportWizard.tokensHelp":         {"SPA": "Tokens disponibles: {fecha} {hora} {datetime}. La extensión se añade automáticamente.", "ENG": "Available tokens: {fecha} {hora} {datetime}. The extension is added automatically.", "FRA": "Tokens disponibles : {fecha} {hora} {datetime}. L'extension est ajoutée automatiquement.", "ITA": "Token disponibili: {fecha} {hora} {datetime}. L'estensione viene aggiunta automaticamente."},
    "exportWizard.localCfg":           {"SPA": "Carpeta destino",                                                                  "ENG": "Destination folder",                                                  "FRA": "Dossier de destination",                                               "ITA": "Cartella di destinazione"},
    "exportWizard.newFolderProfile":   {"SPA": "Nueva carpeta",                                                                    "ENG": "New folder",                                                          "FRA": "Nouveau dossier",                                                      "ITA": "Nuova cartella"},
    "exportWizard.noFolderProfiles":   {"SPA": "Sin perfiles de carpeta. Crea uno con \"Gestionar destinos\".",                  "ENG": "No folder profiles. Create one with \"Manage destinations\".",        "FRA": "Aucun profil de dossier. Créez-en un via « Gérer les destinations ».", "ITA": "Nessun profilo di cartella. Creane uno con \"Gestisci destinazioni\"."},
    "exportWizard.folderResolved":     {"SPA": "Ruta final",                                                                       "ENG": "Final path",                                                          "FRA": "Chemin final",                                                         "ITA": "Percorso finale"},
    "exportWizard.emailCfg":           {"SPA": "Configuración de email",                                                           "ENG": "Email configuration",                                                 "FRA": "Configuration de l'e-mail",                                            "ITA": "Configurazione email"},
    "exportWizard.newEmailProfile":    {"SPA": "Nueva cuenta",                                                                     "ENG": "New account",                                                         "FRA": "Nouveau compte",                                                       "ITA": "Nuovo account"},
    "exportWizard.noEmailProfiles":    {"SPA": "Sin cuentas SMTP. Crea una con \"Gestionar destinos\".",                          "ENG": "No SMTP accounts. Create one with \"Manage destinations\".",          "FRA": "Aucun compte SMTP. Créez-en un via « Gérer les destinations ».",       "ITA": "Nessun account SMTP. Creane uno con \"Gestisci destinazioni\"."},
    "exportWizard.emailProfile":       {"SPA": "Cuenta SMTP",                                                                      "ENG": "SMTP account",                                                        "FRA": "Compte SMTP",                                                          "ITA": "Account SMTP"},
    "exportWizard.defaultRecipients":  {"SPA": "Destinatarios por defecto del perfil",                                             "ENG": "Default recipients of the profile",                                   "FRA": "Destinataires par défaut du profil",                                   "ITA": "Destinatari predefiniti del profilo"},
    "exportWizard.email.to":           {"SPA": "Para (To) — vacío = usar los del perfil",                                          "ENG": "To — empty = use those from the profile",                             "FRA": "À — vide = utiliser ceux du profil",                                   "ITA": "A — vuoto = usa quelli del profilo"},
    "exportWizard.email.cc":           {"SPA": "CC",                                                                                "ENG": "CC",                                                                  "FRA": "CC",                                                                    "ITA": "CC"},
    "exportWizard.email.bcc":          {"SPA": "CCO",                                                                               "ENG": "BCC",                                                                 "FRA": "CCI",                                                                   "ITA": "CCN"},
    "exportWizard.email.subject":      {"SPA": "Asunto",                                                                            "ENG": "Subject",                                                             "FRA": "Objet",                                                                 "ITA": "Oggetto"},
    "exportWizard.email.body":         {"SPA": "Cuerpo",                                                                            "ENG": "Body",                                                                "FRA": "Corps",                                                                 "ITA": "Corpo"},

    # --- Report design (XLSX + HTML) -----------------------------------------
    "exportWizard.reportDesign":             {"SPA": "Diseño del informe",                            "ENG": "Report design",                          "FRA": "Conception du rapport",                "ITA": "Progettazione del report"},
    "exportWizard.report.includeHeader":     {"SPA": "Incluir cabecera (logo + título)",              "ENG": "Include header (logo + title)",          "FRA": "Inclure l'en-tête (logo + titre)",     "ITA": "Includi intestazione (logo + titolo)"},
    "exportWizard.report.title":             {"SPA": "Título",                                        "ENG": "Title",                                  "FRA": "Titre",                                "ITA": "Titolo"},
    "exportWizard.report.subtitle":          {"SPA": "Subtítulo",                                     "ENG": "Subtitle",                               "FRA": "Sous-titre",                           "ITA": "Sottotitolo"},
    "exportWizard.report.companyName":       {"SPA": "Nombre de empresa",                             "ENG": "Company name",                           "FRA": "Nom de l'entreprise",                  "ITA": "Nome dell'azienda"},
    "exportWizard.report.logo":              {"SPA": "Logo (PNG/JPG, máx 1 MB)",                      "ENG": "Logo (PNG/JPG, max 1 MB)",               "FRA": "Logo (PNG/JPG, max 1 Mo)",             "ITA": "Logo (PNG/JPG, max 1 MB)"},
    "exportWizard.report.noLogo":            {"SPA": "Sin logo",                                      "ENG": "No logo",                                "FRA": "Aucun logo",                           "ITA": "Nessun logo"},
    "exportWizard.report.removeLogo":        {"SPA": "Quitar",                                        "ENG": "Remove",                                 "FRA": "Supprimer",                            "ITA": "Rimuovi"},
    "exportWizard.report.showDate":          {"SPA": "Mostrar fecha de generación",                   "ENG": "Show generation date",                   "FRA": "Afficher la date de génération",       "ITA": "Mostra data di generazione"},
    "exportWizard.report.showProject":       {"SPA": "Mostrar nombre de proyecto",                    "ENG": "Show project name",                      "FRA": "Afficher le nom du projet",            "ITA": "Mostra nome del progetto"},
    "exportWizard.report.includeFilters":    {"SPA": "Incluir bloque \"Filtros aplicados\"",          "ENG": "Include \"Applied filters\" block",      "FRA": "Inclure le bloc « Filtres appliqués »","ITA": "Includi blocco \"Filtri applicati\""},
    "exportWizard.report.summary":           {"SPA": "Totales / resumen",                             "ENG": "Totals / summary",                       "FRA": "Totaux / résumé",                      "ITA": "Totali / riepilogo"},
    "exportWizard.report.summaryOff":        {"SPA": "Sin resumen",                                   "ENG": "No summary",                             "FRA": "Aucun résumé",                         "ITA": "Nessun riepilogo"},
    "exportWizard.report.summaryAuto":       {"SPA": "Automático (todas las columnas numéricas)",     "ENG": "Automatic (all numeric columns)",        "FRA": "Automatique (toutes les colonnes numériques)", "ITA": "Automatico (tutte le colonne numeriche)"},
    "exportWizard.report.summaryManual":     {"SPA": "Manual (elegir columnas)",                      "ENG": "Manual (choose columns)",                "FRA": "Manuel (choisir les colonnes)",        "ITA": "Manuale (scegli le colonne)"},
    "exportWizard.report.aggregations":      {"SPA": "Operaciones",                                   "ENG": "Operations",                             "FRA": "Opérations",                           "ITA": "Operazioni"},
    "exportWizard.report.summaryCols":       {"SPA": "Columnas a resumir",                            "ENG": "Columns to summarize",                   "FRA": "Colonnes à résumer",                   "ITA": "Colonne da riepilogare"},
    "exportWizard.report.includeFooter":     {"SPA": "Incluir pie de página",                         "ENG": "Include footer",                         "FRA": "Inclure le pied de page",              "ITA": "Includi piè di pagina"},
    "exportWizard.report.footerPh":          {"SPA": "Ej: Confidencial — Aquafrisch S.L.",            "ENG": "e.g.: Confidential — Aquafrisch S.L.",   "FRA": "ex. : Confidentiel — Aquafrisch S.L.", "ITA": "es.: Riservato — Aquafrisch S.L."},
    "exportWizard.report.headerColor":       {"SPA": "Color cabecera",                                "ENG": "Header color",                           "FRA": "Couleur de l'en-tête",                 "ITA": "Colore intestazione"},
    "exportWizard.report.accentColor":       {"SPA": "Color acento",                                  "ENG": "Accent color",                           "FRA": "Couleur d'accentuation",               "ITA": "Colore accento"},
    "exportWizard.logoTooLarge":             {"SPA": "El logo no puede superar 1 MB.",                "ENG": "The logo cannot exceed 1 MB.",           "FRA": "Le logo ne peut pas dépasser 1 Mo.",   "ITA": "Il logo non può superare 1 MB."},

    # --- Step 4: Automatización ----------------------------------------------
    "exportWizard.autoTitle":         {"SPA": "Cómo se ejecuta la tarea",                                                                                                "ENG": "How the task runs",                                                                                                       "FRA": "Comment la tâche s'exécute",                                                                                                "ITA": "Come viene eseguita l'attività"},
    "exportWizard.mode.manual":       {"SPA": "Manual",                                                                                                                  "ENG": "Manual",                                                                                                                  "FRA": "Manuel",                                                                                                                    "ITA": "Manuale"},
    "exportWizard.mode.manualDesc":   {"SPA": "Se lanza desde la lista de tareas",                                                                                       "ENG": "Launched from the task list",                                                                                            "FRA": "Lancée depuis la liste des tâches",                                                                                         "ITA": "Avviata dall'elenco delle attività"},
    "exportWizard.mode.cron":         {"SPA": "Programada (Cron)",                                                                                                       "ENG": "Scheduled (Cron)",                                                                                                       "FRA": "Planifiée (Cron)",                                                                                                          "ITA": "Pianificata (Cron)"},
    "exportWizard.mode.cronDesc":     {"SPA": "Horarios fijos: cada X tiempo, hora, día",                                                                                "ENG": "Fixed schedule: every X time, hour, day",                                                                                "FRA": "Horaires fixes : tous les X temps, heure, jour",                                                                            "ITA": "Orari fissi: ogni X tempo, ora, giorno"},
    "exportWizard.mode.plc":          {"SPA": "Trigger PLC",                                                                                                             "ENG": "PLC trigger",                                                                                                            "FRA": "Déclencheur PLC",                                                                                                           "ITA": "Trigger PLC"},
    "exportWizard.mode.plcDesc":      {"SPA": "Flanco false→true de variable BOOL",                                                                                      "ENG": "false→true edge of a BOOL variable",                                                                                     "FRA": "Front false→true d'une variable BOOL",                                                                                      "ITA": "Fronte false→true di una variabile BOOL"},
    "exportWizard.dateFilterIgnored": {"SPA": "El rango de fechas configurado en el paso 2 se ignorará: en ejecución programada o por trigger PLC el periodo se decide en el momento del disparo.", "ENG": "The date range configured in step 2 will be ignored: with scheduled or PLC-triggered execution the period is decided at trigger time.", "FRA": "La plage de dates configurée à l'étape 2 sera ignorée : en exécution planifiée ou déclenchée par PLC, la période est décidée au moment du déclenchement.", "ITA": "L'intervallo di date configurato al passo 2 verrà ignorato: in esecuzione pianificata o tramite trigger PLC il periodo viene deciso al momento dello scatto."},
    "exportWizard.plcCfg":            {"SPA": "Variable PLC trigger",                                                                                                    "ENG": "PLC trigger variable",                                                                                                   "FRA": "Variable de déclenchement PLC",                                                                                             "ITA": "Variabile trigger PLC"},
    "exportWizard.plcVarPlaceholder": {"SPA": "MAIN.fbMachine.bExportTrigger",                                                                                            "ENG": "MAIN.fbMachine.bExportTrigger",                                                                                          "FRA": "MAIN.fbMachine.bExportTrigger",                                                                                             "ITA": "MAIN.fbMachine.bExportTrigger"},
    "exportWizard.plcHelp":           {"SPA": "La tarea se disparará automáticamente cuando la variable pase de FALSE a TRUE. No se dispara con el primer sample tras reinicio del backend.", "ENG": "The task will fire automatically when the variable goes from FALSE to TRUE. It does not fire on the first sample after backend restart.", "FRA": "La tâche se déclenchera automatiquement lorsque la variable passe de FALSE à TRUE. Elle ne se déclenche pas au premier échantillon après le redémarrage du backend.", "ITA": "L'attività si attiverà automaticamente quando la variabile passa da FALSE a TRUE. Non si attiva al primo campione dopo il riavvio del backend."},
    "exportWizard.plcNoVarsHint":     {"SPA": "Sugerencia: no hay variables BOOL en PLC_Variables; escribe la ruta completa (ej. MAIN.fbMachine.bExportTrigger). El backend la suscribirá al guardar la tarea.", "ENG": "Hint: there are no BOOL variables in PLC_Variables; type the full path (e.g. MAIN.fbMachine.bExportTrigger). The backend will subscribe to it when the task is saved.", "FRA": "Astuce : aucune variable BOOL dans PLC_Variables ; saisissez le chemin complet (ex. MAIN.fbMachine.bExportTrigger). Le backend s'y abonnera lors de l'enregistrement de la tâche.", "ITA": "Suggerimento: non ci sono variabili BOOL in PLC_Variables; digita il percorso completo (es. MAIN.fbMachine.bExportTrigger). Il backend si iscriverà al salvataggio dell'attività."},

    "exportWizard.cronCfg":     {"SPA": "Expresión cron (5 campos)",                                                                                                   "ENG": "Cron expression (5 fields)",                                                                                            "FRA": "Expression cron (5 champs)",                                                                                                  "ITA": "Espressione cron (5 campi)"},
    "exportWizard.cronHelp":    {"SPA": "Formato: minuto hora día mes día-semana — admite *, listas (1,5), rangos (1-5), pasos (*/15). Hora local del servidor.",     "ENG": "Format: minute hour day month day-of-week — accepts *, lists (1,5), ranges (1-5), steps (*/15). Server local time.",     "FRA": "Format : minute heure jour mois jour-semaine — accepte *, listes (1,5), plages (1-5), pas (*/15). Heure locale du serveur.", "ITA": "Formato: minuto ora giorno mese giorno-settimana — supporta *, liste (1,5), intervalli (1-5), passi (*/15). Ora locale del server."},
    "exportWizard.cronChecking":{"SPA": "Validando…",                                                                                                                   "ENG": "Validating…",                                                                                                            "FRA": "Validation…",                                                                                                                  "ITA": "Validazione…"},
    "exportWizard.cronOk":      {"SPA": "Expresión válida",                                                                                                             "ENG": "Valid expression",                                                                                                       "FRA": "Expression valide",                                                                                                            "ITA": "Espressione valida"},

    # Cron presets (new — wrapped via tLabel)
    "exportWizard.cron.preset.hourly":       {"SPA": "cada hora",            "ENG": "every hour",            "FRA": "toutes les heures",   "ITA": "ogni ora"},
    "exportWizard.cron.preset.q15":          {"SPA": "cada 15 min",          "ENG": "every 15 min",          "FRA": "toutes les 15 min",   "ITA": "ogni 15 min"},
    "exportWizard.cron.preset.daily00":      {"SPA": "diario 00:00",         "ENG": "daily 00:00",           "FRA": "quotidien 00:00",     "ITA": "giornaliero 00:00"},
    "exportWizard.cron.preset.daily08":      {"SPA": "cada día 08:00",       "ENG": "every day 08:00",       "FRA": "chaque jour 08:00",   "ITA": "ogni giorno 08:00"},
    "exportWizard.cron.preset.weekdays0730": {"SPA": "lun-vie 07:30",        "ENG": "Mon-Fri 07:30",         "FRA": "lun-ven 07:30",       "ITA": "lun-ven 07:30"},
    "exportWizard.cron.preset.weeklyMon00":  {"SPA": "semanal lunes 00:00",  "ENG": "weekly Monday 00:00",   "FRA": "hebdo lundi 00:00",   "ITA": "settimanale lunedì 00:00"},
    "exportWizard.cron.preset.mon09":        {"SPA": "cada lunes 09:00",     "ENG": "every Monday 09:00",    "FRA": "chaque lundi 09:00",  "ITA": "ogni lunedì 09:00"},
    "exportWizard.cron.preset.monthly1":     {"SPA": "mensual día 1 00:00",  "ENG": "monthly day 1 00:00",   "FRA": "mensuel le 1er 00:00","ITA": "mensile giorno 1 00:00"},
    "exportWizard.cron.preset.yearly":       {"SPA": "anual 1 enero 00:00",  "ENG": "yearly Jan 1 00:00",    "FRA": "annuel 1er janv. 00:00","ITA": "annuale 1° gennaio 00:00"},

    # --- Step 5: Resumen ------------------------------------------------------
    "exportWizard.taskName":   {"SPA": "Nombre descriptivo de la tarea *",                  "ENG": "Descriptive task name *",                  "FRA": "Nom descriptif de la tâche *",                  "ITA": "Nome descrittivo dell'attività *"},
    "exportWizard.taskNamePh": {"SPA": "Ej: Auditoría mensual a logs@empresa.com",          "ENG": "e.g.: Monthly audit to logs@company.com",  "FRA": "ex. : Audit mensuel vers logs@entreprise.com",  "ITA": "es.: Audit mensile a logs@azienda.com"},
    "exportWizard.s.dataset":  {"SPA": "Dataset",                                            "ENG": "Dataset",                                  "FRA": "Jeu de données",                                "ITA": "Dataset"},
    "exportWizard.s.fields":   {"SPA": "Campos",                                             "ENG": "Fields",                                   "FRA": "Champs",                                         "ITA": "Campi"},
    "exportWizard.s.format":   {"SPA": "Formato",                                            "ENG": "Format",                                   "FRA": "Format",                                         "ITA": "Formato"},
    "exportWizard.s.dest":     {"SPA": "Destinos",                                           "ENG": "Destinations",                             "FRA": "Destinations",                                   "ITA": "Destinazioni"},
    "exportWizard.s.filename": {"SPA": "Archivo",                                            "ENG": "File",                                     "FRA": "Fichier",                                        "ITA": "File"},
    "exportWizard.s.folder":   {"SPA": "Carpeta",                                            "ENG": "Folder",                                   "FRA": "Dossier",                                        "ITA": "Cartella"},
    "exportWizard.s.emailTo":  {"SPA": "Para",                                               "ENG": "To",                                       "FRA": "À",                                              "ITA": "A"},
    "exportWizard.s.execType": {"SPA": "Ejecución",                                          "ENG": "Execution",                                "FRA": "Exécution",                                      "ITA": "Esecuzione"},
    "exportWizard.s.plc":      {"SPA": "Trigger PLC: {v}",                                   "ENG": "PLC trigger: {v}",                         "FRA": "Déclencheur PLC : {v}",                          "ITA": "Trigger PLC: {v}"},
    "exportWizard.s.cron":     {"SPA": "Cron: {v}",                                          "ENG": "Cron: {v}",                                "FRA": "Cron : {v}",                                     "ITA": "Cron: {v}"},
    "exportWizard.s.manual":   {"SPA": "Manual (▶ desde la lista de tareas)",                "ENG": "Manual (▶ from the task list)",            "FRA": "Manuel (▶ depuis la liste des tâches)",          "ITA": "Manuale (▶ dall'elenco delle attività)"},
}


def main():
    if not TARGET.exists():
        print(f"ERROR: {TARGET} not found", file=sys.stderr)
        return 1

    # Preserve key order in the file by loading as ordered dict.
    with TARGET.open("r", encoding="utf-8") as f:
        data = json.load(f, object_pairs_hook=OrderedDict)

    translations = data.get("translations")
    if translations is None:
        print("ERROR: 'translations' key missing", file=sys.stderr)
        return 1

    added = 0
    updated = 0
    for k, v in ENTRIES.items():
        if k in translations:
            updated += 1
        else:
            added += 1
        translations[k] = OrderedDict([
            ("SPA", v["SPA"]),
            ("ENG", v["ENG"]),
            ("FRA", v["FRA"]),
            ("ITA", v["ITA"]),
        ])

    # Register a documentation page entry (idempotent).
    pages = data.get("pages")
    if pages is not None:
        pages["EXPORT_WIZARD"] = OrderedDict([
            ("description", "Wizard de exportación (Aquafrisch Export Manager)"),
            ("labels", list(ENTRIES.keys())),
        ])

    # Bump lastModified
    md = data.get("metadata")
    if md is not None:
        md["lastModified"] = "2026-05-27T00:00:00Z"

    with TARGET.open("w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=4)
        f.write("\n")

    print(f"OK: {added} added, {updated} updated. Total exportWizard keys: {len(ENTRIES)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
