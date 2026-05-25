# Adds qrPreview.platform.* keys to every Projects/*/translations/translations.json
# Idempotent: skips files that already contain the keys.
param(
  [string]$Root = (Join-Path $PSScriptRoot '..')
)

$ErrorActionPreference = 'Stop'

$newKeys = [ordered]@{
  'qrPreview.platform.apple'   = [ordered]@{ SPA='iPhone / iPad'; ENG='iPhone / iPad'; FRA='iPhone / iPad'; ITA='iPhone / iPad' }
  'qrPreview.platform.android' = [ordered]@{ SPA='Android'; ENG='Android'; FRA='Android'; ITA='Android' }
  'qrPreview.platform.helpApple'   = [ordered]@{
    SPA='Abre la app Mail nativa (iOS).'
    ENG='Opens the native Mail app (iOS).'
    FRA="Ouvre l'application Mail native (iOS)."
    ITA="Apre l'app Mail nativa (iOS)."
  }
  'qrPreview.platform.helpAndroid' = [ordered]@{
    SPA='Escanea con Google Lens (app Fotos) para abrir tu cliente de correo.'
    ENG='Scan with Google Lens (Photos app) to open your email client.'
    FRA="Scannez avec Google Lens (app Photos) pour ouvrir votre client de messagerie."
    ITA='Scansiona con Google Lens (app Foto) per aprire il tuo client di posta.'
  }

  # ── SupportModal: asunto y cuerpo del QR de soporte/recuperación
  'support.qr.subjectRecovery' = [ordered]@{
    SPA='[Aquafrisch] Recuperación contraseña - {{id}}'
    ENG='[Aquafrisch] Password recovery - {{id}}'
    FRA='[Aquafrisch] Récupération mot de passe - {{id}}'
    ITA='[Aquafrisch] Recupero password - {{id}}'
  }
  'support.qr.subjectSupport' = [ordered]@{
    SPA='[Aquafrisch] Solicitud soporte - {{id}}'
    ENG='[Aquafrisch] Support request - {{id}}'
    FRA='[Aquafrisch] Demande de support - {{id}}'
    ITA='[Aquafrisch] Richiesta supporto - {{id}}'
  }
  'support.qr.label.installationId' = [ordered]@{ SPA='Installation ID'; ENG='Installation ID'; FRA="ID d'installation"; ITA='ID Installazione' }
  'support.qr.label.dateTime'       = [ordered]@{ SPA='Fecha/hora';      ENG='Date/time';      FRA='Date/heure';        ITA='Data/ora' }
  'support.qr.label.challenge'      = [ordered]@{ SPA='Challenge code';  ENG='Challenge code'; FRA='Code défi';         ITA='Codice challenge' }
  'support.qr.label.describe'       = [ordered]@{ SPA='Describa el problema:'; ENG='Describe the issue:'; FRA='Décrivez le problème :'; ITA='Descriva il problema:' }
  'support.data.not_available'      = [ordered]@{ SPA='N/D'; ENG='N/A'; FRA='N/D'; ITA='N/D' }

  # ── StatisticsView · ConsumablesOrderModal: QR de pedido
  'statistics.order.qr.subject' = [ordered]@{
    SPA='Pedido {{ref}} - {{element}}'
    ENG='Order {{ref}} - {{element}}'
    FRA='Commande {{ref}} - {{element}}'
    ITA='Ordine {{ref}} - {{element}}'
  }
  'statistics.order.qr.errorNoQty' = [ordered]@{
    SPA='Indica al menos una cantidad > 0'
    ENG='Set at least one quantity > 0'
    FRA="Indiquez au moins une quantité > 0"
    ITA='Indica almeno una quantità > 0'
  }
  'statistics.order.qr.label.ref'     = [ordered]@{ SPA='Ref';      ENG='Ref';      FRA='Réf';      ITA='Rif' }
  'statistics.order.qr.label.date'    = [ordered]@{ SPA='Fecha';    ENG='Date';     FRA='Date';     ITA='Data' }
  'statistics.order.qr.label.element' = [ordered]@{ SPA='Elemento'; ENG='Element';  FRA='Élément';  ITA='Elemento' }
  'statistics.order.qr.label.sku'     = [ordered]@{ SPA='SKU';      ENG='SKU';      FRA='SKU';      ITA='SKU' }
  'statistics.order.qr.label.lines'   = [ordered]@{ SPA='--- LINEAS ---'; ENG='--- LINES ---'; FRA='--- LIGNES ---'; ITA='--- RIGHE ---' }
  'statistics.order.qr.label.footer'  = [ordered]@{
    SPA='Generado desde Aquafrisch Supervisor'
    ENG='Generated from Aquafrisch Supervisor'
    FRA='Généré depuis Aquafrisch Supervisor'
    ITA='Generato da Aquafrisch Supervisor'
  }
  'statistics.order.qr.label.unit'    = [ordered]@{ SPA='ud'; ENG='un'; FRA='u'; ITA='u' }

  # ── StatisticsView · ConsumablesOrderModal: UI del modal de pedido
  'statistics.order.ui.title' = [ordered]@{
    SPA='Pedido de repuestos / consumibles'
    ENG='Spare parts / consumables order'
    FRA='Commande de pièces / consommables'
    ITA='Ordine ricambi / consumabili'
  }
  'statistics.order.ui.element' = [ordered]@{ SPA='Elemento'; ENG='Element'; FRA='Élément'; ITA='Elemento' }
  'statistics.order.ui.col.task'        = [ordered]@{ SPA='Tarea / Tipo'; ENG='Task / Type'; FRA='Tâche / Type'; ITA='Attività / Tipo' }
  'statistics.order.ui.col.description' = [ordered]@{ SPA='Descripción';  ENG='Description'; FRA='Description'; ITA='Descrizione' }
  'statistics.order.ui.col.unit'        = [ordered]@{ SPA='Unidad';       ENG='Unit';        FRA='Unité';       ITA='Unità' }
  'statistics.order.ui.col.qty'         = [ordered]@{ SPA='Cantidad';     ENG='Quantity';    FRA='Quantité';    ITA='Quantità' }
  'statistics.order.ui.noConsumables' = [ordered]@{
    SPA='Sin consumibles definidos para este elemento.'
    ENG='No consumables defined for this element.'
    FRA='Aucun consommable défini pour cet élément.'
    ITA='Nessun consumabile definito per questo elemento.'
  }
  'statistics.order.ui.channelTitle' = [ordered]@{
    SPA='Vía de envío — {{count}} línea(s) seleccionada(s)'
    ENG='Delivery channel — {{count}} line(s) selected'
    FRA="Canal d'envoi — {{count}} ligne(s) sélectionnée(s)"
    ITA='Canale di invio — {{count}} riga/righe selezionate'
  }
  'statistics.order.ui.channel.print.label' = [ordered]@{ SPA='Imprimir / PDF'; ENG='Print / PDF'; FRA='Imprimer / PDF'; ITA='Stampa / PDF' }
  'statistics.order.ui.channel.print.sub'   = [ordered]@{ SPA='Diálogo navegador'; ENG='Browser dialog'; FRA='Dialogue navigateur'; ITA='Dialogo browser' }
  'statistics.order.ui.channel.qr.label'    = [ordered]@{ SPA='Generar QR'; ENG='Generate QR'; FRA='Générer QR'; ITA='Genera QR' }
  'statistics.order.ui.channel.qr.sub'      = [ordered]@{
    SPA='Abre Mail en el móvil'
    ENG='Opens Mail on the phone'
    FRA='Ouvre Mail sur le mobile'
    ITA='Apre Mail sul cellulare'
  }
  'statistics.order.ui.channel.file.label'  = [ordered]@{ SPA='Guardar archivo'; ENG='Save file'; FRA='Enregistrer le fichier'; ITA='Salva file' }
  'statistics.order.ui.channel.file.sub'    = [ordered]@{
    SPA='Disco · USB · Red — Pendiente'
    ENG='Disk · USB · Network — Pending'
    FRA='Disque · USB · Réseau — En attente'
    ITA='Disco · USB · Rete — In sospeso'
  }
  'statistics.order.ui.channel.email.label' = [ordered]@{ SPA='Enviar email'; ENG='Send email'; FRA='Envoyer un e-mail'; ITA='Invia email' }
  'statistics.order.ui.channel.email.sub'   = [ordered]@{
    SPA='Pendiente gestión correos'
    ENG='Mail management pending'
    FRA='Gestion des e-mails en attente'
    ITA='Gestione email in sospeso'
  }
  'statistics.order.ui.kioskNote' = [ordered]@{
    SPA='Sistema en kiosko: el operador no tiene acceso al SO. Las vías marcadas como "Pendiente" requieren funcionalidad adicional aún no implementada.'
    ENG='Kiosk system: the operator has no OS access. Channels marked as "Pending" require additional functionality not yet implemented.'
    FRA="Système en mode kiosque : l'opérateur n'a pas accès au système d'exploitation. Les canaux marqués « En attente » nécessitent des fonctionnalités supplémentaires non encore implémentées."
    ITA='Sistema kiosk: l''operatore non ha accesso al SO. I canali contrassegnati come "In sospeso" richiedono funzionalità aggiuntive non ancora implementate.'
  }
  'statistics.order.ui.previewTitle' = [ordered]@{
    SPA='Vista previa — Pedido {{element}}'
    ENG='Preview — Order {{element}}'
    FRA='Aperçu — Commande {{element}}'
    ITA='Anteprima — Ordine {{element}}'
  }
  'statistics.order.ui.printNotes' = [ordered]@{
    SPA='Pedido generado para {{element}}'
    ENG='Order generated for {{element}}'
    FRA='Commande générée pour {{element}}'
    ITA='Ordine generato per {{element}}'
  }
  'statistics.order.ui.errorBuild' = [ordered]@{
    SPA='Error generando pedido'
    ENG='Error generating order'
    FRA='Erreur de génération de la commande'
    ITA='Errore generazione ordine'
  }

  # ── StatisticsView · PlantExportModal: QR de informe de planta
  'statistics.plant.qr.title' = [ordered]@{
    SPA='AQUAFRISCH — INFORME PLANTA'
    ENG='AQUAFRISCH — PLANT REPORT'
    FRA='AQUAFRISCH — RAPPORT USINE'
    ITA='AQUAFRISCH — REPORT IMPIANTO'
  }
  'statistics.plant.qr.date'     = [ordered]@{ SPA='Fecha';  ENG='Date';   FRA='Date';   ITA='Data' }
  'statistics.plant.qr.health'   = [ordered]@{ SPA='Salud';  ENG='Health'; FRA='Santé';  ITA='Salute' }
  'statistics.plant.qr.critShort'= [ordered]@{ SPA='Crit';   ENG='Crit';   FRA='Crit';   ITA='Crit' }
  'statistics.plant.qr.warnShort'= [ordered]@{ SPA='Atn';    ENG='Warn';   FRA='Att';    ITA='Att' }
  'statistics.plant.qr.total'    = [ordered]@{ SPA='Total';  ENG='Total';  FRA='Total';  ITA='Totale' }
  'statistics.plant.qr.life'     = [ordered]@{ SPA='Vida';   ENG='Life';   FRA='Vie';    ITA='Vita' }
  'statistics.plant.qr.mt'       = [ordered]@{ SPA='Mt';     ENG='Mt';     FRA='Mt';     ITA='Mt' }
  'statistics.plant.qr.elements' = [ordered]@{ SPA='el';     ENG='el';     FRA='él';     ITA='el' }
  'statistics.plant.qr.lifeCritical' = [ordered]@{ SPA='VIDA CRITICOS'; ENG='LIFE CRITICAL'; FRA='VIE CRITIQUES'; ITA='VITA CRITICI' }
  'statistics.plant.qr.mtCritical'   = [ordered]@{ SPA='MT CRITICOS';   ENG='MT CRITICAL';   FRA='MT CRITIQUES';   ITA='MT CRITICI' }
  'statistics.plant.qr.lifeWarning'  = [ordered]@{ SPA='VIDA ATENCION'; ENG='LIFE WARNING';  FRA='VIE ATTENTION';  ITA='VITA ATTENZIONE' }
  'statistics.plant.qr.mtWarning'    = [ordered]@{ SPA='MT ATENCION';   ENG='MT WARNING';    FRA='MT ATTENTION';   ITA='MT ATTENZIONE' }
  'statistics.plant.qr.lifeOk'       = [ordered]@{ SPA='VIDA OK';       ENG='LIFE OK';       FRA='VIE OK';         ITA='VITA OK' }
  'statistics.plant.qr.mtOk'         = [ordered]@{ SPA='MT OK';         ENG='MT OK';         FRA='MT OK';          ITA='MT OK' }
  'statistics.plant.qr.interventions'= [ordered]@{ SPA='INTERVENCIONES'; ENG='INTERVENTIONS'; FRA='INTERVENTIONS'; ITA='INTERVENTI' }
  'statistics.plant.qr.order'        = [ordered]@{ SPA='PEDIDO';        ENG='ORDER';         FRA='COMMANDE';       ITA='ORDINE' }
  'statistics.plant.qr.omitted'      = [ordered]@{
    SPA='[+{{count}} omitidos — ver PDF]'
    ENG='[+{{count}} omitted — see PDF]'
    FRA='[+{{count}} omis — voir PDF]'
    ITA='[+{{count}} omessi — vedi PDF]'
  }
  'statistics.plant.qr.subject' = [ordered]@{
    SPA='Informe planta {{pct}} - {{date}}'
    ENG='Plant report {{pct}} - {{date}}'
    FRA='Rapport usine {{pct}} - {{date}}'
    ITA='Report impianto {{pct}} - {{date}}'
  }
  'statistics.plant.qr.error' = [ordered]@{
    SPA='Error generando QR'
    ENG='Error generating QR'
    FRA='Erreur de génération du QR'
    ITA='Errore generazione QR'
  }

  # ── StatisticsView · MaintenanceTab: warnings de configuración Excel
  'statistics.maintenance.config_warning.no_maintenance_group' = [ordered]@{
    SPA='No hay ningún grupo marcado como "Mantenimiento" en Excel (columna ShowInMaintenance). Las barras de vida pueden no aparecer.'
    ENG='No group is marked as "Maintenance" in Excel (column ShowInMaintenance). Life bars may not appear.'
    FRA='Aucun groupe marqué comme « Maintenance » dans Excel (colonne ShowInMaintenance). Les barres de vie peuvent ne pas s''afficher.'
    ITA='Nessun gruppo contrassegnato come "Manutenzione" in Excel (colonna ShowInMaintenance). Le barre di vita potrebbero non apparire.'
  }
  'statistics.maintenance.config_warning.multiple_maintenance_groups' = [ordered]@{
    SPA='Hay {{count}} grupos marcados como mantenimiento ({{names}}). Sólo debería haber uno; los datos pueden mezclarse. Corrige el Excel y sincroniza.'
    ENG='There are {{count}} groups marked as maintenance ({{names}}). Only one should exist; data may mix. Fix the Excel and sync.'
    FRA='Il y a {{count}} groupes marqués comme maintenance ({{names}}). Un seul devrait exister ; les données peuvent se mélanger. Corrigez l''Excel et synchronisez.'
    ITA='Ci sono {{count}} gruppi contrassegnati come manutenzione ({{names}}). Dovrebbe essercene solo uno; i dati possono mescolarsi. Correggi l''Excel e sincronizza.'
  }
  'statistics.maintenance.config_warning.duplicate_lifecycle_vars' = [ordered]@{
    SPA='{{count}} elemento(s) tienen más de una variable de ciclo de vida. Sólo se usará la primera: {{sample}}{{more}}.'
    ENG='{{count}} element(s) have more than one lifecycle variable. Only the first will be used: {{sample}}{{more}}.'
    FRA='{{count}} élément(s) ont plus d''une variable de cycle de vie. Seule la première sera utilisée : {{sample}}{{more}}.'
    ITA='{{count}} elemento(i) hanno più di una variabile di ciclo di vita. Sarà usata solo la prima: {{sample}}{{more}}.'
  }

  # ── StatisticsView · MaintenanceTab: snapshot / reset / hard purge
  'statistics.maintenance.snapshot.error_title'   = [ordered]@{ SPA='Error de snapshot'; ENG='Snapshot error'; FRA='Erreur de snapshot'; ITA='Errore snapshot' }
  'statistics.maintenance.snapshot.error_message' = [ordered]@{
    SPA='Snapshot del PLC falló: {{error}}'
    ENG='PLC snapshot failed: {{error}}'
    FRA='Le snapshot de l''API a échoué : {{error}}'
    ITA='Snapshot del PLC fallito: {{error}}'
  }
  'statistics.maintenance.reset.alert_title' = [ordered]@{ SPA='✅ Mantenimiento reseteado'; ENG='✅ Maintenance reset'; FRA='✅ Maintenance réinitialisée'; ITA='✅ Manutenzione reimpostata' }
  'statistics.maintenance.reset.alert_message' = [ordered]@{
    SPA="Borrado:`n  intervenciones: {{dInt}}`n  lifecycles: {{dLife}}`n  used parts: {{dParts}}`n`nCreado:`n  lifecycles: {{cLife}}`n  intervenciones: {{cInt}}`n`nLas barras deberían marcar 0% al refrescar."
    ENG="Deleted:`n  interventions: {{dInt}}`n  lifecycles: {{dLife}}`n  used parts: {{dParts}}`n`nCreated:`n  lifecycles: {{cLife}}`n  interventions: {{cInt}}`n`nBars should display 0% upon refresh."
    FRA="Supprimés :`n  interventions : {{dInt}}`n  cycles de vie : {{dLife}}`n  pièces utilisées : {{dParts}}`n`nCréés :`n  cycles de vie : {{cLife}}`n  interventions : {{cInt}}`n`nLes barres devraient afficher 0 % après rafraîchissement."
    ITA="Cancellati:`n  interventi: {{dInt}}`n  cicli di vita: {{dLife}}`n  parti usate: {{dParts}}`n`nCreati:`n  cicli di vita: {{cLife}}`n  interventi: {{cInt}}`n`nLe barre dovrebbero mostrare 0% al refresh."
  }
  'statistics.maintenance.hard_purge.alert_title' = [ordered]@{ SPA='✅ BD de mantenimiento vaciada'; ENG='✅ Maintenance DB emptied'; FRA='✅ BD de maintenance vidée'; ITA='✅ DB di manutenzione svuotato' }
  'statistics.maintenance.hard_purge.error_title' = [ordered]@{ SPA='Error en HARD PURGE'; ENG='HARD PURGE error'; FRA='Erreur HARD PURGE'; ITA='Errore HARD PURGE' }
  'statistics.maintenance.hard_purge.line.interventions'      = [ordered]@{ SPA='Intervenciones'; ENG='Interventions'; FRA='Interventions'; ITA='Interventi' }
  'statistics.maintenance.hard_purge.line.lifecycles'         = [ordered]@{ SPA='Lifecycles';     ENG='Lifecycles';    FRA='Cycles de vie'; ITA='Cicli di vita' }
  'statistics.maintenance.hard_purge.line.used_parts'         = [ordered]@{ SPA='Used parts';     ENG='Used parts';    FRA='Pièces utilisées'; ITA='Parti usate' }
  'statistics.maintenance.hard_purge.line.predictions'        = [ordered]@{ SPA='Predicciones';   ENG='Predictions';   FRA='Prédictions';   ITA='Predizioni' }
  'statistics.maintenance.hard_purge.line.derived_stats'      = [ordered]@{ SPA='Stats derivadas'; ENG='Derived stats'; FRA='Stats dérivées'; ITA='Stat derivate' }
  'statistics.maintenance.hard_purge.line.readings'           = [ordered]@{ SPA='Lecturas';       ENG='Readings';      FRA='Lectures';      ITA='Letture' }
  'statistics.maintenance.hard_purge.line.cycles'             = [ordered]@{ SPA='Ciclos';         ENG='Cycles';        FRA='Cycles';        ITA='Cicli' }
  'statistics.maintenance.hard_purge.line.wrap_tracking_reset'= [ordered]@{ SPA='Wrap-tracking reset'; ENG='Wrap-tracking reset'; FRA='Réinit. wrap-tracking'; ITA='Reset wrap-tracking' }

  # ── Comunes
  'common.error'         = [ordered]@{ SPA='Error';            ENG='Error';            FRA='Erreur';            ITA='Errore' }
  'common.unknown_error' = [ordered]@{ SPA='error desconocido'; ENG='unknown error';    FRA='erreur inconnue';   ITA='errore sconosciuto' }

  # ── GeneralHealthBar
  'statistics.generalHealth.title'                  = [ordered]@{ SPA='SALUD GENERAL DE LA PLANTA'; ENG='OVERALL PLANT HEALTH'; FRA="SANTÉ GÉNÉRALE DE L'USINE"; ITA="SALUTE GENERALE DELL'IMPIANTO" }
  'statistics.generalHealth.lifecycle_bar_label'    = [ordered]@{ SPA='Ciclo de vida';   ENG='Lifecycle';     FRA='Cycle de vie'; ITA='Ciclo di vita' }
  'statistics.generalHealth.maintenance_bar_label'  = [ordered]@{ SPA='Mantenimiento';   ENG='Maintenance';   FRA='Maintenance';  ITA='Manutenzione' }

  # ── ElementLifeBar
  'statistics.elementLifeBar.subcomponents_short' = [ordered]@{ SPA='sub-comp.'; ENG='sub-comp.'; FRA='sous-comp.'; ITA='sotto-comp.' }
  'statistics.elementLifeBar.worst_short'         = [ordered]@{ SPA='peor {{pct}}%'; ENG='worst {{pct}}%'; FRA='pire {{pct}}%'; ITA='peggiore {{pct}}%' }
  'statistics.elementLifeBar.worst_lifecycle'     = [ordered]@{ SPA='Peor ciclo de vida'; ENG='Worst lifecycle'; FRA='Pire cycle de vie'; ITA='Peggior ciclo di vita' }
  'statistics.elementLifeBar.worst_maintenance'   = [ordered]@{ SPA='Peor mantenimiento'; ENG='Worst maintenance'; FRA='Pire maintenance'; ITA='Peggior manutenzione' }
  'statistics.elementLifeBar.no_metric'           = [ordered]@{
    SPA='Sin métrica de vida definida en Excel para este elemento.'
    ENG='No life metric defined in Excel for this element.'
    FRA='Aucune métrique de vie définie dans Excel pour cet élément.'
    ITA='Nessuna metrica di vita definita in Excel per questo elemento.'
  }

  # ── ElementMediaViewer
  'statistics.elementMediaViewer.worst_subcomponent' = [ordered]@{
    SPA='peor {{pct}}%'; ENG='worst {{pct}}%'; FRA='pire {{pct}}%'; ITA='peggiore {{pct}}%'
  }
  'statistics.elementMediaViewer.no_metric' = [ordered]@{
    SPA='Sin métrica de vida definida.'
    ENG='No life metric defined.'
    FRA='Aucune métrique de vie définie.'
    ITA='Nessuna metrica di vita definita.'
  }
  'statistics.elementMediaViewer.intervention_button' = [ordered]@{ SPA='Intervención'; ENG='Intervention'; FRA='Intervention'; ITA='Intervento' }
  'statistics.elementMediaViewer.order_button'        = [ordered]@{ SPA='Pedido ({{count}})'; ENG='Order ({{count}})'; FRA='Commande ({{count}})'; ITA='Ordine ({{count}})' }

  # ── ManualQR
  'statistics.manualQR.close' = [ordered]@{ SPA='Cerrar'; ENG='Close'; FRA='Fermer'; ITA='Chiudi' }

  # ── MarkDoneButton
  'statistics.markDone.button'       = [ordered]@{ SPA='✓ Hecho'; ENG='✓ Done'; FRA='✓ Fait'; ITA='✓ Fatto' }
  'statistics.markDone.title'        = [ordered]@{ SPA='Marcar mantenimiento hecho'; ENG='Mark maintenance as done'; FRA='Marquer la maintenance comme effectuée'; ITA='Contrassegna manutenzione come effettuata' }
  'statistics.markDone.info_prefix'  = [ordered]@{
    SPA='Se registrará una intervención con valor de referencia'
    ENG='An intervention will be recorded with baseline value'
    FRA='Une intervention sera enregistrée avec la valeur de référence'
    ITA="Sarà registrato un intervento con valore di riferimento"
  }
  'statistics.markDone.info_suffix' = [ordered]@{
    SPA='La barra se reiniciará a 0% en la próxima recarga.'
    ENG='The bar will reset to 0% on the next reload.'
    FRA='La barre sera réinitialisée à 0 % au prochain rechargement.'
    ITA='La barra sarà reimpostata a 0% al prossimo ricaricamento.'
  }
  'statistics.markDone.workorder_label'       = [ordered]@{ SPA='Orden de trabajo (opcional)'; ENG='Work order (optional)'; FRA='Ordre de travail (optionnel)'; ITA='Ordine di lavoro (opzionale)' }
  'statistics.markDone.workorder_placeholder' = [ordered]@{ SPA='ej: OT-2026-0042'; ENG='e.g. OT-2026-0042'; FRA='ex. : OT-2026-0042'; ITA='es. OT-2026-0042' }
  'statistics.markDone.notes_label'           = [ordered]@{ SPA='Notas (opcional)'; ENG='Notes (optional)'; FRA='Notes (optionnel)'; ITA='Note (opzionale)' }
  'statistics.markDone.notes_placeholder'     = [ordered]@{ SPA='Observaciones, mediciones…'; ENG='Observations, measurements…'; FRA='Observations, mesures…'; ITA='Osservazioni, misurazioni…' }
  'statistics.markDone.consumables_label'     = [ordered]@{ SPA='Consumibles utilizados (opcional)'; ENG='Consumables used (optional)'; FRA='Consommables utilisés (optionnel)'; ITA='Consumabili utilizzati (opzionale)' }
  'statistics.markDone.cancel'                = [ordered]@{ SPA='Cancelar'; ENG='Cancel'; FRA='Annuler'; ITA='Annulla' }
  'statistics.markDone.confirm'               = [ordered]@{ SPA='✓ Confirmar'; ENG='✓ Confirm'; FRA='✓ Confirmer'; ITA='✓ Conferma' }
  'statistics.markDone.saving'                = [ordered]@{ SPA='Guardando…'; ENG='Saving…'; FRA='Enregistrement…'; ITA='Salvataggio…' }

  # ── ChildLifeRow
  'statistics.childLifeRow.no_lifecycle' = [ordered]@{
    SPA='Sin métrica de vida definida.'
    ENG='No life metric defined.'
    FRA='Aucune métrique de vie définie.'
    ITA='Nessuna metrica di vita definita.'
  }

  # ── ElementDetailPanel
  'statistics.elementPanel.subcomponents_title'          = [ordered]@{ SPA='Sub-componentes'; ENG='Sub-components'; FRA='Sous-composants'; ITA='Sotto-componenti' }
  'statistics.elementPanel.intervention_history_title'   = [ordered]@{ SPA='Histórico intervenciones'; ENG='Intervention history'; FRA='Historique des interventions'; ITA='Storico interventi' }
  'statistics.elementPanel.no_metrics_message' = [ordered]@{
    SPA='ℹ Este elemento no tiene métricas propias de ciclo de vida ni mantenimiento. Las intervenciones se registran a nivel de cada sub-componente.'
    ENG='ℹ This element has no lifecycle or maintenance metrics of its own. Interventions are recorded at each sub-component.'
    FRA='ℹ Cet élément n''a pas de métriques propres de cycle de vie ni de maintenance. Les interventions sont enregistrées au niveau de chaque sous-composant.'
    ITA='ℹ Questo elemento non ha metriche proprie di ciclo di vita o manutenzione. Gli interventi vengono registrati a livello di ogni sotto-componente.'
  }
  'statistics.elementPanel.no_interventions_message' = [ordered]@{
    SPA='ℹ Sin intervenciones registradas.'
    ENG='ℹ No interventions recorded.'
    FRA='ℹ Aucune intervention enregistrée.'
    ITA='ℹ Nessun intervento registrato.'
  }
  'statistics.elementPanel.consumables_title'   = [ordered]@{ SPA='Consumibles del elemento'; ENG='Element consumables'; FRA="Consommables de l'élément"; ITA="Consumabili dell'elemento" }
  'statistics.elementPanel.loading_consumables' = [ordered]@{ SPA='Cargando consumibles…'; ENG='Loading consumables…'; FRA='Chargement des consommables…'; ITA='Caricamento consumabili…' }
  'statistics.elementPanel.intervention_table.date'       = [ordered]@{ SPA='Fecha';      ENG='Date';      FRA='Date';      ITA='Data' }
  'statistics.elementPanel.intervention_table.task'       = [ordered]@{ SPA='Tarea';      ENG='Task';      FRA='Tâche';     ITA='Attività' }
  'statistics.elementPanel.intervention_table.type'       = [ordered]@{ SPA='Tipo';       ENG='Type';      FRA='Type';      ITA='Tipo' }
  'statistics.elementPanel.intervention_table.operator'   = [ordered]@{ SPA='Operario';   ENG='Operator';  FRA='Opérateur'; ITA='Operatore' }
  'statistics.elementPanel.intervention_table.workorder'  = [ordered]@{ SPA='OT';         ENG='WO';        FRA='OT';        ITA='OT' }
  'statistics.elementPanel.intervention_table.notes'      = [ordered]@{ SPA='Notas';      ENG='Notes';     FRA='Notes';     ITA='Note' }
  'statistics.elementPanel.intervention_table.consumables'= [ordered]@{ SPA='Consumibles'; ENG='Consumables'; FRA='Consommables'; ITA='Consumabili' }
  'statistics.elementPanel.consumables_table.task'        = [ordered]@{ SPA='Tarea';      ENG='Task';      FRA='Tâche';     ITA='Attività' }
  'statistics.elementPanel.consumables_table.sku'         = [ordered]@{ SPA='SKU';        ENG='SKU';       FRA='SKU';       ITA='SKU' }
  'statistics.elementPanel.consumables_table.description' = [ordered]@{ SPA='Descripción'; ENG='Description'; FRA='Description'; ITA='Descrizione' }
  'statistics.elementPanel.consumables_table.unit'        = [ordered]@{ SPA='Unidad';     ENG='Unit';      FRA='Unité';     ITA='Unità' }
  'statistics.elementPanel.consumables_table.default_qty' = [ordered]@{ SPA='Cant. defecto'; ENG='Default qty'; FRA='Qté par défaut'; ITA='Qtà predefinita' }
  'statistics.elementPanel.consumables_table.doc'         = [ordered]@{ SPA='Doc';        ENG='Doc';       FRA='Doc';       ITA='Doc' }

  # ── InterventionWizard
  'statistics.wizard.title'                     = [ordered]@{ SPA='🛠 Nueva intervención'; ENG='🛠 New intervention'; FRA='🛠 Nouvelle intervention'; ITA='🛠 Nuovo intervento' }
  'statistics.wizard.type_label'                = [ordered]@{ SPA='Tipo de intervención'; ENG='Intervention type'; FRA="Type d'intervention"; ITA='Tipo di intervento' }
  'statistics.wizard.type.maintenance'          = [ordered]@{ SPA='Planificado'; ENG='Planned'; FRA='Planifié'; ITA='Pianificato' }
  'statistics.wizard.type.maintenance_sub'      = [ordered]@{ SPA='Resetea tarea'; ENG='Resets task'; FRA='Réinitialise la tâche'; ITA='Reimposta attività' }
  'statistics.wizard.type.extraordinary'        = [ordered]@{ SPA='Extraordinario'; ENG='Extraordinary'; FRA='Extraordinaire'; ITA='Straordinario' }
  'statistics.wizard.type.extraordinary_sub'    = [ordered]@{ SPA='Fuera de plan'; ENG='Outside plan'; FRA='Hors plan'; ITA='Fuori piano' }
  'statistics.wizard.type.replacement'          = [ordered]@{ SPA='Reemplazo'; ENG='Replacement'; FRA='Remplacement'; ITA='Sostituzione' }
  'statistics.wizard.type.replacement_sub'      = [ordered]@{ SPA='Cierra lifecycle'; ENG='Closes lifecycle'; FRA='Ferme le cycle de vie'; ITA='Chiude il ciclo di vita' }
  'statistics.wizard.type.inspection'           = [ordered]@{ SPA='Inspección'; ENG='Inspection'; FRA='Inspection'; ITA='Ispezione' }
  'statistics.wizard.type.inspection_sub'       = [ordered]@{ SPA='Sin reset valor'; ENG='No value reset'; FRA='Sans reset de valeur'; ITA='Senza reset valore' }
  'statistics.wizard.planned_task_label'        = [ordered]@{ SPA='Tarea planificada *'; ENG='Planned task *'; FRA='Tâche planifiée *'; ITA='Attività pianificata *' }
  'statistics.wizard.no_maintenance_tasks'      = [ordered]@{
    SPA='Este elemento no tiene mantenimientos planificados. Usa "Extraordinario".'
    ENG='This element has no planned maintenance. Use "Extraordinary".'
    FRA='Cet élément n''a aucune maintenance planifiée. Utilisez « Extraordinaire ».'
    ITA='Questo elemento non ha manutenzioni pianificate. Usa "Straordinario".'
  }
  'statistics.wizard.reset_hint'                = [ordered]@{ SPA='al confirmar reset → 0%'; ENG='on confirm reset → 0%'; FRA='à la confirmation reset → 0 %'; ITA='alla conferma reset → 0%' }
  'statistics.wizard.task_description_label'    = [ordered]@{ SPA='Tarea / descripción *'; ENG='Task / description *'; FRA='Tâche / description *'; ITA='Attività / descrizione *' }
  'statistics.wizard.inspection_placeholder'    = [ordered]@{ SPA='ej: Revisión visual fugas'; ENG='e.g. Visual inspection of leaks'; FRA='ex. : Inspection visuelle des fuites'; ITA='es. Ispezione visiva perdite' }
  'statistics.wizard.extraordinary_placeholder' = [ordered]@{ SPA='ej: Reparación urgente fuga aceite'; ENG='e.g. Emergency oil leak repair'; FRA="ex. : Réparation urgente d'une fuite d'huile"; ITA='es. Riparazione urgente perdita olio' }
  'statistics.wizard.workorder_label'           = [ordered]@{ SPA='Orden de trabajo (OT)'; ENG='Work order (WO)'; FRA='Ordre de travail (OT)'; ITA='Ordine di lavoro (OT)' }
  'statistics.wizard.workorder_placeholder'     = [ordered]@{ SPA='ej: OT-2026-0042'; ENG='e.g. OT-2026-0042'; FRA='ex. : OT-2026-0042'; ITA='es. OT-2026-0042' }
  'statistics.wizard.replacement_section_label' = [ordered]@{ SPA='Reemplazo de componente'; ENG='Component replacement'; FRA='Remplacement de composant'; ITA='Sostituzione componente' }
  'statistics.wizard.replacement_info_prefix'   = [ordered]@{ SPA='Se cerrará el lifecycle activo de'; ENG='The active lifecycle of'; FRA='Le cycle de vie actif de'; ITA='Sarà chiuso il ciclo di vita attivo di' }
  'statistics.wizard.replacement_info_suffix'   = [ordered]@{
    SPA='y se abrirá uno nuevo con todos los contadores reiniciados.'
    ENG='will be closed and a new one will start with all counters reset.'
    FRA='sera fermé et un nouveau sera ouvert avec tous les compteurs réinitialisés.'
    ITA='e ne sarà aperto uno nuovo con tutti i contatori azzerati.'
  }
  'statistics.wizard.replacement_auto_task_hint_prefix' = [ordered]@{
    SPA='La tarea se registrará automáticamente como'
    ENG='The task will be automatically recorded as'
    FRA='La tâche sera enregistrée automatiquement comme'
    ITA="L'attività sarà registrata automaticamente come"
  }
  'statistics.wizard.notes_label'              = [ordered]@{ SPA='Notas'; ENG='Notes'; FRA='Notes'; ITA='Note' }
  'statistics.wizard.notes_placeholder'        = [ordered]@{ SPA='Observaciones, mediciones, …'; ENG='Observations, measurements, …'; FRA='Observations, mesures, …'; ITA='Osservazioni, misurazioni, …' }
  'statistics.wizard.consumables_label'        = [ordered]@{ SPA='Consumibles utilizados'; ENG='Consumables used'; FRA='Consommables utilisés'; ITA='Consumabili utilizzati' }
  'statistics.wizard.no_consumables'           = [ordered]@{
    SPA='Sin consumibles asociados a este elemento.'
    ENG='No consumables associated with this element.'
    FRA='Aucun consommable associé à cet élément.'
    ITA='Nessun consumabile associato a questo elemento.'
  }
  'statistics.wizard.consumables_table.sku'         = [ordered]@{ SPA='SKU'; ENG='SKU'; FRA='SKU'; ITA='SKU' }
  'statistics.wizard.consumables_table.description' = [ordered]@{ SPA='Descripción'; ENG='Description'; FRA='Description'; ITA='Descrizione' }
  'statistics.wizard.consumables_table.quantity'    = [ordered]@{ SPA='Cantidad'; ENG='Quantity'; FRA='Quantité'; ITA='Quantità' }
  'statistics.wizard.cancel' = [ordered]@{ SPA='Cancelar'; ENG='Cancel'; FRA='Annuler'; ITA='Annulla' }
  'statistics.wizard.save'   = [ordered]@{ SPA='💾 Guardar intervención'; ENG='💾 Save intervention'; FRA='💾 Enregistrer intervention'; ITA='💾 Salva intervento' }
  'statistics.wizard.saving' = [ordered]@{ SPA='⏳ Guardando…'; ENG='⏳ Saving…'; FRA='⏳ Enregistrement…'; ITA='⏳ Salvataggio…' }
  'statistics.wizard.error_no_task_selected'    = [ordered]@{
    SPA='Selecciona una tarea de mantenimiento de la lista'
    ENG='Select a maintenance task from the list'
    FRA='Sélectionnez une tâche de maintenance dans la liste'
    ITA="Seleziona un'attività di manutenzione dall'elenco"
  }
  'statistics.wizard.error_no_task_description' = [ordered]@{
    SPA='Describe la tarea realizada'
    ENG='Describe the task performed'
    FRA='Décrivez la tâche effectuée'
    ITA="Descrivi l'attività eseguita"
  }

  # ── StatisticsView · PlantExportModal: UI del modal
  'statistics.plantExport.title'    = [ordered]@{ SPA='📤 Exportar informe de planta'; ENG='📤 Export plant report'; FRA="📤 Exporter le rapport d'usine"; ITA="📤 Esporta report dell'impianto" }
  'statistics.plantExport.subtitle' = [ordered]@{
    SPA='Selecciona las secciones a incluir y la vía de envío.'
    ENG='Select the sections to include and the delivery channel.'
    FRA='Sélectionnez les sections à inclure et le canal de transmission.'
    ITA='Seleziona le sezioni da includere e il canale di invio.'
  }
  'statistics.plantExport.dateFilter.title'    = [ordered]@{ SPA='📅 Filtro por fechas'; ENG='📅 Date filter'; FRA='📅 Filtre par date'; ITA='📅 Filtro per data' }
  'statistics.plantExport.dateFilter.subtitle' = [ordered]@{
    SPA='— afecta a intervenciones'
    ENG='— applies to interventions'
    FRA='— concerne les interventions'
    ITA='— riguarda gli interventi'
  }
  'statistics.plantExport.dateFilter.from'      = [ordered]@{ SPA='Desde';   ENG='From';   FRA='Du';     ITA='Da' }
  'statistics.plantExport.dateFilter.to'        = [ordered]@{ SPA='Hasta';   ENG='To';     FRA='Au';     ITA='A' }
  'statistics.plantExport.dateFilter.fromShort' = [ordered]@{ SPA='Inicio';  ENG='Start';  FRA='Début';  ITA='Inizio' }
  'statistics.plantExport.dateFilter.toShort'   = [ordered]@{ SPA='Fin';     ENG='End';    FRA='Fin';    ITA='Fine' }
  'statistics.plantExport.dateFilter.preset7d'  = [ordered]@{ SPA='7d';      ENG='7d';     FRA='7j';     ITA='7g' }
  'statistics.plantExport.dateFilter.preset30d' = [ordered]@{ SPA='30d';     ENG='30d';    FRA='30j';    ITA='30g' }
  'statistics.plantExport.dateFilter.preset90d' = [ordered]@{ SPA='90d';     ENG='90d';    FRA='90j';    ITA='90g' }
  'statistics.plantExport.dateFilter.preset1y'  = [ordered]@{ SPA='1 año';   ENG='1 year'; FRA='1 an';   ITA='1 anno' }
  'statistics.plantExport.dateFilter.clear'     = [ordered]@{ SPA='✕ limpiar'; ENG='✕ clear'; FRA='✕ effacer'; ITA='✕ pulisci' }

  'statistics.plantExport.section.summary.label' = [ordered]@{ SPA='📊 Resumen general'; ENG='📊 Overall summary'; FRA='📊 Résumé général'; ITA='📊 Riepilogo generale' }
  'statistics.plantExport.section.summary.sub'   = [ordered]@{
    SPA='KPIs globales: salud, ciclo de vida, mantenimiento, críticos, atención'
    ENG='Global KPIs: health, lifecycle, maintenance, critical, warnings'
    FRA='KPI globaux : santé, cycle de vie, maintenance, critiques, attention'
    ITA='KPI globali: salute, ciclo di vita, manutenzione, critici, attenzione'
  }
  'statistics.plantExport.section.life.label' = [ordered]@{ SPA='🔋 Ciclos de vida'; ENG='🔋 Lifecycles'; FRA='🔋 Cycles de vie'; ITA='🔋 Cicli di vita' }
  'statistics.plantExport.section.life.sub'   = [ordered]@{
    SPA='Listado de todos los elementos con vida útil y % restante'
    ENG='List of all elements with lifecycle and remaining %'
    FRA='Liste de tous les éléments avec durée de vie et % restant'
    ITA='Elenco di tutti gli elementi con vita utile e % rimanente'
  }
  'statistics.plantExport.section.maintenance.label' = [ordered]@{ SPA='🛠 Mantenimientos'; ENG='🛠 Maintenance'; FRA='🛠 Maintenances'; ITA='🛠 Manutenzioni' }
  'statistics.plantExport.section.maintenance.sub'   = [ordered]@{
    SPA='Listado de tareas de mantenimiento con % consumido'
    ENG='List of maintenance tasks with % consumed'
    FRA='Liste des tâches de maintenance avec % consommé'
    ITA='Elenco delle attività di manutenzione con % consumata'
  }
  'statistics.plantExport.section.interventions.label' = [ordered]@{ SPA='📋 Intervenciones recientes'; ENG='📋 Recent interventions'; FRA='📋 Interventions récentes'; ITA='📋 Interventi recenti' }
  'statistics.plantExport.section.interventions.sub'   = [ordered]@{
    SPA='Histórico de intervenciones por elemento (filtrado por fechas si aplica)'
    ENG='Intervention history per element (date-filtered if applicable)'
    FRA='Historique des interventions par élément (filtré par date si applicable)'
    ITA='Storico interventi per elemento (filtrato per data se applicabile)'
  }
  'statistics.plantExport.section.order.label' = [ordered]@{ SPA='🛒 Pedido sugerido'; ENG='🛒 Suggested order'; FRA='🛒 Commande suggérée'; ITA='🛒 Ordine suggerito' }
  'statistics.plantExport.section.order.sub'   = [ordered]@{
    SPA='Consumibles agregados de tareas no completadas (carga al exportar)'
    ENG='Consumables aggregated from incomplete tasks (loaded on export)'
    FRA='Consommables agrégés des tâches non terminées (chargés à l''export)'
    ITA='Consumabili aggregati da attività non completate (caricati all''export)'
  }

  'statistics.plantExport.info_note' = [ordered]@{
    SPA='ℹ El informe completo se imprime/exporta como PDF. El QR codifica un correo al soporte; si el contenido es muy largo, se recorta automáticamente.'
    ENG='ℹ The full report is printed/exported as PDF. The QR encodes a support email; if the content is too long, it is automatically trimmed.'
    FRA='ℹ Le rapport complet est imprimé/exporté en PDF. Le QR encode un e-mail au support ; si le contenu est trop long, il est automatiquement tronqué.'
    ITA='ℹ Il report completo viene stampato/esportato come PDF. Il QR codifica un''email al supporto; se il contenuto è troppo lungo, viene tagliato automaticamente.'
  }

  'statistics.plantExport.channels.title'     = [ordered]@{ SPA='Vía de envío'; ENG='Delivery channel'; FRA="Canal d'envoi"; ITA='Canale di invio' }
  'statistics.plantExport.channels.preparing' = [ordered]@{ SPA='— preparando…'; ENG='— preparing…'; FRA='— préparation…'; ITA='— preparazione…' }
  'statistics.plantExport.channels.print.label' = [ordered]@{ SPA='Imprimir / PDF'; ENG='Print / PDF'; FRA='Imprimer / PDF'; ITA='Stampa / PDF' }
  'statistics.plantExport.channels.print.sub'   = [ordered]@{ SPA='Diálogo navegador'; ENG='Browser dialog'; FRA='Dialogue navigateur'; ITA='Dialogo browser' }
  'statistics.plantExport.channels.qr.label'    = [ordered]@{ SPA='Generar QR'; ENG='Generate QR'; FRA='Générer QR'; ITA='Genera QR' }
  'statistics.plantExport.channels.qr.sub'      = [ordered]@{ SPA='Abre Mail en el móvil'; ENG='Opens Mail on the phone'; FRA='Ouvre Mail sur le mobile'; ITA='Apre Mail sul cellulare' }
  'statistics.plantExport.channels.file.label'  = [ordered]@{ SPA='Guardar archivo'; ENG='Save file'; FRA='Enregistrer le fichier'; ITA='Salva file' }
  'statistics.plantExport.channels.file.sub'    = [ordered]@{
    SPA='Disco · USB · Red — Pendiente backend'
    ENG='Disk · USB · Network — Backend pending'
    FRA='Disque · USB · Réseau — Backend en attente'
    ITA='Disco · USB · Rete — Backend in sospeso'
  }
  'statistics.plantExport.channels.email.label' = [ordered]@{ SPA='Enviar email'; ENG='Send email'; FRA='Envoyer un e-mail'; ITA='Invia email' }
  'statistics.plantExport.channels.email.sub'   = [ordered]@{ SPA='Pendiente gestión SMTP'; ENG='SMTP management pending'; FRA='Gestion SMTP en attente'; ITA='Gestione SMTP in sospeso' }

  'statistics.plantExport.kiosk_note_prefix' = [ordered]@{
    SPA='ℹ Sistema en kiosko: el operador no tiene acceso al SO.'
    ENG='ℹ Kiosk system: the operator has no OS access.'
    FRA="ℹ Système en mode kiosque : l'opérateur n'a pas accès au système d'exploitation."
    ITA="ℹ Sistema kiosk: l'operatore non ha accesso al SO."
  }
  'statistics.plantExport.kiosk_note_disabled_suffix' = [ordered]@{
    SPA='Los canales "Guardar archivo" y "Enviar email" están deshabilitados en la configuración del sistema (Excel SystemConfig).'
    ENG='The "Save file" and "Send email" channels are disabled in the system configuration (Excel SystemConfig).'
    FRA='Les canaux « Enregistrer le fichier » et « Envoyer un e-mail » sont désactivés dans la configuration système (Excel SystemConfig).'
    ITA='I canali "Salva file" e "Invia email" sono disabilitati nella configurazione di sistema (Excel SystemConfig).'
  }

  'statistics.plantExport.preview_title' = [ordered]@{
    SPA='Vista previa — Informe de planta'
    ENG='Preview — Plant report'
    FRA="Aperçu — Rapport d'usine"
    ITA="Anteprima — Report dell'impianto"
  }

  'statistics.plantExport.error.report' = [ordered]@{
    SPA='Error generando informe: '
    ENG='Error generating report: '
    FRA='Erreur de génération du rapport : '
    ITA='Errore generazione report: '
  }
  'statistics.plantExport.error.csv' = [ordered]@{
    SPA='Error generando CSV: '
    ENG='Error generating CSV: '
    FRA='Erreur de génération du CSV : '
    ITA='Errore generazione CSV: '
  }

  # ── GeneralHealthBar: botón EXPORTAR
  'statistics.generalHealth.exportButton'    = [ordered]@{ SPA='EXPORTAR'; ENG='EXPORT'; FRA='EXPORTER'; ITA='ESPORTA' }
  'statistics.generalHealth.exportButtonSub' = [ordered]@{ SPA='PDF · QR · …'; ENG='PDF · QR · …'; FRA='PDF · QR · …'; ITA='PDF · QR · …' }

  # ── PlantExportModal: contenido del informe (HTML imprimible + texto plano)
  'statistics.plantExport.report.title'      = [ordered]@{
    SPA='AQUAFRISCH SUPERVISOR — INFORME DE PLANTA'
    ENG='AQUAFRISCH SUPERVISOR — PLANT REPORT'
    FRA="AQUAFRISCH SUPERVISOR — RAPPORT D'USINE"
    ITA="AQUAFRISCH SUPERVISOR — REPORT DELL'IMPIANTO"
  }
  'statistics.plantExport.report.docTitle'   = [ordered]@{ SPA='Informe de planta'; ENG='Plant report'; FRA="Rapport d'usine"; ITA="Report dell'impianto" }
  'statistics.plantExport.report.h1'         = [ordered]@{ SPA='Informe de salud de planta'; ENG='Plant health report'; FRA="Rapport de santé de l'usine"; ITA="Report di salute dell'impianto" }
  'statistics.plantExport.report.subline'    = [ordered]@{ SPA='Aquafrisch Supervisor · Mantenimiento'; ENG='Aquafrisch Supervisor · Maintenance'; FRA='Aquafrisch Supervisor · Maintenance'; ITA='Aquafrisch Supervisor · Manutenzione' }
  'statistics.plantExport.report.generated'  = [ordered]@{ SPA='Generado'; ENG='Generated'; FRA='Généré'; ITA='Generato' }
  'statistics.plantExport.report.range'      = [ordered]@{ SPA='Rango';    ENG='Range';     FRA='Plage';  ITA='Intervallo' }
  'statistics.plantExport.report.summary_section'        = [ordered]@{ SPA='RESUMEN GENERAL';        ENG='OVERALL SUMMARY';      FRA='RÉSUMÉ GÉNÉRAL';       ITA='RIEPILOGO GENERALE' }
  'statistics.plantExport.report.lifecycles_section'     = [ordered]@{ SPA='CICLOS DE VIDA';         ENG='LIFECYCLES';           FRA='CYCLES DE VIE';        ITA='CICLI DI VITA' }
  'statistics.plantExport.report.maintenance_section'    = [ordered]@{ SPA='MANTENIMIENTOS';         ENG='MAINTENANCE';          FRA='MAINTENANCES';         ITA='MANUTENZIONI' }
  'statistics.plantExport.report.interventions_section'  = [ordered]@{ SPA='INTERVENCIONES RECIENTES'; ENG='RECENT INTERVENTIONS'; FRA='INTERVENTIONS RÉCENTES'; ITA='INTERVENTI RECENTI' }
  'statistics.plantExport.report.order_section'          = [ordered]@{ SPA='PEDIDO SUGERIDO';        ENG='SUGGESTED ORDER';      FRA='COMMANDE SUGGÉRÉE';    ITA='ORDINE SUGGERITO' }
  'statistics.plantExport.report.global_health' = [ordered]@{ SPA='Salud global';   ENG='Overall health'; FRA='Santé globale'; ITA='Salute globale' }
  'statistics.plantExport.report.lifecycle'     = [ordered]@{ SPA='Ciclo de vida';  ENG='Lifecycle';      FRA='Cycle de vie';  ITA='Ciclo di vita' }
  'statistics.plantExport.report.maintenance'   = [ordered]@{ SPA='Mantenimiento';  ENG='Maintenance';    FRA='Maintenance';   ITA='Manutenzione' }
  'statistics.plantExport.report.critical'      = [ordered]@{ SPA='Criticos';       ENG='Critical';       FRA='Critiques';     ITA='Critici' }
  'statistics.plantExport.report.warning'       = [ordered]@{ SPA='Atencion';       ENG='Warning';        FRA='Attention';     ITA='Attenzione' }
  'statistics.plantExport.report.total_metrics' = [ordered]@{ SPA='Total metricas'; ENG='Total metrics';  FRA='Total métriques'; ITA='Totale metriche' }
  'statistics.plantExport.report.elements_lc'   = [ordered]@{ SPA='elementos';      ENG='elements';       FRA='éléments';      ITA='elementi' }
  'statistics.plantExport.report.lines_lc'      = [ordered]@{ SPA='líneas';         ENG='lines';          FRA='lignes';        ITA='righe' }
  'statistics.plantExport.report.unit_default'  = [ordered]@{ SPA='ud';             ENG='un';             FRA='u';             ITA='u' }
  'statistics.plantExport.report.footer'        = [ordered]@{
    SPA='Generado desde Aquafrisch Supervisor'
    ENG='Generated from Aquafrisch Supervisor'
    FRA='Généré depuis Aquafrisch Supervisor'
    ITA='Generato da Aquafrisch Supervisor'
  }
  'statistics.plantExport.report.htmlFooter'    = [ordered]@{
    SPA='Documento generado automáticamente por Aquafrisch Supervisor — Módulo SMM.'
    ENG='Document automatically generated by Aquafrisch Supervisor — SMM module.'
    FRA='Document généré automatiquement par Aquafrisch Supervisor — Module SMM.'
    ITA='Documento generato automaticamente da Aquafrisch Supervisor — Modulo SMM.'
  }
  'statistics.plantExport.report.printButton'   = [ordered]@{
    SPA='🖨 Imprimir / Guardar como PDF'
    ENG='🖨 Print / Save as PDF'
    FRA='🖨 Imprimer / Enregistrer en PDF'
    ITA='🖨 Stampa / Salva come PDF'
  }
  'statistics.plantExport.report.no_data'          = [ordered]@{ SPA='No hay datos.'; ENG='No data.'; FRA='Aucune donnée.'; ITA='Nessun dato.' }
  'statistics.plantExport.report.no_interventions' = [ordered]@{ SPA='No hay intervenciones.'; ENG='No interventions.'; FRA='Aucune intervention.'; ITA='Nessun intervento.' }
  'statistics.plantExport.report.no_order_lines'   = [ordered]@{ SPA='No hay líneas sugeridas.'; ENG='No suggested lines.'; FRA='Aucune ligne suggérée.'; ITA='Nessuna riga suggerita.' }

  # Tabla — cabeceras del informe
  'statistics.plantExport.report.col.health'      = [ordered]@{ SPA='Salud';      ENG='Health';      FRA='Santé';       ITA='Salute' }
  'statistics.plantExport.report.col.element'     = [ordered]@{ SPA='Elemento';   ENG='Element';     FRA='Élément';     ITA='Elemento' }
  'statistics.plantExport.report.col.parent'      = [ordered]@{ SPA='Padre';      ENG='Parent';      FRA='Parent';      ITA='Padre' }
  'statistics.plantExport.report.col.variable'    = [ordered]@{ SPA='Variable';   ENG='Variable';    FRA='Variable';    ITA='Variabile' }
  'statistics.plantExport.report.col.task'        = [ordered]@{ SPA='Tarea';      ENG='Task';        FRA='Tâche';       ITA='Attività' }
  'statistics.plantExport.report.col.consumed'    = [ordered]@{ SPA='Consumido';  ENG='Consumed';    FRA='Consommé';    ITA='Consumato' }
  'statistics.plantExport.report.col.critical'    = [ordered]@{ SPA='Crítico';    ENG='Critical';    FRA='Critique';    ITA='Critico' }
  'statistics.plantExport.report.col.unit'        = [ordered]@{ SPA='Unidad';     ENG='Unit';        FRA='Unité';       ITA='Unità' }
  'statistics.plantExport.report.col.date'        = [ordered]@{ SPA='Fecha';      ENG='Date';        FRA='Date';        ITA='Data' }
  'statistics.plantExport.report.col.type'        = [ordered]@{ SPA='Tipo';       ENG='Type';        FRA='Type';        ITA='Tipo' }
  'statistics.plantExport.report.col.operator'    = [ordered]@{ SPA='Operador';   ENG='Operator';    FRA='Opérateur';   ITA='Operatore' }
  'statistics.plantExport.report.col.notes'       = [ordered]@{ SPA='Notas';      ENG='Notes';       FRA='Notes';       ITA='Note' }
  'statistics.plantExport.report.col.sku'         = [ordered]@{ SPA='SKU';        ENG='SKU';         FRA='SKU';         ITA='SKU' }
  'statistics.plantExport.report.col.description' = [ordered]@{ SPA='Descripción'; ENG='Description'; FRA='Description'; ITA='Descrizione' }
  'statistics.plantExport.report.col.quantity'    = [ordered]@{ SPA='Cantidad';   ENG='Quantity';    FRA='Quantité';    ITA='Quantità' }
  'statistics.plantExport.report.col.elements'    = [ordered]@{ SPA='Elementos';  ENG='Elements';    FRA='Éléments';    ITA='Elementi' }

  # KPIs del bloque de resumen del informe HTML
  'statistics.plantExport.report.kpi.global'        = [ordered]@{ SPA='Salud global';   ENG='Overall health'; FRA='Santé globale';   ITA='Salute globale' }
  'statistics.plantExport.report.kpi.lifecycle'     = [ordered]@{ SPA='Ciclo de vida';  ENG='Lifecycle';      FRA='Cycle de vie';    ITA='Ciclo di vita' }
  'statistics.plantExport.report.kpi.maintenance'   = [ordered]@{ SPA='Mantenimiento';  ENG='Maintenance';    FRA='Maintenance';     ITA='Manutenzione' }
  'statistics.plantExport.report.kpi.critical'      = [ordered]@{ SPA='Críticos';       ENG='Critical';       FRA='Critiques';       ITA='Critici' }
  'statistics.plantExport.report.kpi.warning'       = [ordered]@{ SPA='Atención';       ENG='Warning';        FRA='Attention';       ITA='Attenzione' }
  'statistics.plantExport.report.kpi.total'         = [ordered]@{ SPA='Total métricas'; ENG='Total metrics';  FRA='Total métriques'; ITA='Totale metriche' }
  'statistics.plantExport.report.kpi.elementsShort' = [ordered]@{ SPA='elem.';          ENG='elem.';          FRA='élém.';           ITA='elem.' }

  # Secciones del informe HTML (con contador entre paréntesis)
  'statistics.plantExport.report.section.summary'       = [ordered]@{ SPA='Resumen general';            ENG='Overall summary';            FRA='Résumé général';                ITA='Riepilogo generale' }
  'statistics.plantExport.report.section.life'          = [ordered]@{ SPA='Ciclos de vida ({{count}})'; ENG='Lifecycles ({{count}})';     FRA='Cycles de vie ({{count}})';     ITA='Cicli di vita ({{count}})' }
  'statistics.plantExport.report.section.maintenance'   = [ordered]@{ SPA='Mantenimientos ({{count}})'; ENG='Maintenance ({{count}})';    FRA='Maintenances ({{count}})';      ITA='Manutenzioni ({{count}})' }
  'statistics.plantExport.report.section.interventions' = [ordered]@{ SPA='Intervenciones recientes ({{count}})'; ENG='Recent interventions ({{count}})'; FRA='Interventions récentes ({{count}})'; ITA='Interventi recenti ({{count}})' }
  'statistics.plantExport.report.section.order'         = [ordered]@{ SPA='Pedido sugerido ({{count}} líneas)';   ENG='Suggested order ({{count}} lines)'; FRA='Commande suggérée ({{count}} lignes)'; ITA='Ordine suggerito ({{count}} righe)' }
}

