# ============================================================================
# WSUS-Configure.ps1 - WSUS Management Tool (ES/EN/FR/IT)
# ============================================================================
# Run as Administrator on the production PC
#
# Usage:
#   .\WSUS-Configure.ps1                     -> Interactive menu
#   .\WSUS-Configure.ps1 -Action setup       -> Configure WSUS (first time)
#   .\WSUS-Configure.ps1 -Action enable      -> Enable updates
#   .\WSUS-Configure.ps1 -Action disable     -> Disable updates
#   .\WSUS-Configure.ps1 -Action status      -> Show current status
#   .\WSUS-Configure.ps1 -Action check       -> Search for updates
#   .\WSUS-Configure.ps1 -Action install     -> Install pending updates
#   .\WSUS-Configure.ps1 -Action reset       -> Remove ALL WSUS config
#   .\WSUS-Configure.ps1 -Action modo        -> Change mode: manual/auto
#   .\WSUS-Configure.ps1 -Action setup -Server "http://other:8530"
#   .\WSUS-Configure.ps1 -Lang EN            -> Force language
# ============================================================================

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("setup", "enable", "disable", "status", "check", "install", "reset", "modo")]
    [string]$Action,

    [Parameter(Mandatory=$false)]
    [string]$Server = "http://10.8.82.1:8530",

    [Parameter(Mandatory=$false)]
    [ValidateSet("ES", "EN", "FR", "IT")]
    [string]$Lang
)

