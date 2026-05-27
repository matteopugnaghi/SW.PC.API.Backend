# ============================================================================
# add-cra-export-descriptions.ps1
# ----------------------------------------------------------------------------
# Añade traducciones (SPA/ENG/FRA/ITA) para los datasets de exportación CRA
# del panel "Software Integrity" (InfoPanel.expanded.export_*).
# ============================================================================

$ErrorActionPreference = 'Stop'

$additions = [ordered]@{
    # ---- SBOM ---------------------------------------------------------------
    'infoPanel.expanded.export_sbom_dataset' = @{
        SPA = 'SBOM (componentes)'
        ENG = 'SBOM (components)'
        FRA = 'SBOM (composants)'
        ITA = 'SBOM (componenti)'
    }
    'infoPanel.expanded.export_sbom_desc' = @{
        SPA = 'SBOM (Software Bill of Materials): inventario completo de todos los componentes y librerías de terceros que integran la aplicación, con su versión y licencia. Documento exigido por la EU CRA para demostrar trazabilidad de dependencias y permitir auditorías de vulnerabilidades conocidas (CVE).'
        ENG = 'SBOM (Software Bill of Materials): complete inventory of every third-party component and library bundled with the application, including version and licence. Required by the EU CRA to prove dependency traceability and enable audits of known vulnerabilities (CVEs).'
        FRA = 'SBOM (Software Bill of Materials) : inventaire complet de tous les composants et bibliothèques tiers intégrés à l''application, avec version et licence. Document exigé par l''EU CRA pour démontrer la traçabilité des dépendances et permettre l''audit des vulnérabilités connues (CVE).'
        ITA = 'SBOM (Software Bill of Materials): inventario completo di tutti i componenti e librerie di terze parti integrati nell''applicazione, con versione e licenza. Documento richiesto dall''EU CRA per dimostrare la tracciabilità delle dipendenze e consentire audit delle vulnerabilità note (CVE).'
    }

    # ---- Certificado de integridad ------------------------------------------
    'infoPanel.expanded.export_cert_dataset' = @{
        SPA = 'Certificado de integridad'
        ENG = 'Integrity certificate'
        FRA = 'Certificat d''intégrité'
        ITA = 'Certificato di integrità'
    }
    'infoPanel.expanded.export_cert_desc' = @{
        SPA = 'Certificado de integridad EU CRA — estado actual de los componentes desplegados (versión, commit, hash, firma digital y ficheros modificados). Sirve como evidencia de que el software ejecutándose en la máquina es el mismo que se firmó y entregó, sin manipulación posterior.'
        ENG = 'EU CRA integrity certificate — current state of the deployed components (version, commit, hash, digital signature and modified files). Provides evidence that the software running on the machine is the same one that was signed and delivered, with no later tampering.'
        FRA = 'Certificat d''intégrité EU CRA — état actuel des composants déployés (version, commit, hash, signature numérique et fichiers modifiés). Preuve que le logiciel exécuté sur la machine est bien celui qui a été signé et livré, sans manipulation ultérieure.'
        ITA = 'Certificato di integrità EU CRA — stato attuale dei componenti distribuiti (versione, commit, hash, firma digitale e file modificati). Costituisce la prova che il software in esecuzione sulla macchina è lo stesso firmato e consegnato, senza manomissioni successive.'
    }

    # ---- Certificados de despliegue -----------------------------------------
    'infoPanel.expanded.export_deployment_dataset' = @{
        SPA = 'Certificados de despliegue (EU CRA)'
        ENG = 'Deployment certificates (EU CRA)'
        FRA = 'Certificats de déploiement (EU CRA)'
        ITA = 'Certificati di distribuzione (EU CRA)'
    }
    'infoPanel.expanded.export_deployment_desc' = @{
        SPA = 'Historial de despliegues: cada push o commit hacia esta máquina genera automáticamente un registro con fecha, operador, commit y hash de integridad. Constituye la pista de auditoría de actualizaciones requerida por la EU CRA (quién actualizó qué, cuándo y con qué versión).'
        ENG = 'Deployment history: every push or commit to this machine automatically produces a record with date, operator, commit and integrity hash. Provides the update audit trail required by the EU CRA (who updated what, when and with which version).'
        FRA = 'Historique des déploiements : chaque push ou commit vers cette machine génère automatiquement un enregistrement avec date, opérateur, commit et hash d''intégrité. Constitue la piste d''audit des mises à jour exigée par l''EU CRA (qui a mis à jour quoi, quand et avec quelle version).'
        ITA = 'Cronologia delle distribuzioni: ogni push o commit verso questa macchina genera automaticamente un record con data, operatore, commit e hash di integrità. Costituisce la pista di audit degli aggiornamenti richiesta dall''EU CRA (chi ha aggiornato cosa, quando e con quale versione).'
    }
    'infoPanel.expanded.export_deployment_empty' = @{
        SPA = 'Aún no se ha registrado ningún despliegue en esta máquina. El primer registro aparecerá automáticamente al ejecutar el primer push/commit firmado contra el backend.'
        ENG = 'No deployment has been recorded on this machine yet. The first entry will appear automatically when the first signed push/commit reaches the backend.'
        FRA = 'Aucun déploiement n''a encore été enregistré sur cette machine. La première entrée apparaîtra automatiquement dès le premier push/commit signé envoyé au backend.'
        ITA = 'Nessuna distribuzione è ancora stata registrata su questa macchina. La prima voce comparirà automaticamente con il primo push/commit firmato inviato al backend.'
    }

    # ---- Claves SSH autorizadas ---------------------------------------------
    'infoPanel.expanded.export_signingkeys_dataset' = @{
        SPA = 'Claves SSH autorizadas para firmar'
        ENG = 'SSH keys authorized for signing'
        FRA = 'Clés SSH autorisées pour la signature'
        ITA = 'Chiavi SSH autorizzate per la firma'
    }
    'infoPanel.expanded.export_signingkeys_desc' = @{
        SPA = 'Lista de claves SSH públicas autorizadas para firmar despliegues y commits (sólo clave pública — nunca privada). Identifica qué máquinas u operadores tienen permiso para introducir cambios firmados, requisito de control de acceso de la EU CRA.'
        ENG = 'List of public SSH keys authorized to sign deployments and commits (public key only — never private). Identifies which machines or operators are allowed to introduce signed changes, an access-control requirement of the EU CRA.'
        FRA = 'Liste des clés SSH publiques autorisées à signer les déploiements et commits (clé publique uniquement — jamais privée). Identifie les machines ou opérateurs autorisés à introduire des modifications signées, exigence de contrôle d''accès de l''EU CRA.'
        ITA = 'Elenco delle chiavi SSH pubbliche autorizzate a firmare distribuzioni e commit (solo chiave pubblica — mai privata). Identifica quali macchine od operatori possono introdurre modifiche firmate, requisito di controllo accessi dell''EU CRA.'
    }

    # ---- Certificado SSL ----------------------------------------------------
    'infoPanel.expanded.export_sslcert_dataset' = @{
        SPA = 'Certificado SSL/HTTPS del servidor'
        ENG = 'Server SSL/HTTPS certificate'
        FRA = 'Certificat SSL/HTTPS du serveur'
        ITA = 'Certificato SSL/HTTPS del server'
    }
    'infoPanel.expanded.export_sslcert_desc' = @{
        SPA = 'Certificado X.509 del servidor HTTPS (asunto, emisor, validez, huella SHA-256, algoritmo y SAN). Evidencia que la comunicación entre cliente y backend está cifrada con TLS, cumpliendo el requisito EU CRA de "seguridad en tránsito".'
        ENG = 'Server HTTPS X.509 certificate (subject, issuer, validity, SHA-256 fingerprint, algorithm and SAN). Evidence that the client-backend channel is encrypted with TLS, meeting the EU CRA "security in transit" requirement.'
        FRA = 'Certificat X.509 du serveur HTTPS (sujet, émetteur, validité, empreinte SHA-256, algorithme et SAN). Preuve que la communication entre le client et le backend est chiffrée en TLS, conformément à l''exigence EU CRA de « sécurité en transit ».'
        ITA = 'Certificato X.509 del server HTTPS (soggetto, emittente, validità, impronta SHA-256, algoritmo e SAN). Dimostra che la comunicazione tra client e backend è cifrata con TLS, soddisfacendo il requisito EU CRA di "sicurezza in transito".'
    }
}