$files = Get-ChildItem -Path (Join-Path $Root 'Projects') -Recurse -Filter 'translations.json'
foreach ($f in $files) {
  $raw  = Get-Content -Raw -Encoding UTF8 $f.FullName
  $json = $raw | ConvertFrom-Json
  $changed = $false

  if ($json.PSObject.Properties.Name -contains 'keys' -and $json.keys) {
    $list = New-Object System.Collections.Generic.List[string]
    foreach ($k in $json.keys) { [void]$list.Add([string]$k) }
    foreach ($k in $newKeys.Keys) {
      if (-not $list.Contains($k)) { [void]$list.Add($k); $changed = $true }
    }
    $json.keys = $list.ToArray()
  }

  $tNode = $null
  foreach ($cand in 'translations','strings','values') {
    if ($json.PSObject.Properties.Name -contains $cand) { $tNode = $json.$cand; break }
  }
  if ($null -eq $tNode) { $tNode = $json }

  foreach ($k in $newKeys.Keys) {
    $newVal = [pscustomobject]$newKeys[$k]
    if (-not ($tNode.PSObject.Properties.Name -contains $k)) {
      $tNode | Add-Member -NotePropertyName $k -NotePropertyValue $newVal -Force
      $changed = $true
    } else {
      # Sobrescribe siempre (permite ajustar textos en re-ejecuciones).
      $tNode.$k = $newVal
      $changed = $true
    }
  }

  if ($changed) {
    $out = $json | ConvertTo-Json -Depth 50
    [IO.File]::WriteAllText($f.FullName, $out, [Text.UTF8Encoding]::new($false))
    Write-Host "OK  $($f.FullName)"
  } else {
    Write-Host "--  $($f.FullName) (sin cambios)"
  }
}