# ============================================================================
# STRINGS - All user-facing text in 4 languages
# ============================================================================
$Strings = @{
    "ES" = @{
        Title            = "  Herramienta de configuracion WSUS"
        MenuStatus       = "  1) status   - Ver configuracion actual"
        MenuSetup        = "  2) setup    - Configurar WSUS (primera vez)"
        MenuEnable       = "  3) enable   - Habilitar actualizaciones"
        MenuDisable      = "  4) disable  - Deshabilitar actualizaciones"
        MenuCheck        = "  5) check    - Buscar actualizaciones disponibles"
        MenuInstall      = "  6) install  - Instalar actualizaciones pendientes"
        MenuReset        = "  7) reset    - Eliminar config WSUS (volver al estado original)"
        MenuModo         = "  8) modo     - Cambiar modo: manual o automatico"
        MenuExit         = "  0) Salir"
        SelectOption     = "Selecciona una opcion"
        InvalidOption    = "Opcion no valida"
        # Setup
        SetupTitle       = " Configurar WSUS"
        CurrentServer    = "  Servidor actual"
        NewServer        = "  Nuevo servidor (Enter para mantener)"
        Configuring      = "  Configurando WSUS"
        SetupDone        = "WSUS configurado correctamente"
        SetupServer      = "  Servidor"
        SetupMode        = "  Modo: Descargar y notificar (NO instala solo)"
        SetupReboot      = "  Reinicio automatico: BLOQUEADO (si hay usuario logueado)"
        SetupFreq        = "  Frecuencia busqueda: cada 22 horas"
        SetupDisabled    = "  Estado: DESHABILITADO (usar opcion 3 para activar)"
        # Enable/Disable
        Enabling         = "Habilitando actualizaciones WSUS..."
        Enabled          = "Actualizaciones HABILITADAS"
        EnabledSearch    = "  Windows buscara actualizaciones en WSUS"
        EnabledMode      = "  Modo: Descargar y notificar antes de instalar"
        Disabling        = "Deshabilitando actualizaciones WSUS..."
        Disabled         = "Actualizaciones DESHABILITADAS"
        # Status
        StatusTitle      = " Estado WSUS"
        StServer         = "  --- Servidor ---"
        StWsusServer     = "  Servidor WSUS"
        StStatusServer   = "  Status Server"
        StUpdates        = "  --- Actualizaciones ---"
        StEnabled        = "  Estado:         HABILITADAS"
        StDisabled       = "  Estado:         DESHABILITADAS"
        StMode2          = "  Modo:           Notificar antes de descargar"
        StMode3          = "  Modo:           Descargar y notificar antes de instalar"
        StMode4          = "  Modo:           Instalacion automatica programada"
        StMode5          = "  Modo:           Permitir admin local elegir"
        StProtection     = "  --- Proteccion ---"
        StRebootBlocked  = "  Reinicio auto:  BLOQUEADO (si hay usuario logueado)"
        StRebootAllowed  = "  Reinicio auto:  PERMITIDO (puede reiniciar sin aviso!)"
        StRebootNA       = "  Reinicio auto:  No configurado"
        StFrequency      = "  --- Frecuencia ---"
        StSearchEvery    = "  Busqueda cada"
        StHours          = "horas"
        StSearchDefault  = "  Busqueda:       Frecuencia por defecto (22h)"
        StRecommended    = "  Recomendadas"
        StAutoMinor      = "  Auto menores"
        StService        = "  --- Servicio ---"
        StWinUpdate      = "  Windows Update"
        StNotConfigured  = "  WSUS no configurado"
        StRunSetup       = "  Ejecutar: opcion 2 - setup"
        # Check
        Checking         = "Forzando busqueda de actualizaciones..."
        CheckDisabled    = "ATENCION: Actualizaciones deshabilitadas. Habilitar primero (opcion 3)"
        Searching        = "Buscando actualizaciones en WSUS (puede tardar)..."
        NoPending        = "No hay actualizaciones pendientes"
        Available        = "Actualizaciones disponibles"
        CheckError       = "Error buscando actualizaciones"
        CheckConnectivity= "Verificar conectividad con"
        # Install
        InstallTitle     = " Instalar actualizaciones pendientes"
        SearchPending    = "Buscando actualizaciones pendientes..."
        ToInstall        = "Actualizaciones a instalar"
        ConfirmInstall   = "Instalar? (S/Y/O/N)"
        Cancelled        = "Cancelado"
        Downloading      = "Descargando actualizaciones..."
        Installing       = "Instalando actualizaciones..."
        InstallOK        = "Instalacion completada correctamente"
        InstallErrors    = "Instalacion completada con errores"
        InstallFailed    = "Instalacion fallida"
        InstallAborted   = "Instalacion abortada"
        RebootRequired   = "REINICIO REQUERIDO para completar la instalacion"
        ConfirmReboot    = "Reiniciar ahora? (S/Y/O/N)"
        # Reset
        ResetTitle       = " Eliminar TODA la configuracion WSUS"
        ResetWarn1       = "Esto eliminara TODAS las claves de registro de WSUS"
        ResetWarn2       = "Windows Update volvera a buscar directamente en Microsoft Update"
        ConfirmReset     = "Estas seguro? (S/Y/O/N)"
        ResetDone        = "Claves de registro WSUS eliminadas"
        ResetClean       = "No habia configuracion WSUS (ya estaba limpio)"
        ResetService     = "Servicio Windows Update reiniciado"
        ResetRestored    = "Windows Update restaurado a estado original"
        ResetInternet    = "Actualizaciones se buscaran en Microsoft Update (Internet)"
        # Modo
        ModoTitle        = " Cambiar modo de actualizacion"
        ModoCurrentMan   = "  Modo actual: MANUAL (descarga y avisa, tu decides cuando instalar)"
        ModoCurrentAuto  = "  Modo actual: AUTOMATICO (descarga e instala segun programacion)"
        ModoNotConfig    = "  WSUS no configurado. Ejecutar setup primero."
        ModoManual       = "  1) MANUAL     - Descarga y avisa. Tu decides cuando instalar"
        ModoAuto         = "  2) AUTOMATICO - Descarga e instala automaticamente"
        ModoCancel       = "  0) Cancelar"
        ModoSelect       = "  Selecciona modo"
        ModoChangedMan   = "  Modo cambiado a MANUAL"
        ModoManDesc      = "  Windows descargara actualizaciones y te avisara antes de instalar"
        ModoWarnAuto     = "  ATENCION: En modo automatico, Windows instalara actualizaciones solo."
        ModoWarnReboot   = "  El reinicio sigue BLOQUEADO si hay usuario logueado."
        ModoConfirmAuto  = "  Estas seguro? (S/Y/O/N)"
        ModoChangedAuto  = "  Modo cambiado a AUTOMATICO"
        # General
        PressEnter       = "Pulsa Enter para salir"
        Error            = "Error"
        Yes              = @("S","s","Y","y","O","o")
    }
    "EN" = @{
        Title            = "  WSUS Configuration Tool"
        MenuStatus       = "  1) status   - View current configuration"
        MenuSetup        = "  2) setup    - Configure WSUS (first time)"
        MenuEnable       = "  3) enable   - Enable updates"
        MenuDisable      = "  4) disable  - Disable updates"
        MenuCheck        = "  5) check    - Search for available updates"
        MenuInstall      = "  6) install  - Install pending updates"
        MenuReset        = "  7) reset    - Remove WSUS config (restore original state)"
        MenuModo         = "  8) mode     - Change mode: manual or automatic"
        MenuExit         = "  0) Exit"
        SelectOption     = "Select an option"
        InvalidOption    = "Invalid option"
        SetupTitle       = " Configure WSUS"
        CurrentServer    = "  Current server"
        NewServer        = "  New server (Enter to keep current)"
        Configuring      = "  Configuring WSUS"
        SetupDone        = "WSUS configured successfully"
        SetupServer      = "  Server"
        SetupMode        = "  Mode: Download and notify (does NOT install alone)"
        SetupReboot      = "  Auto-reboot: BLOCKED (if user is logged in)"
        SetupFreq        = "  Search frequency: every 22 hours"
        SetupDisabled    = "  Status: DISABLED (use option 3 to enable)"
        Enabling         = "Enabling WSUS updates..."
        Enabled          = "Updates ENABLED"
        EnabledSearch    = "  Windows will search for updates on WSUS"
        EnabledMode      = "  Mode: Download and notify before installing"
        Disabling        = "Disabling WSUS updates..."
        Disabled         = "Updates DISABLED"
        StatusTitle      = " WSUS Status"
        StServer         = "  --- Server ---"
        StWsusServer     = "  WSUS Server"
        StStatusServer   = "  Status Server"
        StUpdates        = "  --- Updates ---"
        StEnabled        = "  Status:         ENABLED"
        StDisabled       = "  Status:         DISABLED"
        StMode2          = "  Mode:           Notify before downloading"
        StMode3          = "  Mode:           Download and notify before installing"
        StMode4          = "  Mode:           Scheduled automatic installation"
        StMode5          = "  Mode:           Allow local admin to choose"
        StProtection     = "  --- Protection ---"
        StRebootBlocked  = "  Auto-reboot:    BLOCKED (if user is logged in)"
        StRebootAllowed  = "  Auto-reboot:    ALLOWED (may reboot without warning!)"
        StRebootNA       = "  Auto-reboot:    Not configured"
        StFrequency      = "  --- Frequency ---"
        StSearchEvery    = "  Search every"
        StHours          = "hours"
        StSearchDefault  = "  Search:         Default frequency (22h)"
        StRecommended    = "  Recommended"
        StAutoMinor      = "  Auto minor"
        StService        = "  --- Service ---"
        StWinUpdate      = "  Windows Update"
        StNotConfigured  = "  WSUS not configured"
        StRunSetup       = "  Run: option 2 - setup"
        Checking         = "Forcing update search..."
        CheckDisabled    = "WARNING: Updates disabled. Enable first (option 3)"
        Searching        = "Searching for updates on WSUS (may take a while)..."
        NoPending        = "No pending updates"
        Available        = "Available updates"
        CheckError       = "Error searching for updates"
        CheckConnectivity= "Check connectivity to"
        InstallTitle     = " Install pending updates"
        SearchPending    = "Searching for pending updates..."
        ToInstall        = "Updates to install"
        ConfirmInstall   = "Install? (Y/N)"
        Cancelled        = "Cancelled"
        Downloading      = "Downloading updates..."
        Installing       = "Installing updates..."
        InstallOK        = "Installation completed successfully"
        InstallErrors    = "Installation completed with errors"
        InstallFailed    = "Installation failed"
        InstallAborted   = "Installation aborted"
        RebootRequired   = "REBOOT REQUIRED to complete installation"
        ConfirmReboot    = "Reboot now? (Y/N)"
        ResetTitle       = " Remove ALL WSUS configuration"
        ResetWarn1       = "This will remove ALL WSUS registry keys"
        ResetWarn2       = "Windows Update will search directly on Microsoft Update"
        ConfirmReset     = "Are you sure? (Y/N)"
        ResetDone        = "WSUS registry keys removed"
        ResetClean       = "No WSUS configuration found (already clean)"
        ResetService     = "Windows Update service restarted"
        ResetRestored    = "Windows Update restored to original state"
        ResetInternet    = "Updates will be searched on Microsoft Update (Internet)"
        ModoTitle        = " Change update mode"
        ModoCurrentMan   = "  Current mode: MANUAL (downloads and notifies, you decide when to install)"
        ModoCurrentAuto  = "  Current mode: AUTOMATIC (downloads and installs on schedule)"
        ModoNotConfig    = "  WSUS not configured. Run setup first."
        ModoManual       = "  1) MANUAL     - Downloads and notifies. You decide when to install"
        ModoAuto         = "  2) AUTOMATIC  - Downloads and installs automatically"
        ModoCancel       = "  0) Cancel"
        ModoSelect       = "  Select mode"
        ModoChangedMan   = "  Mode changed to MANUAL"
        ModoManDesc      = "  Windows will download updates and notify before installing"
        ModoWarnAuto     = "  WARNING: In automatic mode, Windows will install updates on its own."
        ModoWarnReboot   = "  Reboot is still BLOCKED if a user is logged in."
        ModoConfirmAuto  = "  Are you sure? (Y/N)"
        ModoChangedAuto  = "  Mode changed to AUTOMATIC"
        PressEnter       = "Press Enter to exit"
        Error            = "Error"
        Yes              = @("Y","y","S","s","O","o")
    }
    "FR" = @{
        Title            = "  Outil de configuration WSUS"
        MenuStatus       = "  1) status   - Voir la configuration actuelle"
        MenuSetup        = "  2) setup    - Configurer WSUS (premiere fois)"
        MenuEnable       = "  3) enable   - Activer les mises a jour"
        MenuDisable      = "  4) disable  - Desactiver les mises a jour"
        MenuCheck        = "  5) check    - Rechercher les mises a jour disponibles"
        MenuInstall      = "  6) install  - Installer les mises a jour en attente"
        MenuReset        = "  7) reset    - Supprimer config WSUS (retour a l'etat initial)"
        MenuModo         = "  8) mode     - Changer le mode: manuel ou automatique"
        MenuExit         = "  0) Quitter"
        SelectOption     = "Choisissez une option"
        InvalidOption    = "Option non valide"
        SetupTitle       = " Configurer WSUS"
        CurrentServer    = "  Serveur actuel"
        NewServer        = "  Nouveau serveur (Entree pour garder l'actuel)"
        Configuring      = "  Configuration WSUS"
        SetupDone        = "WSUS configure correctement"
        SetupServer      = "  Serveur"
        SetupMode        = "  Mode: Telecharger et notifier (n'installe PAS seul)"
        SetupReboot      = "  Redemarrage auto: BLOQUE (si utilisateur connecte)"
        SetupFreq        = "  Frequence de recherche: toutes les 22 heures"
        SetupDisabled    = "  Etat: DESACTIVE (utiliser option 3 pour activer)"
        Enabling         = "Activation des mises a jour WSUS..."
        Enabled          = "Mises a jour ACTIVEES"
        EnabledSearch    = "  Windows recherchera les mises a jour sur WSUS"
        EnabledMode      = "  Mode: Telecharger et notifier avant d'installer"
        Disabling        = "Desactivation des mises a jour WSUS..."
        Disabled         = "Mises a jour DESACTIVEES"
        StatusTitle      = " Etat WSUS"
        StServer         = "  --- Serveur ---"
        StWsusServer     = "  Serveur WSUS"
        StStatusServer   = "  Serveur Status"
        StUpdates        = "  --- Mises a jour ---"
        StEnabled        = "  Etat:           ACTIVEES"
        StDisabled       = "  Etat:           DESACTIVEES"
        StMode2          = "  Mode:           Notifier avant de telecharger"
        StMode3          = "  Mode:           Telecharger et notifier avant d'installer"
        StMode4          = "  Mode:           Installation automatique programmee"
        StMode5          = "  Mode:           Laisser l'admin local choisir"
        StProtection     = "  --- Protection ---"
        StRebootBlocked  = "  Redemarrage:    BLOQUE (si utilisateur connecte)"
        StRebootAllowed  = "  Redemarrage:    AUTORISE (peut redemarrer sans avertir!)"
        StRebootNA       = "  Redemarrage:    Non configure"
        StFrequency      = "  --- Frequence ---"
        StSearchEvery    = "  Recherche toutes les"
        StHours          = "heures"
        StSearchDefault  = "  Recherche:      Frequence par defaut (22h)"
        StRecommended    = "  Recommandees"
        StAutoMinor      = "  Auto mineures"
        StService        = "  --- Service ---"
        StWinUpdate      = "  Windows Update"
        StNotConfigured  = "  WSUS non configure"
        StRunSetup       = "  Executer: option 2 - setup"
        Checking         = "Recherche forcee de mises a jour..."
        CheckDisabled    = "ATTENTION: Mises a jour desactivees. Activer d'abord (option 3)"
        Searching        = "Recherche de mises a jour sur WSUS (peut prendre du temps)..."
        NoPending        = "Aucune mise a jour en attente"
        Available        = "Mises a jour disponibles"
        CheckError       = "Erreur lors de la recherche de mises a jour"
        CheckConnectivity= "Verifier la connectivite avec"
        InstallTitle     = " Installer les mises a jour en attente"
        SearchPending    = "Recherche des mises a jour en attente..."
        ToInstall        = "Mises a jour a installer"
        ConfirmInstall   = "Installer? (O/N)"
        Cancelled        = "Annule"
        Downloading      = "Telechargement des mises a jour..."
        Installing       = "Installation des mises a jour..."
        InstallOK        = "Installation terminee avec succes"
        InstallErrors    = "Installation terminee avec des erreurs"
        InstallFailed    = "Installation echouee"
        InstallAborted   = "Installation annulee"
        RebootRequired   = "REDEMARRAGE REQUIS pour terminer l'installation"
        ConfirmReboot    = "Redemarrer maintenant? (O/N)"
        ResetTitle       = " Supprimer TOUTE la configuration WSUS"
        ResetWarn1       = "Cela supprimera TOUTES les cles de registre WSUS"
        ResetWarn2       = "Windows Update recherchera directement sur Microsoft Update"
        ConfirmReset     = "Etes-vous sur? (O/N)"
        ResetDone        = "Cles de registre WSUS supprimees"
        ResetClean       = "Aucune configuration WSUS trouvee (deja propre)"
        ResetService     = "Service Windows Update redemarre"
        ResetRestored    = "Windows Update restaure a l'etat initial"
        ResetInternet    = "Les mises a jour seront recherchees sur Microsoft Update (Internet)"
        ModoTitle        = " Changer le mode de mise a jour"
        ModoCurrentMan   = "  Mode actuel: MANUEL (telecharge et notifie, vous decidez quand installer)"
        ModoCurrentAuto  = "  Mode actuel: AUTOMATIQUE (telecharge et installe selon le planning)"
        ModoNotConfig    = "  WSUS non configure. Executer setup d'abord."
        ModoManual       = "  1) MANUEL      - Telecharge et notifie. Vous decidez quand installer"
        ModoAuto         = "  2) AUTOMATIQUE - Telecharge et installe automatiquement"
        ModoCancel       = "  0) Annuler"
        ModoSelect       = "  Choisissez le mode"
        ModoChangedMan   = "  Mode change a MANUEL"
        ModoManDesc      = "  Windows telechargera les mises a jour et notifiera avant d'installer"
        ModoWarnAuto     = "  ATTENTION: En mode automatique, Windows installera les mises a jour seul."
        ModoWarnReboot   = "  Le redemarrage reste BLOQUE si un utilisateur est connecte."
        ModoConfirmAuto  = "  Etes-vous sur? (O/N)"
        ModoChangedAuto  = "  Mode change a AUTOMATIQUE"
        PressEnter       = "Appuyez sur Entree pour quitter"
        Error            = "Erreur"
        Yes              = @("O","o","S","s","Y","y")
    }
    "IT" = @{
        Title            = "  Strumento di configurazione WSUS"
        MenuStatus       = "  1) status   - Visualizza configurazione attuale"
        MenuSetup        = "  2) setup    - Configura WSUS (prima volta)"
        MenuEnable       = "  3) enable   - Abilitare aggiornamenti"
        MenuDisable      = "  4) disable  - Disabilitare aggiornamenti"
        MenuCheck        = "  5) check    - Cercare aggiornamenti disponibili"
        MenuInstall      = "  6) install  - Installare aggiornamenti in sospeso"
        MenuReset        = "  7) reset    - Eliminare config WSUS (tornare allo stato originale)"
        MenuModo         = "  8) modo     - Cambiare modalita: manuale o automatica"
        MenuExit         = "  0) Uscire"
        SelectOption     = "Seleziona un'opzione"
        InvalidOption    = "Opzione non valida"
        SetupTitle       = " Configurare WSUS"
        CurrentServer    = "  Server attuale"
        NewServer        = "  Nuovo server (Invio per mantenere)"
        Configuring      = "  Configurazione WSUS"
        SetupDone        = "WSUS configurato correttamente"
        SetupServer      = "  Server"
        SetupMode        = "  Modalita: Scarica e notifica (NON installa da solo)"
        SetupReboot      = "  Riavvio automatico: BLOCCATO (se utente connesso)"
        SetupFreq        = "  Frequenza ricerca: ogni 22 ore"
        SetupDisabled    = "  Stato: DISABILITATO (usare opzione 3 per attivare)"
        Enabling         = "Abilitazione aggiornamenti WSUS..."
        Enabled          = "Aggiornamenti ABILITATI"
        EnabledSearch    = "  Windows cerchera aggiornamenti su WSUS"
        EnabledMode      = "  Modalita: Scarica e notifica prima di installare"
        Disabling        = "Disabilitazione aggiornamenti WSUS..."
        Disabled         = "Aggiornamenti DISABILITATI"
        StatusTitle      = " Stato WSUS"
        StServer         = "  --- Server ---"
        StWsusServer     = "  Server WSUS"
        StStatusServer   = "  Status Server"
        StUpdates        = "  --- Aggiornamenti ---"
        StEnabled        = "  Stato:          ABILITATI"
        StDisabled       = "  Stato:          DISABILITATI"
        StMode2          = "  Modalita:       Notifica prima di scaricare"
        StMode3          = "  Modalita:       Scarica e notifica prima di installare"
        StMode4          = "  Modalita:       Installazione automatica programmata"
        StMode5          = "  Modalita:       Consenti all'admin locale di scegliere"
        StProtection     = "  --- Protezione ---"
        StRebootBlocked  = "  Riavvio auto:   BLOCCATO (se utente connesso)"
        StRebootAllowed  = "  Riavvio auto:   PERMESSO (puo riavviare senza avviso!)"
        StRebootNA       = "  Riavvio auto:   Non configurato"
        StFrequency      = "  --- Frequenza ---"
        StSearchEvery    = "  Ricerca ogni"
        StHours          = "ore"
        StSearchDefault  = "  Ricerca:        Frequenza predefinita (22h)"
        StRecommended    = "  Raccomandate"
        StAutoMinor      = "  Auto minori"
        StService        = "  --- Servizio ---"
        StWinUpdate      = "  Windows Update"
        StNotConfigured  = "  WSUS non configurato"
        StRunSetup       = "  Eseguire: opzione 2 - setup"
        Checking         = "Ricerca forzata aggiornamenti..."
        CheckDisabled    = "ATTENZIONE: Aggiornamenti disabilitati. Abilitare prima (opzione 3)"
        Searching        = "Ricerca aggiornamenti su WSUS (potrebbe richiedere tempo)..."
        NoPending        = "Nessun aggiornamento in sospeso"
        Available        = "Aggiornamenti disponibili"
        CheckError       = "Errore nella ricerca aggiornamenti"
        CheckConnectivity= "Verificare connettivita con"
        InstallTitle     = " Installare aggiornamenti in sospeso"
        SearchPending    = "Ricerca aggiornamenti in sospeso..."
        ToInstall        = "Aggiornamenti da installare"
        ConfirmInstall   = "Installare? (S/N)"
        Cancelled        = "Annullato"
        Downloading      = "Download aggiornamenti..."
        Installing       = "Installazione aggiornamenti..."
        InstallOK        = "Installazione completata con successo"
        InstallErrors    = "Installazione completata con errori"
        InstallFailed    = "Installazione fallita"
        InstallAborted   = "Installazione annullata"
        RebootRequired   = "RIAVVIO NECESSARIO per completare l'installazione"
        ConfirmReboot    = "Riavviare adesso? (S/N)"
        ResetTitle       = " Eliminare TUTTA la configurazione WSUS"
        ResetWarn1       = "Questo eliminera TUTTE le chiavi di registro WSUS"
        ResetWarn2       = "Windows Update cerchera direttamente su Microsoft Update"
        ConfirmReset     = "Sei sicuro? (S/N)"
        ResetDone        = "Chiavi di registro WSUS eliminate"
        ResetClean       = "Nessuna configurazione WSUS trovata (gia pulito)"
        ResetService     = "Servizio Windows Update riavviato"
        ResetRestored    = "Windows Update ripristinato allo stato originale"
        ResetInternet    = "Gli aggiornamenti saranno cercati su Microsoft Update (Internet)"
        ModoTitle        = " Cambiare modalita di aggiornamento"
        ModoCurrentMan   = "  Modalita attuale: MANUALE (scarica e avvisa, decidi tu quando installare)"
        ModoCurrentAuto  = "  Modalita attuale: AUTOMATICA (scarica e installa secondo programmazione)"
        ModoNotConfig    = "  WSUS non configurato. Eseguire setup prima."
        ModoManual       = "  1) MANUALE     - Scarica e avvisa. Decidi tu quando installare"
        ModoAuto         = "  2) AUTOMATICA  - Scarica e installa automaticamente"
        ModoCancel       = "  0) Annullare"
        ModoSelect       = "  Seleziona modalita"
        ModoChangedMan   = "  Modalita cambiata a MANUALE"
        ModoManDesc      = "  Windows scarichera aggiornamenti e avvisera prima di installare"
        ModoWarnAuto     = "  ATTENZIONE: In modalita automatica, Windows installera aggiornamenti da solo."
        ModoWarnReboot   = "  Il riavvio resta BLOCCATO se c'e un utente connesso."
        ModoConfirmAuto  = "  Sei sicuro? (S/N)"
        ModoChangedAuto  = "  Modalita cambiata ad AUTOMATICA"
        PressEnter       = "Premi Invio per uscire"
        Error            = "Errore"
        Yes              = @("S","s","Y","y","O","o")
    }
}

