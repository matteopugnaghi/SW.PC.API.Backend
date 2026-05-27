#!/usr/bin/env python3
"""Merge ExportManagerWizard translations into project translations.json.

Idempotent — re-running just overwrites the same keys.

Usage (from repo root):
    py Tools/merge_exportwizard_translations.py                  # default project (A72.TOUTWP)
    py Tools/merge_exportwizard_translations.py A70.SOMEPROJECT  # one specific project
    py Tools/merge_exportwizard_translations.py --all            # every Projects/*/translations/translations.json found
"""
import json
import sys
from collections import OrderedDict
from pathlib import Path

PROJECTS_ROOT = Path(__file__).resolve().parent.parent / "Projects"
DEFAULT_PROJECT = "A72.TOUTWP"

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
    "exportWizard.saveChanges":        {"SPA": "Guardar cambios",                                         "ENG": "Save changes",                                          "FRA": "Enregistrer les modifications",                              "ITA": "Salva modifiche"},
    "exportWizard.saveChangesTooltip": {"SPA": "Guardar cambios sin recorrer el resto del wizard",       "ENG": "Save changes without stepping through the rest of the wizard", "FRA": "Enregistrer les modifications sans parcourir le reste de l'assistant", "ITA": "Salva le modifiche senza percorrere il resto della procedura"},
    "exportWizard.saveChangesBlocked": {"SPA": "Hay pasos sin completar",                                "ENG": "Some steps are incomplete",                             "FRA": "Certaines étapes sont incomplètes",                          "ITA": "Alcuni passaggi sono incompleti"},

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
    "exportWizard.dateRange.absolute": {"SPA": "Absoluto",                                               "ENG": "Absolute",                                             "FRA": "Absolu",                                                "ITA": "Assoluto"},
    "exportWizard.dateRange.relative": {"SPA": "Relativo",                                               "ENG": "Relative",                                             "FRA": "Relatif",                                               "ITA": "Relativo"},
    "exportWizard.dateRange.from":     {"SPA": "Desde",                                                  "ENG": "From",                                                 "FRA": "Du",                                                    "ITA": "Da"},
    "exportWizard.dateRange.to":       {"SPA": "Hasta",                                                  "ENG": "To",                                                   "FRA": "Au",                                                    "ITA": "A"},
    "exportWizard.dateRange.last":     {"SPA": "Últimas",                                                "ENG": "Last",                                                 "FRA": "Dernières",                                             "ITA": "Ultime"},
    "exportWizard.dateRange.unit.m":   {"SPA": "minutos",                                                "ENG": "minutes",                                              "FRA": "minutes",                                               "ITA": "minuti"},
    "exportWizard.dateRange.unit.h":   {"SPA": "horas",                                                  "ENG": "hours",                                                "FRA": "heures",                                                "ITA": "ore"},
    "exportWizard.dateRange.unit.d":   {"SPA": "días",                                                   "ENG": "days",                                                 "FRA": "jours",                                                 "ITA": "giorni"},
    "exportWizard.preview":            {"SPA": "Previsualizar (5 filas)",                                "ENG": "Preview (5 rows)",                                     "FRA": "Aperçu (5 lignes)",                                     "ITA": "Anteprima (5 righe)"},
    "exportWizard.previewUnavailable": {"SPA": "Preview no disponible para este dataset",                "ENG": "Preview not available for this dataset",               "FRA": "Aperçu non disponible pour ce jeu de données",          "ITA": "Anteprima non disponibile per questo dataset"},
    "exportWizard.previewLocalEmpty":  {"SPA": "Sin datos para previsualizar.",                          "ENG": "No data to preview.",                                  "FRA": "Aucune donnée à prévisualiser.",                        "ITA": "Nessun dato da visualizzare in anteprima."},
    "exportWizard.previewLocalNote":   {"SPA": "Este dataset se exporta directamente desde la pantalla actual — no hay preview previo.", "ENG": "This dataset is exported directly from the current screen — no prior preview available.", "FRA": "Ce jeu de données est exporté directement depuis l'écran actuel — aucun aperçu préalable disponible.", "ITA": "Questo dataset viene esportato direttamente dalla schermata corrente — nessuna anteprima preliminare disponibile."},
    "exportWizard.previewTotal":       {"SPA": "{n} filas totales · mostrando {shown}",                  "ENG": "{n} total rows · showing {shown}",                     "FRA": "{n} lignes au total · affichage de {shown}",            "ITA": "{n} righe totali · mostrate {shown}"},

    # --- Step 1: Formato ------------------------------------------------------
    "exportWizard.formatTitle":   {"SPA": "Formato de salida",                                                          "ENG": "Output format",                                                                "FRA": "Format de sortie",                                                                 "ITA": "Formato di output"},
    "exportWizard.noPdfNote":     {"SPA": "Nota: PDF no se ofrece aquí (usa el botón \"Imprimir\" del modal anfitrión).", "ENG": "Note: PDF is not offered here (use the \"Print\" button in the host modal).",    "FRA": "Note : le PDF n'est pas proposé ici (utilisez le bouton « Imprimer » de la modale hôte).", "ITA": "Nota: il PDF non è disponibile qui (usa il pulsante \"Stampa\" della finestra ospite)."},
    "exportWizard.pngHiddenNote": {"SPA": "PNG no está disponible para este origen: solo se ofrece cuando el contenido es un gráfico.", "ENG": "PNG is not available for this source: it is only offered when the content is a chart.", "FRA": "PNG n'est pas disponible pour cette source : il n'est proposé que lorsque le contenu est un graphique.", "ITA": "PNG non è disponibile per questa origine: viene offerto solo quando il contenuto è un grafico."},
    "exportWizard.languageTitle": {"SPA": "Idioma del documento exportado", "ENG": "Exported document language", "FRA": "Langue du document exporté", "ITA": "Lingua del documento esportato"},
    "exportWizard.languageNote":  {"SPA": "Las cabeceras del archivo (CSV/XLSX) se generarán en este idioma. Los nombres de variables OPC/UA se mantienen como en el PLC.", "ENG": "File headers (CSV/XLSX) will be generated in this language. OPC/UA variable names are kept as in the PLC.", "FRA": "Les en-têtes du fichier (CSV/XLSX) seront générés dans cette langue. Les noms des variables OPC/UA restent ceux du PLC.", "ITA": "Le intestazioni del file (CSV/XLSX) saranno generate in questa lingua. I nomi delle variabili OPC/UA restano come nel PLC."},

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

    # ═══ ExportTasksPanel (lista de tareas persistentes) ════════════════════════
    "exportTasks.title":                {"SPA": "Gestor de exportaciones",                                                 "ENG": "Export manager",                                                       "FRA": "Gestionnaire d'exportations",                                              "ITA": "Gestore esportazioni"},
    "exportTasks.subtitle":             {"SPA": "Tareas del módulo: {source}",                                             "ENG": "Module tasks: {source}",                                               "FRA": "Tâches du module : {source}",                                              "ITA": "Attività del modulo: {source}"},
    "exportTasks.loading":              {"SPA": "Cargando…",                                                               "ENG": "Loading…",                                                             "FRA": "Chargement…",                                                              "ITA": "Caricamento…"},
    "exportTasks.count":                {"SPA": "{count} tarea(s)",                                                        "ENG": "{count} task(s)",                                                      "FRA": "{count} tâche(s)",                                                         "ITA": "{count} attività"},
    "exportTasks.refresh":              {"SPA": "Recargar",                                                                "ENG": "Reload",                                                               "FRA": "Recharger",                                                                "ITA": "Ricarica"},
    "exportTasks.noDatasets":           {"SPA": "El módulo no ha declarado datasets exportables",                          "ENG": "The module has not declared any exportable datasets",                  "FRA": "Le module n'a déclaré aucun jeu de données exportable",                    "ITA": "Il modulo non ha dichiarato dataset esportabili"},
    "exportTasks.new":                  {"SPA": "Nueva tarea",                                                             "ENG": "New task",                                                             "FRA": "Nouvelle tâche",                                                           "ITA": "Nuova attività"},
    "exportTasks.empty":                {"SPA": "No hay tareas configuradas todavía. Pulsa \"Nueva tarea\" para crear una.","ENG": "No tasks configured yet. Click \"New task\" to create one.",            "FRA": "Aucune tâche configurée pour le moment. Cliquez sur « Nouvelle tâche ».","ITA": "Nessuna attività configurata. Clicca \"Nuova attività\" per crearne una."},
    "exportTasks.paused":               {"SPA": "Pausada",                                                                 "ENG": "Paused",                                                               "FRA": "En pause",                                                                 "ITA": "In pausa"},
    "exportTasks.lastRun":              {"SPA": "Última ejecución",                                                        "ENG": "Last run",                                                             "FRA": "Dernière exécution",                                                       "ITA": "Ultima esecuzione"},
    "exportTasks.never":                {"SPA": "nunca",                                                                   "ENG": "never",                                                                "FRA": "jamais",                                                                   "ITA": "mai"},
    "exportTasks.run":                  {"SPA": "Ejecutar",                                                                "ENG": "Run",                                                                  "FRA": "Exécuter",                                                                 "ITA": "Esegui"},
    "exportTasks.edit":                 {"SPA": "Editar",                                                                  "ENG": "Edit",                                                                 "FRA": "Modifier",                                                                 "ITA": "Modifica"},
    "exportTasks.pause":                {"SPA": "Pausar",                                                                  "ENG": "Pause",                                                                "FRA": "Mettre en pause",                                                          "ITA": "Pausa"},
    "exportTasks.resume":               {"SPA": "Reanudar",                                                                "ENG": "Resume",                                                               "FRA": "Reprendre",                                                                "ITA": "Riprendi"},
    "exportTasks.delete":               {"SPA": "Eliminar",                                                                "ENG": "Delete",                                                               "FRA": "Supprimer",                                                                "ITA": "Elimina"},
    "exportTasks.confirmDelete":        {"SPA": "¿Eliminar la tarea \"{name}\"? Esta acción no se puede deshacer.",        "ENG": "Delete task \"{name}\"? This action cannot be undone.",                "FRA": "Supprimer la tâche « {name} » ? Cette action est irréversible.",          "ITA": "Eliminare l'attività \"{name}\"? L'azione non può essere annullata."},
    "exportTasks.runPrompt.title":      {"SPA": "Ejecutar tarea",                                                          "ENG": "Run task",                                                             "FRA": "Exécuter la tâche",                                                        "ITA": "Esegui attività"},
    "exportTasks.runPrompt.help":       {"SPA": "Selecciona el rango de fechas para esta ejecución. Dejar vacío exporta todos los datos disponibles.", "ENG": "Select the date range for this run. Leave empty to export all available data.", "FRA": "Sélectionnez la plage de dates pour cette exécution. Laisser vide exporte toutes les données disponibles.", "ITA": "Seleziona l'intervallo di date per questa esecuzione. Lasciare vuoto esporta tutti i dati disponibili."},
    "exportTasks.runPrompt.from":       {"SPA": "Desde",                                                                   "ENG": "From",                                                                 "FRA": "Du",                                                                       "ITA": "Da"},
    "exportTasks.runPrompt.to":         {"SPA": "Hasta",                                                                   "ENG": "To",                                                                   "FRA": "Au",                                                                       "ITA": "A"},
    "exportTasks.runPrompt.invalid":    {"SPA": "\"Desde\" no puede ser posterior a \"Hasta\".",                           "ENG": "\"From\" cannot be later than \"To\".",                                "FRA": "« Du » ne peut pas être postérieur à « Au ».",                            "ITA": "\"Da\" non può essere successivo a \"A\"."},
    "exportTasks.runPrompt.cancel":     {"SPA": "Cancelar",                                                                "ENG": "Cancel",                                                               "FRA": "Annuler",                                                                  "ITA": "Annulla"},
    "exportTasks.runPrompt.confirm":    {"SPA": "Ejecutar",                                                                "ENG": "Run",                                                                  "FRA": "Exécuter",                                                                 "ITA": "Esegui"},

    # ═══ ExportModal (modal anfitrión con los 3 botones de exportación) ════════
    "exportModal.header.title":            {"SPA": "Exportar",                              "ENG": "Export",                              "FRA": "Exporter",                                "ITA": "Esporta"},
    "exportModal.header.rowsCount_one":    {"SPA": "{count} fila a exportar",               "ENG": "{count} row to export",               "FRA": "{count} ligne à exporter",                "ITA": "{count} riga da esportare"},
    "exportModal.header.rowsCount_other":  {"SPA": "{count} filas a exportar",              "ENG": "{count} rows to export",              "FRA": "{count} lignes à exporter",               "ITA": "{count} righe da esportare"},
    "exportModal.content.title":           {"SPA": "Contenido a exportar",                  "ENG": "Content to export",                   "FRA": "Contenu à exporter",                      "ITA": "Contenuto da esportare"},
    "exportModal.content.empty":           {"SPA": "Sin datos disponibles.",                "ENG": "No data available.",                  "FRA": "Aucune donnée disponible.",               "ITA": "Nessun dato disponibile."},
    "exportModal.content.help":            {"SPA": "Imprimir genera el informe completo en PDF. El QR codifica un correo al soporte; si el contenido es muy largo se recorta automáticamente (más nuevo primero).", "ENG": "Print generates the full PDF report. The QR encodes a support email; if the content is too long it is automatically truncated (newest first).", "FRA": "Imprimer génère le rapport PDF complet. Le QR encode un e-mail au support ; si le contenu est trop long, il est tronqué automatiquement (le plus récent en premier).", "ITA": "Stampa genera il report PDF completo. Il QR codifica un'e-mail al supporto; se il contenuto è troppo lungo viene troncato automaticamente (più recente per primo)."},
    "exportModal.channel.sectionTitle":    {"SPA": "Vía de envío",                          "ENG": "Delivery channel",                    "FRA": "Voie d'envoi",                            "ITA": "Canale di invio"},
    "exportModal.channel.preparing":       {"SPA": "preparando…",                           "ENG": "preparing…",                          "FRA": "préparation…",                            "ITA": "preparazione…"},
    "exportModal.channel.print.label":     {"SPA": "Imprimir / PDF",                        "ENG": "Print / PDF",                         "FRA": "Imprimer / PDF",                          "ITA": "Stampa / PDF"},
    "exportModal.channel.print.sub":       {"SPA": "Diálogo navegador",                     "ENG": "Browser dialog",                      "FRA": "Boîte de dialogue du navigateur",         "ITA": "Finestra del browser"},
    "exportModal.channel.qr.label":        {"SPA": "Generar QR",                            "ENG": "Generate QR",                         "FRA": "Générer un QR",                           "ITA": "Genera QR"},
    "exportModal.channel.qr.sub":          {"SPA": "Abre Mail en el móvil",                 "ENG": "Opens Mail on mobile",                "FRA": "Ouvre Mail sur le mobile",                "ITA": "Apre Mail sul cellulare"},
    "exportModal.channel.email.label":     {"SPA": "Enviar email",                          "ENG": "Send email",                          "FRA": "Envoyer un e-mail",                       "ITA": "Invia e-mail"},
    "exportModal.channel.email.sub":       {"SPA": "Pendiente SMTP",                        "ENG": "SMTP pending",                        "FRA": "SMTP en attente",                         "ITA": "SMTP in attesa"},
    "exportModal.channel.manager.label":   {"SPA": "Gestor de exportaciones",               "ENG": "Export manager",                      "FRA": "Gestionnaire d'exportations",             "ITA": "Gestore esportazioni"},
    "exportModal.channel.manager.sub":     {"SPA": "Tareas persistentes",                   "ENG": "Persistent tasks",                    "FRA": "Tâches persistantes",                     "ITA": "Attività persistenti"},
    "exportModal.channel.kioskNotice":     {"SPA": "Sistema en kiosko: \"Email\" deshabilitado en SystemConfig (Excel).", "ENG": "Kiosk system: \"Email\" disabled in SystemConfig (Excel).", "FRA": "Système en mode kiosque : « E-mail » désactivé dans SystemConfig (Excel).", "ITA": "Sistema in modalità chiosco: \"Email\" disabilitato in SystemConfig (Excel)."},
    "exportModal.report.defaultTitle":     {"SPA": "Informe",                               "ENG": "Report",                              "FRA": "Rapport",                                 "ITA": "Report"},
    "exportModal.report.generated":        {"SPA": "Generado",                              "ENG": "Generated",                           "FRA": "Généré",                                  "ITA": "Generato"},
    "exportModal.report.generatedBy":      {"SPA": "Generado desde Aquafrisch Supervisor",  "ENG": "Generated from Aquafrisch Supervisor","FRA": "Généré depuis Aquafrisch Supervisor",     "ITA": "Generato da Aquafrisch Supervisor"},
    "exportModal.report.noData":           {"SPA": "Sin datos.",                            "ENG": "No data.",                            "FRA": "Aucune donnée.",                          "ITA": "Nessun dato."},
    "exportModal.report.footer":           {"SPA": "Documento generado automáticamente por Aquafrisch Supervisor.",       "ENG": "Document automatically generated by Aquafrisch Supervisor.",       "FRA": "Document généré automatiquement par Aquafrisch Supervisor.",       "ITA": "Documento generato automaticamente da Aquafrisch Supervisor."},
    "exportModal.qr.date":                 {"SPA": "Fecha",                                 "ENG": "Date",                                "FRA": "Date",                                    "ITA": "Data"},
    "exportModal.qr.omitted":              {"SPA": "[+{count} omitidos — ver PDF]",         "ENG": "[+{count} omitted — see PDF]",        "FRA": "[+{count} omis — voir le PDF]",           "ITA": "[+{count} omessi — vedi PDF]"},
    "exportModal.error.print":             {"SPA": "Error generando informe",               "ENG": "Error generating report",             "FRA": "Erreur lors de la génération du rapport", "ITA": "Errore nella generazione del report"},
    "exportModal.error.qr":                {"SPA": "Error generando QR",                    "ENG": "Error generating QR",                 "FRA": "Erreur lors de la génération du QR",      "ITA": "Errore nella generazione del QR"},

    # ═══ ExportDestinationManager (modal "Gestionar destinos") ════════════════
    "exportDest.title":                    {"SPA": "Gestionar destinos de exportación",                        "ENG": "Manage export destinations",                                  "FRA": "Gérer les destinations d'exportation",                            "ITA": "Gestisci destinazioni di esportazione"},
    "exportDest.loading":                  {"SPA": "Cargando…",                                                 "ENG": "Loading…",                                                    "FRA": "Chargement…",                                                     "ITA": "Caricamento…"},
    "exportDest.errorDeleting":            {"SPA": "Error eliminando",                                          "ENG": "Error deleting",                                              "FRA": "Erreur lors de la suppression",                                   "ITA": "Errore durante l'eliminazione"},
    "exportDest.tab.folders":              {"SPA": "Carpetas ({count})",                                        "ENG": "Folders ({count})",                                           "FRA": "Dossiers ({count})",                                              "ITA": "Cartelle ({count})"},
    "exportDest.tab.email":                {"SPA": "Cuentas SMTP ({count})",                                    "ENG": "SMTP accounts ({count})",                                     "FRA": "Comptes SMTP ({count})",                                          "ITA": "Account SMTP ({count})"},
    "exportDest.col.name":                 {"SPA": "Nombre",                                                    "ENG": "Name",                                                        "FRA": "Nom",                                                             "ITA": "Nome"},
    "exportDest.col.path":                 {"SPA": "Ruta",                                                      "ENG": "Path",                                                        "FRA": "Chemin",                                                          "ITA": "Percorso"},
    "exportDest.col.subfolder":            {"SPA": "Subcarpeta",                                                "ENG": "Subfolder",                                                   "FRA": "Sous-dossier",                                                    "ITA": "Sottocartella"},
    "exportDest.col.hostPort":             {"SPA": "Host:Port",                                                 "ENG": "Host:Port",                                                   "FRA": "Hôte:Port",                                                       "ITA": "Host:Port"},
    "exportDest.col.from":                 {"SPA": "Desde",                                                     "ENG": "From",                                                        "FRA": "De",                                                              "ITA": "Da"},
    "exportDest.col.ssl":                  {"SPA": "SSL",                                                       "ENG": "SSL",                                                         "FRA": "SSL",                                                             "ITA": "SSL"},
    "exportDest.col.pass":                 {"SPA": "Pass",                                                      "ENG": "Pass",                                                        "FRA": "Pass",                                                            "ITA": "Pass"},
    "exportDest.col.actions":              {"SPA": "Acciones",                                                  "ENG": "Actions",                                                     "FRA": "Actions",                                                         "ITA": "Azioni"},
    "exportDest.action.edit":              {"SPA": "Editar",                                                    "ENG": "Edit",                                                        "FRA": "Modifier",                                                        "ITA": "Modifica"},
    "exportDest.action.delete":            {"SPA": "Eliminar",                                                  "ENG": "Delete",                                                      "FRA": "Supprimer",                                                       "ITA": "Elimina"},
    "exportDest.action.test":              {"SPA": "Probar",                                                    "ENG": "Test",                                                        "FRA": "Tester",                                                          "ITA": "Prova"},
    "exportDest.action.cancel":            {"SPA": "Cancelar",                                                  "ENG": "Cancel",                                                      "FRA": "Annuler",                                                         "ITA": "Annulla"},
    "exportDest.action.save":              {"SPA": "Guardar",                                                   "ENG": "Save",                                                        "FRA": "Enregistrer",                                                     "ITA": "Salva"},
    "exportDest.folder.new":               {"SPA": "Nueva carpeta",                                             "ENG": "New folder",                                                  "FRA": "Nouveau dossier",                                                 "ITA": "Nuova cartella"},
    "exportDest.folder.empty":             {"SPA": "Sin carpetas. Crea la primera.",                            "ENG": "No folders. Create the first one.",                           "FRA": "Aucun dossier. Créez le premier.",                                "ITA": "Nessuna cartella. Crea la prima."},
    "exportDest.folder.newTitle":          {"SPA": "Nueva carpeta",                                             "ENG": "New folder",                                                  "FRA": "Nouveau dossier",                                                 "ITA": "Nuova cartella"},
    "exportDest.folder.editTitle":         {"SPA": "Editar carpeta",                                            "ENG": "Edit folder",                                                 "FRA": "Modifier le dossier",                                             "ITA": "Modifica cartella"},
    "exportDest.folder.confirmDelete":     {"SPA": "¿Eliminar perfil de carpeta '{name}'?",                     "ENG": "Delete folder profile '{name}'?",                             "FRA": "Supprimer le profil de dossier « {name} » ?",                    "ITA": "Eliminare il profilo cartella '{name}'?"},
    "exportDest.folder.namePh":            {"SPA": "Backups producción",                                        "ENG": "Production backups",                                          "FRA": "Sauvegardes production",                                          "ITA": "Backup produzione"},
    "exportDest.folder.pathPh":            {"SPA": "C:\\Exports  ·  \\\\servidor\\share",                       "ENG": "C:\\Exports  ·  \\\\server\\share",                           "FRA": "C:\\Exports  ·  \\\\serveur\\partage",                            "ITA": "C:\\Exports  ·  \\\\server\\share"},
    "exportDest.folder.subfolderPh":       {"SPA": "auditoría/{fecha}",                                         "ENG": "audit/{fecha}",                                               "FRA": "audit/{fecha}",                                                   "ITA": "audit/{fecha}"},
    "exportDest.folder.browse":            {"SPA": "Examinar…",                                                 "ENG": "Browse…",                                                     "FRA": "Parcourir…",                                                      "ITA": "Sfoglia…"},
    "exportDest.folder.browseTitle":       {"SPA": "Explorar carpetas del servidor",                            "ENG": "Browse server folders",                                       "FRA": "Parcourir les dossiers du serveur",                               "ITA": "Esplora le cartelle del server"},
    "exportDest.email.new":                {"SPA": "Nueva cuenta SMTP",                                         "ENG": "New SMTP account",                                            "FRA": "Nouveau compte SMTP",                                             "ITA": "Nuovo account SMTP"},
    "exportDest.email.empty":              {"SPA": "Sin cuentas. Crea la primera.",                             "ENG": "No accounts. Create the first one.",                          "FRA": "Aucun compte. Créez le premier.",                                 "ITA": "Nessun account. Crea il primo."},
    "exportDest.email.newTitle":           {"SPA": "Nueva cuenta SMTP",                                         "ENG": "New SMTP account",                                            "FRA": "Nouveau compte SMTP",                                             "ITA": "Nuovo account SMTP"},
    "exportDest.email.editTitle":          {"SPA": "Editar cuenta SMTP",                                        "ENG": "Edit SMTP account",                                           "FRA": "Modifier le compte SMTP",                                         "ITA": "Modifica account SMTP"},
    "exportDest.email.confirmDelete":      {"SPA": "¿Eliminar cuenta SMTP '{name}'?",                           "ENG": "Delete SMTP account '{name}'?",                               "FRA": "Supprimer le compte SMTP « {name} » ?",                          "ITA": "Eliminare l'account SMTP '{name}'?"},
    "exportDest.email.testPrompt":         {"SPA": "Enviar prueba desde '{name}'.\nDestinatario:",              "ENG": "Send test from '{name}'.\nRecipient:",                        "FRA": "Envoyer un test depuis « {name} ».\nDestinataire :",            "ITA": "Invia test da '{name}'.\nDestinatario:"},
    "exportDest.email.testOk":             {"SPA": "Email de prueba enviado",                                   "ENG": "Test email sent",                                             "FRA": "E-mail de test envoyé",                                           "ITA": "E-mail di prova inviata"},
    "exportDest.email.testFail":           {"SPA": "Falló",                                                     "ENG": "Failed",                                                      "FRA": "Échec",                                                           "ITA": "Fallito"},
    "exportDest.email.namePh":             {"SPA": "SMTP corporativo",                                          "ENG": "Corporate SMTP",                                              "FRA": "SMTP d'entreprise",                                               "ITA": "SMTP aziendale"},
    "exportDest.email.useSsl":             {"SPA": "Usar SSL",                                                  "ENG": "Use SSL",                                                     "FRA": "Utiliser SSL",                                                    "ITA": "Usa SSL"},
    "exportDest.email.passwordNotice":     {"SPA": "La contraseña se cifra en BD (DPAPI) y nunca se devuelve al cliente.", "ENG": "The password is encrypted in DB (DPAPI) and never returned to the client.", "FRA": "Le mot de passe est chiffré en BD (DPAPI) et n'est jamais renvoyé au client.", "ITA": "La password viene cifrata nel DB (DPAPI) e non viene mai restituita al client."},
    "exportDest.field.name":               {"SPA": "Nombre",                                                    "ENG": "Name",                                                        "FRA": "Nom",                                                             "ITA": "Nome"},
    "exportDest.field.path":               {"SPA": "Ruta (carpeta base)",                                       "ENG": "Path (base folder)",                                          "FRA": "Chemin (dossier de base)",                                        "ITA": "Percorso (cartella base)"},
    "exportDest.field.subfolder":          {"SPA": "Subcarpeta opcional (acepta tokens {fecha} {hora})",        "ENG": "Optional subfolder (accepts tokens {fecha} {hora})",          "FRA": "Sous-dossier optionnel (accepte les jetons {fecha} {hora})",      "ITA": "Sottocartella opzionale (accetta i token {fecha} {hora})"},
    "exportDest.field.description":        {"SPA": "Descripción",                                               "ENG": "Description",                                                 "FRA": "Description",                                                     "ITA": "Descrizione"},
    "exportDest.field.host":               {"SPA": "Host",                                                      "ENG": "Host",                                                        "FRA": "Hôte",                                                            "ITA": "Host"},
    "exportDest.field.port":               {"SPA": "Puerto",                                                    "ENG": "Port",                                                        "FRA": "Port",                                                            "ITA": "Porta"},
    "exportDest.field.ssl":                {"SPA": "SSL/TLS",                                                   "ENG": "SSL/TLS",                                                     "FRA": "SSL/TLS",                                                         "ITA": "SSL/TLS"},
    "exportDest.field.user":               {"SPA": "Usuario (opcional)",                                        "ENG": "Username (optional)",                                         "FRA": "Utilisateur (optionnel)",                                         "ITA": "Utente (opzionale)"},
    "exportDest.field.pass":               {"SPA": "Contraseña",                                                "ENG": "Password",                                                    "FRA": "Mot de passe",                                                    "ITA": "Password"},
    "exportDest.field.passEdit":           {"SPA": "Contraseña (vacío = sin cambios){current}",                 "ENG": "Password (empty = no changes){current}",                      "FRA": "Mot de passe (vide = pas de changement){current}",                "ITA": "Password (vuoto = nessuna modifica){current}"},
    "exportDest.field.fromAddress":        {"SPA": "Email remitente (From)",                                    "ENG": "Sender email (From)",                                         "FRA": "E-mail expéditeur (From)",                                        "ITA": "E-mail mittente (From)"},
    "exportDest.field.fromName":           {"SPA": "Nombre remitente",                                          "ENG": "Sender name",                                                 "FRA": "Nom de l'expéditeur",                                             "ITA": "Nome mittente"},
    "exportDest.field.defaultRecipients":  {"SPA": "Destinatarios por defecto (CSV, opcional)",                 "ENG": "Default recipients (CSV, optional)",                          "FRA": "Destinataires par défaut (CSV, optionnel)",                       "ITA": "Destinatari predefiniti (CSV, opzionale)"},

    # ═══ Backend formatter: cadenas estáticas del informe XLSX/HTML ══════════════════════════════
    # Usadas por Services/Export/ExportFormatterService.cs a través del lookup.
    "export.sheet.dataName":           {"SPA": "Datos",              "ENG": "Data",              "FRA": "Données",          "ITA": "Dati"},
    "export.meta.date":                {"SPA": "Fecha",              "ENG": "Date",              "FRA": "Date",              "ITA": "Data"},
    "export.meta.project":             {"SPA": "Proyecto",           "ENG": "Project",           "FRA": "Projet",            "ITA": "Progetto"},
    "export.section.appliedFilters":   {"SPA": "Filtros aplicados",  "ENG": "Applied filters",   "FRA": "Filtres appliqués", "ITA": "Filtri applicati"},
    "export.section.summary":          {"SPA": "Resumen",            "ENG": "Summary",           "FRA": "Résumé",            "ITA": "Riepilogo"},
    "export.summary.column":           {"SPA": "Columna",            "ENG": "Column",            "FRA": "Colonne",           "ITA": "Colonna"},
    "export.html.reportTitle":         {"SPA": "Informe",            "ENG": "Report",            "FRA": "Rapport",           "ITA": "Report"},
    "export.html.generated":           {"SPA": "Generado",           "ENG": "Generated",         "FRA": "Généré",            "ITA": "Generato"},
    # Etiquetas legibles para claves técnicas de filtros aplicados
    "export.filter.dateRange":         {"SPA": "Rango de fechas",    "ENG": "Date range",        "FRA": "Plage de dates",    "ITA": "Intervallo date"},
    "export.filter.dateFrom":          {"SPA": "Desde",              "ENG": "From",              "FRA": "Du",                "ITA": "Da"},
    "export.filter.dateTo":            {"SPA": "Hasta",              "ENG": "To",                "FRA": "Au",                "ITA": "A"},
    "export.filter.groupId":           {"SPA": "Grupo (id)",         "ENG": "Group (id)",        "FRA": "Groupe (id)",       "ITA": "Gruppo (id)"},
    "export.filter.groupName":         {"SPA": "Grupo",              "ENG": "Group",             "FRA": "Groupe",            "ITA": "Gruppo"},
    "export.filter.uiType":            {"SPA": "Tipo de vista",      "ENG": "View type",         "FRA": "Type de vue",       "ITA": "Tipo di vista"},
    "export.filter.fallback":          {"SPA": "filtros",            "ENG": "filters",           "FRA": "filtres",           "ITA": "filtri"},
    "export.filter.relative.last":     {"SPA": "Últimas",            "ENG": "Last",              "FRA": "Dernières",         "ITA": "Ultime"},
    "export.filter.unit.min":          {"SPA": "min",                "ENG": "min",               "FRA": "min",               "ITA": "min"},
    "export.filter.unit.h":            {"SPA": "h",                  "ENG": "h",                 "FRA": "h",                 "ITA": "h"},
    "export.filter.unit.days":         {"SPA": "días",               "ENG": "days",              "FRA": "jours",             "ITA": "giorni"},
}