# ============================================================================
# Aplicar a todos los Projects/*/translations/translations.json
# ============================================================================
$utf8NoBom    = New-Object System.Text.UTF8Encoding($false)
$projectsRoot = Join-Path $PSScriptRoot '..\Projects'
$files = Get-ChildItem -Path $projectsRoot -Recurse -Filter 'translations.json' |
         Where-Object { $_.FullName -match '\\translations\\translations\.json$' }

foreach ($file in $files) {
    Write-Host ""
    Write-Host "→ $($file.FullName)" -ForegroundColor Cyan

    $raw  = [System.IO.File]::ReadAllText($file.FullName, $utf8NoBom)
    $json = $raw | ConvertFrom-Json

    $added = 0
    $replaced = 0

    foreach ($key in $additions.Keys) {
        $entry = $additions[$key]
        $obj = [ordered]@{
            SPA = $entry.SPA
            ENG = $entry.ENG
            FRA = $entry.FRA
            ITA = $entry.ITA
        }
        if ($json.translations.PSObject.Properties.Name.Contains($key)) {
            $json.translations.$key = [pscustomobject]$obj
            $replaced++
        } else {
            $json.translations | Add-Member -NotePropertyName $key -NotePropertyValue ([pscustomobject]$obj)
            $added++
        }
    }

    $json.metadata.lastModified = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

    Copy-Item -Path $file.FullName -Destination "$($file.FullName).bak" -Force
    $out = $json | ConvertTo-Json -Depth 100
    [System.IO.File]::WriteAllText($file.FullName, $out, $utf8NoBom)
    Write-Host "   ✓ añadidas $added · sustituidas $replaced" -ForegroundColor Green
}

Write-Host ""
Write-Host "Hecho." -ForegroundColor Green