# ============================================================================
# Language selection
# ============================================================================
if (-not $Lang) {
    Write-Host ""
    Write-Host "  Select language / Selecciona idioma:" -ForegroundColor Cyan
    Write-Host "  1) ES - Espanol"
    Write-Host "  2) EN - English"
    Write-Host "  3) FR - Francais"
    Write-Host "  4) IT - Italiano"
    Write-Host ""
    $langChoice = Read-Host "  (1/2/3/4)"
    switch ($langChoice) {
        "1" { $Lang = "ES" }
        "2" { $Lang = "EN" }
        "3" { $Lang = "FR" }
        "4" { $Lang = "IT" }
        default { $Lang = "ES" }
    }
}

$L = $Strings[$Lang]

# ============================================================================
# Interactive menu
# ============================================================================
if (-not $Action) {
    Write-Host ""
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host $L.Title -ForegroundColor Cyan
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host $L.MenuStatus -ForegroundColor White
    Write-Host $L.MenuSetup -ForegroundColor White
    Write-Host $L.MenuEnable -ForegroundColor Green
    Write-Host $L.MenuDisable -ForegroundColor Yellow
    Write-Host $L.MenuCheck -ForegroundColor White
    Write-Host $L.MenuInstall -ForegroundColor White
    Write-Host $L.MenuReset -ForegroundColor Red
    Write-Host $L.MenuModo -ForegroundColor White
    Write-Host $L.MenuExit -ForegroundColor Gray
    Write-Host ""
    $choice = Read-Host $L.SelectOption
    switch ($choice) {
        "1" { $Action = "status" }
        "2" { $Action = "setup" }
        "3" { $Action = "enable" }
        "4" { $Action = "disable" }
        "5" { $Action = "check" }
        "6" { $Action = "install" }
        "7" { $Action = "reset" }
        "8" { $Action = "modo" }
        "0" { exit 0 }
        default { Write-Host $L.InvalidOption -ForegroundColor Red; exit 1 }
    }
}