# Page-level groupings (which translations.json "pages" entry each key belongs to)
PAGE_GROUPS = {
    "EXPORT_WIZARD":   "exportWizard.",
    "EXPORT_TASKS":    "exportTasks.",
    "EXPORT_MODAL":    "exportModal.",
    "EXPORT_DEST":     "exportDest.",
    "EXPORT_FORMATTER": "export.",
}
PAGE_DESCRIPTIONS = {
    "EXPORT_WIZARD":   "Wizard de exportación (Aquafrisch Export Manager)",
    "EXPORT_TASKS":    "Listado de tareas persistentes del Export Manager",
    "EXPORT_MODAL":    "Modal anfitrión de exportación (Imprimir / QR / Gestor)",
    "EXPORT_DEST":     "Gestor de destinos de exportación (carpetas + SMTP)",
    "EXPORT_FORMATTER":"Cadenas estáticas del informe generado (XLSX/HTML)",
}


def merge_one(target: Path) -> int:
    if not target.exists():
        print(f"SKIP: {target} not found")
        return 0

    with target.open("r", encoding="utf-8") as f:
        data = json.load(f, object_pairs_hook=OrderedDict)

    translations = data.get("translations")
    if translations is None:
        print(f"ERROR: 'translations' key missing in {target}", file=sys.stderr)
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

    pages = data.get("pages")
    if pages is not None:
        # Para evitar solapes (p.ej. "export." captura tambi\u00e9n "exportWizard."),
        # un label se asigna a la p\u00e1gina cuyo prefijo coincidente es MÁS LARGO.
        all_prefixes = list(PAGE_GROUPS.values())

        def best_prefix(key: str) -> str | None:
            matches = [p for p in all_prefixes if key.startswith(p)]
            if not matches:
                return None
            return max(matches, key=len)

        for page_id, prefix in PAGE_GROUPS.items():
            pages[page_id] = OrderedDict([
                ("description", PAGE_DESCRIPTIONS[page_id]),
                ("labels", [k for k in ENTRIES.keys() if best_prefix(k) == prefix]),
            ])

    md = data.get("metadata")
    if md is not None:
        md["lastModified"] = "2026-05-27T00:00:00Z"

    with target.open("w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=4)
        f.write("\n")

    print(f"OK [{target.parent.parent.name}]: {added} added, {updated} updated")
    return 0


def main():
    args = sys.argv[1:]
    if args and args[0] == "--all":
        targets = sorted(PROJECTS_ROOT.glob("*/translations/translations.json"))
        if not targets:
            print("No translations.json files found under Projects/*/translations/")
            return 1
    else:
        project = args[0] if args else DEFAULT_PROJECT
        targets = [PROJECTS_ROOT / project / "translations" / "translations.json"]

    rc = 0
    for t in targets:
        rc |= merge_one(t)
    return rc


if __name__ == "__main__":
    sys.exit(main())