# ============================================================================
# Auto-elevation: relaunch as Admin if needed
# ============================================================================
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $scriptPath = $MyInvocation.MyCommand.Path
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`" -Action $Action -Server `"$Server`" -Lang $Lang"
    exit 0
}

$WsusServer = $Server
$RegPathWU = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"
$RegPathAU = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU"

# Helper: check yes/no answer
function IsYes($answer) { return $L.Yes -contains $answer }

# ============================================================================
# Actions
# ============================================================================
switch ($Action) {

    "setup" {
        Write-Host "===============================================" -ForegroundColor Cyan
        Write-Host $L.SetupTitle -ForegroundColor Cyan
        Write-Host "===============================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "  $($L.CurrentServer): $WsusServer" -ForegroundColor White
        $nuevoServer = Read-Host "  $($L.NewServer)"
        if ($nuevoServer) { $WsusServer = $nuevoServer }
        Write-Host ""
        Write-Host "  $($L.Configuring): $WsusServer" -ForegroundColor Cyan

        New-Item -Path $RegPathWU -Force | Out-Null
        New-Item -Path $RegPathAU -Force | Out-Null

        Set-ItemProperty -Path $RegPathWU -Name "WUServer" -Value $WsusServer
        Set-ItemProperty -Path $RegPathWU -Name "WUStatusServer" -Value $WsusServer
        Set-ItemProperty -Path $RegPathAU -Name "UseWUServer" -Value 1
        Set-ItemProperty -Path $RegPathAU -Name "NoAutoUpdate" -Value 1
        Set-ItemProperty -Path $RegPathAU -Name "AUOptions" -Value 3
        Set-ItemProperty -Path $RegPathAU -Name "NoAutoRebootWithLoggedOnUsers" -Value 1
        Set-ItemProperty -Path $RegPathAU -Name "DetectionFrequencyEnabled" -Value 1
        Set-ItemProperty -Path $RegPathAU -Name "DetectionFrequency" -Value 22
        Set-ItemProperty -Path $RegPathAU -Name "IncludeRecommendedUpdates" -Value 1
        Set-ItemProperty -Path $RegPathAU -Name "AutoInstallMinorUpdates" -Value 0
        Set-ItemProperty -Path $RegPathAU -Name "RescheduleWaitTimeEnabled" -Value 1
        Set-ItemProperty -Path $RegPathAU -Name "RescheduleWaitTime" -Value 15

        Restart-Service wuauserv -Force

        Write-Host ""
        Write-Host $L.SetupDone -ForegroundColor Green
        Write-Host "  $($L.SetupServer): $WsusServer" -ForegroundColor White
        Write-Host $L.SetupMode -ForegroundColor White
        Write-Host $L.SetupReboot -ForegroundColor White
        Write-Host $L.SetupFreq -ForegroundColor White
        Write-Host $L.SetupDisabled -ForegroundColor Yellow
    }

    "enable" {
        Write-Host $L.Enabling -ForegroundColor Green
        Set-ItemProperty -Path $RegPathAU -Name "NoAutoUpdate" -Value 0
        Restart-Service wuauserv -Force
        Write-Host $L.Enabled -ForegroundColor Green
        Write-Host $L.EnabledSearch -ForegroundColor White
        Write-Host $L.EnabledMode -ForegroundColor White
    }

    "disable" {
        Write-Host $L.Disabling -ForegroundColor Yellow
        Set-ItemProperty -Path $RegPathAU -Name "NoAutoUpdate" -Value 1
        Restart-Service wuauserv -Force
        Write-Host $L.Disabled -ForegroundColor Yellow
    }

    "status" {
        Write-Host "===============================================" -ForegroundColor Cyan
        Write-Host $L.StatusTitle -ForegroundColor Cyan
        Write-Host "===============================================" -ForegroundColor Cyan

        try {
            $wu = Get-ItemProperty $RegPathWU -ErrorAction Stop
            $au = Get-ItemProperty $RegPathAU -ErrorAction Stop

            Write-Host ""
            Write-Host $L.StServer -ForegroundColor DarkCyan
            Write-Host "  $($L.StWsusServer):  $($wu.WUServer)" -ForegroundColor White
            Write-Host "  $($L.StStatusServer):  $($wu.WUStatusServer)" -ForegroundColor White

            Write-Host ""
            Write-Host $L.StUpdates -ForegroundColor DarkCyan
            if ($au.NoAutoUpdate -eq 0) {
                Write-Host $L.StEnabled -ForegroundColor Green
            } else {
                Write-Host $L.StDisabled -ForegroundColor Yellow
            }

            switch ($au.AUOptions) {
                2 { Write-Host $L.StMode2 -ForegroundColor White }
                3 { Write-Host $L.StMode3 -ForegroundColor White }
                4 { Write-Host $L.StMode4 -ForegroundColor White }
                5 { Write-Host $L.StMode5 -ForegroundColor White }
            }

            Write-Host ""
            Write-Host $L.StProtection -ForegroundColor DarkCyan
            $noReboot = try { $au.NoAutoRebootWithLoggedOnUsers } catch { "N/A" }
            if ($noReboot -eq 1) {
                Write-Host $L.StRebootBlocked -ForegroundColor Green
            } elseif ($noReboot -eq 0) {
                Write-Host $L.StRebootAllowed -ForegroundColor Red
            } else {
                Write-Host $L.StRebootNA -ForegroundColor Gray
            }

            Write-Host ""
            Write-Host $L.StFrequency -ForegroundColor DarkCyan
            $detEnabled = try { $au.DetectionFrequencyEnabled } catch { 0 }
            $detFreq = try { $au.DetectionFrequency } catch { "Default" }
            if ($detEnabled -eq 1) {
                Write-Host "  $($L.StSearchEvery):  $detFreq $($L.StHours)" -ForegroundColor White
            } else {
                Write-Host $L.StSearchDefault -ForegroundColor White
            }

            $inclRec = try { $au.IncludeRecommendedUpdates } catch { "N/A" }
            Write-Host "  $($L.StRecommended):   $(if ($inclRec -eq 1) {'SI/YES/OUI'} else {'NO'})" -ForegroundColor White

            $autoMinor = try { $au.AutoInstallMinorUpdates } catch { "N/A" }
            Write-Host "  $($L.StAutoMinor):   $(if ($autoMinor -eq 1) {'SI/YES/OUI'} else {'NO'})" -ForegroundColor White

            Write-Host ""
            Write-Host $L.StService -ForegroundColor DarkCyan
            $svc = Get-Service wuauserv
            Write-Host "  $($L.StWinUpdate): $($svc.Status)" -ForegroundColor White

        } catch {
            Write-Host $L.StNotConfigured -ForegroundColor Red
            Write-Host $L.StRunSetup -ForegroundColor White
        }
        Write-Host ""
    }

    "check" {
        Write-Host $L.Checking -ForegroundColor Cyan

        try {
            $au = Get-ItemProperty $RegPathAU -ErrorAction Stop
            if ($au.NoAutoUpdate -eq 1) {
                Write-Host $L.CheckDisabled -ForegroundColor Yellow
                break
            }
        } catch {}

        $updateSession = New-Object -ComObject Microsoft.Update.Session
        $updateSearcher = $updateSession.CreateUpdateSearcher()
        Write-Host $L.Searching -ForegroundColor White
        try {
            $searchResult = $updateSearcher.Search("IsInstalled=0")
            Write-Host ""
            if ($searchResult.Updates.Count -eq 0) {
                Write-Host $L.NoPending -ForegroundColor Green
            } else {
                Write-Host "$($L.Available): $($searchResult.Updates.Count)" -ForegroundColor Yellow
                foreach ($update in $searchResult.Updates) {
                    Write-Host "  - $($update.Title)" -ForegroundColor White
                }
            }
        } catch {
            Write-Host "$($L.CheckError): $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "$($L.CheckConnectivity) $WsusServer" -ForegroundColor Yellow
        }
    }

    "install" {
        Write-Host "===============================================" -ForegroundColor Cyan
        Write-Host $L.InstallTitle -ForegroundColor Cyan
        Write-Host "===============================================" -ForegroundColor Cyan

        $updateSession = New-Object -ComObject Microsoft.Update.Session
        $updateSearcher = $updateSession.CreateUpdateSearcher()

        Write-Host $L.SearchPending -ForegroundColor White
        try {
            $searchResult = $updateSearcher.Search("IsInstalled=0")

            if ($searchResult.Updates.Count -eq 0) {
                Write-Host $L.NoPending -ForegroundColor Green
                break
            }

            Write-Host "$($L.ToInstall): $($searchResult.Updates.Count)" -ForegroundColor Yellow
            foreach ($update in $searchResult.Updates) {
                Write-Host "  - $($update.Title)" -ForegroundColor White
            }

            Write-Host ""
            $confirm = Read-Host $L.ConfirmInstall
            if (-not (IsYes $confirm)) {
                Write-Host $L.Cancelled -ForegroundColor Yellow
                break
            }

            Write-Host $L.Downloading -ForegroundColor Cyan
            $updatesToDownload = New-Object -ComObject Microsoft.Update.UpdateColl
            foreach ($update in $searchResult.Updates) {
                if (-not $update.IsDownloaded) {
                    $updatesToDownload.Add($update) | Out-Null
                }
            }
            if ($updatesToDownload.Count -gt 0) {
                $downloader = $updateSession.CreateUpdateDownloader()
                $downloader.Updates = $updatesToDownload
                $downloader.Download() | Out-Null
            }

            Write-Host $L.Installing -ForegroundColor Cyan
            $updatesToInstall = New-Object -ComObject Microsoft.Update.UpdateColl
            foreach ($update in $searchResult.Updates) {
                if ($update.IsDownloaded) {
                    $updatesToInstall.Add($update) | Out-Null
                }
            }
            $installer = $updateSession.CreateUpdateInstaller()
            $installer.Updates = $updatesToInstall
            $result = $installer.Install()

            Write-Host ""
            switch ($result.ResultCode) {
                2 { Write-Host $L.InstallOK -ForegroundColor Green }
                3 { Write-Host $L.InstallErrors -ForegroundColor Yellow }
                4 { Write-Host $L.InstallFailed -ForegroundColor Red }
                5 { Write-Host $L.InstallAborted -ForegroundColor Red }
            }

            if ($result.RebootRequired) {
                Write-Host ""
                Write-Host $L.RebootRequired -ForegroundColor Yellow
                $reboot = Read-Host $L.ConfirmReboot
                if (IsYes $reboot) {
                    Restart-Computer -Force
                }
            }

        } catch {
            Write-Host "$($L.Error): $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    "reset" {
        Write-Host "===============================================" -ForegroundColor Red
        Write-Host $L.ResetTitle -ForegroundColor Red
        Write-Host "===============================================" -ForegroundColor Red
        Write-Host ""
        Write-Host $L.ResetWarn1 -ForegroundColor Yellow
        Write-Host $L.ResetWarn2 -ForegroundColor Yellow
        Write-Host ""

        $confirm = Read-Host $L.ConfirmReset
        if (-not (IsYes $confirm)) {
            Write-Host $L.Cancelled -ForegroundColor Yellow
            break
        }

        if (Test-Path $RegPathWU) {
            Remove-Item $RegPathWU -Recurse -Force
            Write-Host $L.ResetDone -ForegroundColor Green
        } else {
            Write-Host $L.ResetClean -ForegroundColor Gray
        }

        Restart-Service wuauserv -Force
        Write-Host $L.ResetService -ForegroundColor Green
        Write-Host ""
        Write-Host $L.ResetRestored -ForegroundColor Green
        Write-Host $L.ResetInternet -ForegroundColor White
    }

    "modo" {
        Write-Host "===============================================" -ForegroundColor Cyan
        Write-Host $L.ModoTitle -ForegroundColor Cyan
        Write-Host "===============================================" -ForegroundColor Cyan

        try {
            $au = Get-ItemProperty $RegPathAU -ErrorAction Stop
            $currentMode = $au.AUOptions
            Write-Host ""
            switch ($currentMode) {
                3 { Write-Host $L.ModoCurrentMan -ForegroundColor Yellow }
                4 { Write-Host $L.ModoCurrentAuto -ForegroundColor Green }
                default { Write-Host "  Mode: $currentMode" -ForegroundColor White }
            }
        } catch {
            Write-Host $L.ModoNotConfig -ForegroundColor Red
            break
        }

        Write-Host ""
        Write-Host $L.ModoManual -ForegroundColor Yellow
        Write-Host $L.ModoAuto -ForegroundColor Green
        Write-Host $L.ModoCancel -ForegroundColor Gray
        Write-Host ""
        $modoChoice = Read-Host $L.ModoSelect
        switch ($modoChoice) {
            "1" {
                Set-ItemProperty -Path $RegPathAU -Name "AUOptions" -Value 3
                Restart-Service wuauserv -Force
                Write-Host $L.ModoChangedMan -ForegroundColor Yellow
                Write-Host $L.ModoManDesc -ForegroundColor White
            }
            "2" {
                Write-Host ""
                Write-Host $L.ModoWarnAuto -ForegroundColor Red
                Write-Host $L.ModoWarnReboot -ForegroundColor Yellow
                $confirmAuto = Read-Host $L.ModoConfirmAuto
                if (IsYes $confirmAuto) {
                    Set-ItemProperty -Path $RegPathAU -Name "AUOptions" -Value 4
                    Restart-Service wuauserv -Force
                    Write-Host $L.ModoChangedAuto -ForegroundColor Green
                } else {
                    Write-Host $L.Cancelled -ForegroundColor Yellow
                }
            }
            "0" { Write-Host $L.Cancelled -ForegroundColor Gray }
            default { Write-Host $L.InvalidOption -ForegroundColor Red }
        }
    }
}

# Pause so the elevated window stays open
Write-Host ""
Read-Host $L.PressEnter
# ============================================================================
# WSUS-Configure.ps1 - Configurar/Habilitar/Deshabilitar WSUS
# ============================================================================
# Ejecutar como Administrador en el PC de produccion
#
# Uso:
#   .\WSUS-Configure.ps1                     -> Menu interactivo
#   .\WSUS-Configure.ps1 -Action setup       -> Configura WSUS (primera vez)
#   .\WSUS-Configure.ps1 -Action enable      -> Habilita actualizaciones
#   .\WSUS-Configure.ps1 -Action disable     -> Deshabilita actualizaciones
#   .\WSUS-Configure.ps1 -Action status      -> Muestra estado actual
#   .\WSUS-Configure.ps1 -Action check       -> Fuerza busqueda de actualizaciones
#   .\WSUS-Configure.ps1 -Action install     -> Instala actualizaciones pendientes
#   .\WSUS-Configure.ps1 -Action reset       -> Elimina TODA la config WSUS (vuelve a estado original)
#   .\WSUS-Configure.ps1 -Action setup -Server "http://otro:8530"
# ============================================================================

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("setup", "enable", "disable", "status", "check", "install", "reset", "modo")]
    [string]$Action,

    [Parameter(Mandatory=$false)]
    [string]$Server = "http://10.8.82.1:8530"
)

# Si no se pasa -Action, mostrar menu interactivo
if (-not $Action) {
    Write-Host ""
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host "  WSUS Configuration Tool" -ForegroundColor Cyan
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  1) status   - Ver configuracion actual" -ForegroundColor White
    Write-Host "  2) setup    - Configurar WSUS (primera vez)" -ForegroundColor White
    Write-Host "  3) enable   - Habilitar actualizaciones" -ForegroundColor Green
    Write-Host "  4) disable  - Deshabilitar actualizaciones" -ForegroundColor Yellow
    Write-Host "  5) check    - Buscar actualizaciones disponibles" -ForegroundColor White
    Write-Host "  6) install  - Instalar actualizaciones pendientes" -ForegroundColor White
    Write-Host "  7) reset    - Eliminar config WSUS (volver a estado original)" -ForegroundColor Red
    Write-Host "  8) modo     - Cambiar modo: manual o automatico" -ForegroundColor White
    Write-Host "  0) Salir" -ForegroundColor Gray
    Write-Host ""
    $choice = Read-Host "Selecciona una opcion"
    switch ($choice) {
        "1" { $Action = "status" }
        "2" { $Action = "setup" }
        "3" { $Action = "enable" }
        "4" { $Action = "disable" }
        "5" { $Action = "check" }
        "6" { $Action = "install" }
        "7" { $Action = "reset" }
        "8" { $Action = "modo" }
        "0" { exit 0 }
        default { Write-Host "Opcion no valida" -ForegroundColor Red; exit 1 }
    }
}

# Auto-elevacion: relanza como Admin si no lo es
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $scriptPath = $MyInvocation.MyCommand.Path
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`" -Action $Action -Server `"$Server`""
    exit 0
}

$WsusServer = $Server
$RegPathWU = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"
$RegPathAU = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU"

switch ($Action) {

    "setup" {
        Write-Host "===============================================" -ForegroundColor Cyan
        Write-Host " Configurar WSUS" -ForegroundColor Cyan
        Write-Host "===============================================" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "  Servidor actual: $WsusServer" -ForegroundColor White
        $nuevoServer = Read-Host "  Nuevo servidor (Enter para mantener)"
        if ($nuevoServer) { $WsusServer = $nuevoServer }
        Write-Host ""
        Write-Host "  Configurando WSUS: $WsusServer" -ForegroundColor Cyan

        New-Item -Path $RegPathWU -Force | Out-Null
        New-Item -Path $RegPathAU -Force | Out-Null

        # --- Servidor WSUS ---
        # URL del servidor WSUS donde buscar actualizaciones
        Set-ItemProperty -Path $RegPathWU -Name "WUServer" -Value $WsusServer

        # URL del servidor donde reportar el estado de las actualizaciones
        # (normalmente el mismo que WUServer)
        Set-ItemProperty -Path $RegPathWU -Name "WUStatusServer" -Value $WsusServer

        # --- Activacion ---
        # 1 = Usar servidor WSUS (no buscar en Microsoft Update/Internet)
        Set-ItemProperty -Path $RegPathAU -Name "UseWUServer" -Value 1

        # 1 = Deshabilitado (no buscar actualizaciones automaticamente)
        # 0 = Habilitado (buscar automaticamente)
        Set-ItemProperty -Path $RegPathAU -Name "NoAutoUpdate" -Value 1

        # --- Modo de actualizacion (AUOptions) ---
        # 2 = Notificar antes de descargar
        # 3 = Descargar automaticamente y notificar antes de instalar (RECOMENDADO)
        # 4 = Descargar e instalar automaticamente segun programacion
        # 5 = Permitir al admin local elegir configuracion
        Set-ItemProperty -Path $RegPathAU -Name "AUOptions" -Value 3

        # --- Proteccion contra reinicios (CRITICO en entorno industrial) ---
        # 1 = NUNCA reiniciar automaticamente si hay un usuario con sesion iniciada
        # Evita reinicios inesperados durante operacion de la maquina
        Set-ItemProperty -Path $RegPathAU -Name "NoAutoRebootWithLoggedOnUsers" -Value 1

        # --- Frecuencia de deteccion ---
        # 1 = Habilitar frecuencia personalizada de busqueda
        Set-ItemProperty -Path $RegPathAU -Name "DetectionFrequencyEnabled" -Value 1

        # Intervalo en horas entre busquedas de actualizaciones (1-22)
        # 22 = buscar 1 vez al dia aprox (minima frecuencia posible)
        Set-ItemProperty -Path $RegPathAU -Name "DetectionFrequency" -Value 22

        # --- Actualizaciones recomendadas ---
        # 1 = Incluir actualizaciones recomendadas (no solo criticas/seguridad)
        Set-ItemProperty -Path $RegPathAU -Name "IncludeRecommendedUpdates" -Value 1

        # --- Actualizaciones menores ---
        # 0 = NO instalar actualizaciones menores automaticamente
        Set-ItemProperty -Path $RegPathAU -Name "AutoInstallMinorUpdates" -Value 0

        # --- Reintento tras reinicio ---
        # 1 = Habilitar reintento de instalaciones perdidas tras reinicio
        Set-ItemProperty -Path $RegPathAU -Name "RescheduleWaitTimeEnabled" -Value 1

        # Minutos a esperar tras arranque antes de reintentar (1-60)
        Set-ItemProperty -Path $RegPathAU -Name "RescheduleWaitTime" -Value 15

        Restart-Service wuauserv -Force

        Write-Host ""
        Write-Host "WSUS configurado correctamente" -ForegroundColor Green
        Write-Host "  Servidor: $WsusServer" -ForegroundColor White
        Write-Host "  Modo: Descargar y notificar (NO instala solo)" -ForegroundColor White
        Write-Host "  Reinicio automatico: BLOQUEADO (si hay usuario logueado)" -ForegroundColor White
        Write-Host "  Frecuencia busqueda: cada 22 horas" -ForegroundColor White
        Write-Host "  Estado: DESHABILITADO (usar opcion 3 para activar)" -ForegroundColor Yellow
    }

    "enable" {
        Write-Host "Habilitando actualizaciones WSUS..." -ForegroundColor Green
        Set-ItemProperty -Path $RegPathAU -Name "NoAutoUpdate" -Value 0
        Restart-Service wuauserv -Force
        Write-Host "Actualizaciones HABILITADAS" -ForegroundColor Green
        Write-Host "  Windows buscara actualizaciones en WSUS" -ForegroundColor White
        Write-Host "  Modo: Descargar y notificar antes de instalar" -ForegroundColor White
    }

    "disable" {
        Write-Host "Deshabilitando actualizaciones WSUS..." -ForegroundColor Yellow
        Set-ItemProperty -Path $RegPathAU -Name "NoAutoUpdate" -Value 1
        Restart-Service wuauserv -Force
        Write-Host "Actualizaciones DESHABILITADAS" -ForegroundColor Yellow
    }

    "status" {
        Write-Host "===============================================" -ForegroundColor Cyan
        Write-Host " Estado WSUS" -ForegroundColor Cyan
        Write-Host "===============================================" -ForegroundColor Cyan

        try {
            $wu = Get-ItemProperty $RegPathWU -ErrorAction Stop
            $au = Get-ItemProperty $RegPathAU -ErrorAction Stop

            Write-Host ""
            Write-Host "  --- Servidor ---" -ForegroundColor DarkCyan
            Write-Host "  Servidor WSUS:  $($wu.WUServer)" -ForegroundColor White
            Write-Host "  Status Server:  $($wu.WUStatusServer)" -ForegroundColor White

            Write-Host ""
            Write-Host "  --- Actualizaciones ---" -ForegroundColor DarkCyan
            if ($au.NoAutoUpdate -eq 0) {
                Write-Host "  Estado:         HABILITADAS" -ForegroundColor Green
            } else {
                Write-Host "  Estado:         DESHABILITADAS" -ForegroundColor Yellow
            }

            switch ($au.AUOptions) {
                2 { Write-Host "  Modo:           Notificar antes de descargar" -ForegroundColor White }
                3 { Write-Host "  Modo:           Descargar y notificar antes de instalar" -ForegroundColor White }
                4 { Write-Host "  Modo:           Instalacion automatica programada" -ForegroundColor White }
                5 { Write-Host "  Modo:           Permitir admin local elegir" -ForegroundColor White }
            }

            Write-Host ""
            Write-Host "  --- Proteccion ---" -ForegroundColor DarkCyan
            $noReboot = try { $au.NoAutoRebootWithLoggedOnUsers } catch { "N/A" }
            if ($noReboot -eq 1) {
                Write-Host "  Reinicio auto:  BLOQUEADO (si hay usuario logueado)" -ForegroundColor Green
            } elseif ($noReboot -eq 0) {
                Write-Host "  Reinicio auto:  PERMITIDO (puede reiniciar sin aviso!)" -ForegroundColor Red
            } else {
                Write-Host "  Reinicio auto:  No configurado" -ForegroundColor Gray
            }

            Write-Host ""
            Write-Host "  --- Frecuencia ---" -ForegroundColor DarkCyan
            $detEnabled = try { $au.DetectionFrequencyEnabled } catch { 0 }
            $detFreq = try { $au.DetectionFrequency } catch { "Default" }
            if ($detEnabled -eq 1) {
                Write-Host "  Busqueda cada:  $detFreq horas" -ForegroundColor White
            } else {
                Write-Host "  Busqueda:       Frecuencia por defecto (22h)" -ForegroundColor White
            }

            $inclRec = try { $au.IncludeRecommendedUpdates } catch { "N/A" }
            Write-Host "  Recomendadas:   $(if ($inclRec -eq 1) {'SI'} else {'NO'})" -ForegroundColor White

            $autoMinor = try { $au.AutoInstallMinorUpdates } catch { "N/A" }
            Write-Host "  Auto menores:   $(if ($autoMinor -eq 1) {'SI'} else {'NO'})" -ForegroundColor White

            Write-Host ""
            Write-Host "  --- Servicio ---" -ForegroundColor DarkCyan
            $svc = Get-Service wuauserv
            Write-Host "  Windows Update: $($svc.Status)" -ForegroundColor White

        } catch {
            Write-Host "  WSUS no configurado" -ForegroundColor Red
            Write-Host "  Ejecutar: .\WSUS-Configure.ps1 (opcion 2 - setup)" -ForegroundColor White
        }
        Write-Host ""
    }

    "check" {
        Write-Host "Forzando busqueda de actualizaciones..." -ForegroundColor Cyan

        # Verificar que esta habilitado
        try {
            $au = Get-ItemProperty $RegPathAU -ErrorAction Stop
            if ($au.NoAutoUpdate -eq 1) {
                Write-Host "ATENCION: Actualizaciones deshabilitadas. Habilitar primero:" -ForegroundColor Yellow
                Write-Host "  .\WSUS-Configure.ps1 -Action enable" -ForegroundColor White
                exit 0
            }
        } catch {}

        # Forzar busqueda
        $updateSession = New-Object -ComObject Microsoft.Update.Session
        $updateSearcher = $updateSession.CreateUpdateSearcher()
        Write-Host "Buscando actualizaciones en WSUS (puede tardar)..." -ForegroundColor White
        try {
            $searchResult = $updateSearcher.Search("IsInstalled=0")
            Write-Host ""
            if ($searchResult.Updates.Count -eq 0) {
                Write-Host "No hay actualizaciones pendientes" -ForegroundColor Green
            } else {
                Write-Host "Actualizaciones disponibles: $($searchResult.Updates.Count)" -ForegroundColor Yellow
                foreach ($update in $searchResult.Updates) {
                    Write-Host "  - $($update.Title)" -ForegroundColor White
                }
            }
        } catch {
            Write-Host "Error buscando actualizaciones: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "Verificar conectividad con $WsusServer" -ForegroundColor Yellow
        }
    }

    "install" {
        Write-Host "===============================================" -ForegroundColor Cyan
        Write-Host " Instalar actualizaciones pendientes" -ForegroundColor Cyan
        Write-Host "===============================================" -ForegroundColor Cyan

        $updateSession = New-Object -ComObject Microsoft.Update.Session
        $updateSearcher = $updateSession.CreateUpdateSearcher()

        Write-Host "Buscando actualizaciones pendientes..." -ForegroundColor White
        try {
            $searchResult = $updateSearcher.Search("IsInstalled=0")

            if ($searchResult.Updates.Count -eq 0) {
                Write-Host "No hay actualizaciones pendientes" -ForegroundColor Green
                exit 0
            }

            Write-Host "Actualizaciones a instalar: $($searchResult.Updates.Count)" -ForegroundColor Yellow
            foreach ($update in $searchResult.Updates) {
                Write-Host "  - $($update.Title)" -ForegroundColor White
            }

            Write-Host ""
            $confirm = Read-Host "Instalar? (S/N)"
            if ($confirm -ne "S" -and $confirm -ne "s") {
                Write-Host "Cancelado" -ForegroundColor Yellow
                exit 0
            }

            # Descargar
            Write-Host "Descargando actualizaciones..." -ForegroundColor Cyan
            $updatesToDownload = New-Object -ComObject Microsoft.Update.UpdateColl
            foreach ($update in $searchResult.Updates) {
                if (-not $update.IsDownloaded) {
                    $updatesToDownload.Add($update) | Out-Null
                }
            }
            if ($updatesToDownload.Count -gt 0) {
                $downloader = $updateSession.CreateUpdateDownloader()
                $downloader.Updates = $updatesToDownload
                $downloader.Download() | Out-Null
            }

            # Instalar
            Write-Host "Instalando actualizaciones..." -ForegroundColor Cyan
            $updatesToInstall = New-Object -ComObject Microsoft.Update.UpdateColl
            foreach ($update in $searchResult.Updates) {
                if ($update.IsDownloaded) {
                    $updatesToInstall.Add($update) | Out-Null
                }
            }
            $installer = $updateSession.CreateUpdateInstaller()
            $installer.Updates = $updatesToInstall
            $result = $installer.Install()

            Write-Host ""
            switch ($result.ResultCode) {
                2 { Write-Host "Instalacion completada correctamente" -ForegroundColor Green }
                3 { Write-Host "Instalacion completada con errores" -ForegroundColor Yellow }
                4 { Write-Host "Instalacion fallida" -ForegroundColor Red }
                5 { Write-Host "Instalacion abortada" -ForegroundColor Red }
            }

            if ($result.RebootRequired) {
                Write-Host ""
                Write-Host "REINICIO REQUERIDO para completar la instalacion" -ForegroundColor Yellow
                $reboot = Read-Host "Reiniciar ahora? (S/N)"
                if ($reboot -eq "S" -or $reboot -eq "s") {
                    Restart-Computer -Force
                }
            }

        } catch {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    "reset" {
        Write-Host "===============================================" -ForegroundColor Red
        Write-Host " Eliminar TODA la configuracion WSUS" -ForegroundColor Red
        Write-Host "===============================================" -ForegroundColor Red
        Write-Host ""
        Write-Host "Esto eliminara TODAS las claves de registro de WSUS" -ForegroundColor Yellow
        Write-Host "Windows Update volvera a buscar directamente en Microsoft Update" -ForegroundColor Yellow
        Write-Host ""

        $confirm = Read-Host "Estas seguro? (S/N)"
        if ($confirm -ne "S" -and $confirm -ne "s") {
            Write-Host "Cancelado" -ForegroundColor Yellow
            break
        }

        if (Test-Path $RegPathWU) {
            Remove-Item $RegPathWU -Recurse -Force
            Write-Host "Claves de registro WSUS eliminadas" -ForegroundColor Green
        } else {
            Write-Host "No habia configuracion WSUS (ya estaba limpio)" -ForegroundColor Gray
        }

        Restart-Service wuauserv -Force
        Write-Host "Servicio Windows Update reiniciado" -ForegroundColor Green
        Write-Host ""
        Write-Host "Windows Update restaurado a estado original" -ForegroundColor Green
        Write-Host "Actualizaciones se buscaran en Microsoft Update (Internet)" -ForegroundColor White
    }

    "modo" {
        Write-Host "===============================================" -ForegroundColor Cyan
        Write-Host " Cambiar modo de actualizacion" -ForegroundColor Cyan
        Write-Host "===============================================" -ForegroundColor Cyan

        try {
            $au = Get-ItemProperty $RegPathAU -ErrorAction Stop
            $currentMode = $au.AUOptions
            Write-Host ""
            switch ($currentMode) {
                3 { Write-Host "  Modo actual: MANUAL (descarga y avisa, tu decides cuando instalar)" -ForegroundColor Yellow }
                4 { Write-Host "  Modo actual: AUTOMATICO (descarga e instala segun programacion)" -ForegroundColor Green }
                default { Write-Host "  Modo actual: $currentMode" -ForegroundColor White }
            }
        } catch {
            Write-Host "  WSUS no configurado. Ejecutar setup primero." -ForegroundColor Red
            break
        }

        Write-Host ""
        Write-Host "  1) MANUAL     - Descarga y avisa. Tu decides cuando instalar" -ForegroundColor Yellow
        Write-Host "  2) AUTOMATICO - Descarga e instala automaticamente" -ForegroundColor Green
        Write-Host "  0) Cancelar" -ForegroundColor Gray
        Write-Host ""
        $modoChoice = Read-Host "  Selecciona modo"
        switch ($modoChoice) {
            "1" {
                Set-ItemProperty -Path $RegPathAU -Name "AUOptions" -Value 3
                Restart-Service wuauserv -Force
                Write-Host "  Modo cambiado a MANUAL" -ForegroundColor Yellow
                Write-Host "  Windows descargara actualizaciones y te avisara antes de instalar" -ForegroundColor White
            }
            "2" {
                Write-Host ""
                Write-Host "  ATENCION: En modo automatico, Windows instalara actualizaciones solo." -ForegroundColor Red
                Write-Host "  El reinicio sigue BLOQUEADO si hay usuario logueado." -ForegroundColor Yellow
                $confirmAuto = Read-Host "  Estas seguro? (S/N)"
                if ($confirmAuto -eq "S" -or $confirmAuto -eq "s") {
                    Set-ItemProperty -Path $RegPathAU -Name "AUOptions" -Value 4
                    Restart-Service wuauserv -Force
                    Write-Host "  Modo cambiado a AUTOMATICO" -ForegroundColor Green
                } else {
                    Write-Host "  Cancelado" -ForegroundColor Yellow
                }
            }
            "0" { Write-Host "  Cancelado" -ForegroundColor Gray }
            default { Write-Host "  Opcion no valida" -ForegroundColor Red }
        }
    }
}

# Pausa para que no se cierre la ventana elevada
Write-Host ""
Read-Host "Pulsa Enter para salir"
