# Especificación de Integración / Integrationsspezifikation
**— Aquafrisch Supervisor ↔ RhB IT**

**Entra ID (SSO) · Cumplimiento RhB IT Standards · Integración Modbus**
***Entra ID (SSO) · Konformität mit den RhB IT Standards · Modbus-Integration***

> Documento de especificación e integración para el cliente Rhätische Bahn (RhB), proyecto *Drehgestell-Waschhalle Landquart*. Sirve además como evidencia de diseño de seguridad para EU CRA e IEC 62443 (identidad, autenticación y control de uso).
> *Spezifikations- und Integrationsdokument für den Kunden Rhätische Bahn (RhB), Projekt Drehgestell-Waschhalle Landquart. Dient zugleich als Nachweis des Sicherheitsdesigns für EU CRA und IEC 62443 (Identität, Authentifizierung und Nutzungskontrolle).*

<!-- INTERNO (no exportar al cliente): documentos hermanos correo-rhb.md (transmisión + seguimiento), SKILL.md (decisiones), gap-analysis-rhb.md. -->

---

## 0. Control del documento / Dokumentenlenkung

| Campo / Feld | Valor / Wert |
|---|---|
| Código del documento / Dokument-Code | **06.7-C07-20** |
| Título / Titel | Especificación_de_Integración_RhB_IT_Entra_ID_Cumplimiento_Modbus |
| Versión / Version | 0.4 (borrador bilingüe / zweisprachiger Entwurf) |
| Estado / Status | 🟡 Borrador, no enviado / Entwurf, nicht versendet |
| Autor / Autor | Pugnaghi Matteo — Aquafrisch S.L.U. |
| Fecha / Datum | 2026-06-27 |
| Cliente — Proyecto / Kunde — Projekt | Rhätische Bahn (RhB) — Drehgestell-Waschhalle Landquart |
| Idioma / Sprache | Español + Deutsch (frase a frase / Satz für Satz) |
| Clasificación / Klassifizierung | Confidencial / Vertraulich |
| Referencia cliente / Kundenreferenz | RhB IT Standards v9.0.4 |

### Histórico de versiones / Versionshistorie
| Versión / Version | Fecha / Datum | Cambios / Änderungen |
|---|---|---|
| 0.1 | 2026-06-27 | Versión inicial (Entra ID + Cumplimiento + Modbus). / Erstfassung (Entra ID + Konformität + Modbus). |
| 0.2 | 2026-06-27 | Código y título; limpieza de redacción; ajuste del Plan. / Code und Titel; redaktionelle Bereinigung; Anpassung des Plans. |
| 0.3 | 2026-06-27 | Versión bilingüe (ES/DE). / Zweisprachige Fassung (ES/DE). |
| 0.4 | 2026-06-27 | Ajustes de redacción: autor; capítulos A/B/C; protocolo ADS de Beckhoff; modo kiosko; voz directa en las peticiones; hosting como hecho; sin columna «Firma»; uniformidad A.2.8; clarificaciones (interfaz Modbus, kiosko en 8.1). / Redaktionelle Anpassungen: Autor; Kapitel A/B/C; ADS-Protokoll von Beckhoff; Kiosk-Modus; direkte Ansprache; Hosting als Faktum; ohne Spalte «Unterschrift»; Vereinheitlichung A.2.8; Klarstellungen (Modbus-Schnittstelle, Kiosk in 8.1). |

---

## 1. Propósito y alcance / Zweck und Geltungsbereich

Estamos adaptando Aquafrisch Supervisor a los RhB IT Standards v9.0.4.
*Wir passen den Aquafrisch Supervisor an die RhB IT Standards v9.0.4 an.*

Este documento describe el contexto y la arquitectura del sistema (sección 4) y se estructura en tres capítulos: el Capítulo A especifica el diseño de autenticación SSO con Microsoft Entra ID y los datos que necesitamos de RhB; el Capítulo B recoge los demás puntos de cumplimiento de los RhB IT Standards; y el Capítulo C plantea la integración Modbus.
*Dieses Dokument beschreibt Kontext und Architektur des Systems (Abschnitt 4) und gliedert sich in drei Kapitel: Kapitel A spezifiziert das SSO-Authentifizierungsdesign mit Microsoft Entra ID und die von RhB benötigten Angaben; Kapitel B erfasst die übrigen Konformitätspunkte der RhB IT Standards; und Kapitel C skizziert die Modbus-Integration.*

Los puntos abiertos que requieren confirmación o información de RhB están marcados a lo largo del documento y consolidados en el capítulo 8.
*Die offenen Punkte, die eine Bestätigung oder Angaben von RhB erfordern, sind im gesamten Dokument gekennzeichnet und in Kapitel 8 zusammengefasst.*

> **Estructura:** secciones numeradas 0–4 (introducción) y 5–8 (entorno de pruebas, trazabilidad, plan y formulario de respuesta), con tres capítulos temáticos A, B y C (Entra ID, Cumplimiento y Modbus) como cuerpo central.
> ***Aufbau:** nummerierte Abschnitte 0–4 (Einführung) und 5–8 (Testumgebung, Nachverfolgbarkeit, Plan und Antwortformular), mit drei thematischen Kapiteln A, B und C (Entra ID, Konformität und Modbus) als Hauptteil.*

---

## 2. Referencias normativas / Normative Referenzen

- RhB IT Standards v9.0.4 (Rhätische Bahn) — requisitos técnicos y operativos. / *RhB IT Standards v9.0.4 (Rhätische Bahn) — technische und betriebliche Anforderungen.*
- EU Cyber Resilience Act (CRA) — Anexo I (requisitos esenciales de ciberseguridad), Anexo VII (documentación técnica). / *EU Cyber Resilience Act (CRA) — Anhang I (wesentliche Cybersicherheitsanforderungen), Anhang VII (technische Dokumentation).*
- IEC 62443-4-1 — Ciclo de vida de desarrollo seguro de productos. / *IEC 62443-4-1 — Sicherer Produktentwicklungslebenszyklus.*
- IEC 62443-4-2 / 62443-3-3 — Requisitos técnicos de seguridad (FR1 Identification & Authentication Control, FR2 Use Control). / *IEC 62443-4-2 / 62443-3-3 — Technische Sicherheitsanforderungen (FR1 Identification & Authentication Control, FR2 Use Control).*
- Microsoft Entra ID — OpenID Connect, OAuth 2.0, MSAL, Microsoft Graph. / *Microsoft Entra ID — OpenID Connect, OAuth 2.0, MSAL, Microsoft Graph.*

---

## 3. Definiciones y glosario / Definitionen und Glossar

| Término / Begriff | Significado / Bedeutung |
|---|---|
| Entra ID | Proveedor de identidad en la nube de Microsoft (antes Azure AD). / *Cloud-Identitätsanbieter von Microsoft (früher Azure AD).* |
| OIDC / OpenID Connect | Protocolo estándar de inicio de sesión web sobre OAuth 2.0. / *Standardprotokoll für die Web-Anmeldung auf Basis von OAuth 2.0.* |
| Authorization Code + PKCE | Flujo OIDC seguro recomendado para aplicaciones de navegador (SPA). / *Empfohlener sicherer OIDC-Ablauf für Browser-Anwendungen (SPA).* |
| MFA | Autenticación multifactor (un segundo factor además de la contraseña). / *Mehrstufige Authentifizierung (ein zweiter Faktor zusätzlich zum Passwort).* |
| Break-glass | Cuenta local de emergencia que permite operar sin conexión al IDP. / *Lokales Notfallkonto, das den Betrieb ohne Verbindung zum IDP ermöglicht.* |
| RBAC | Control de acceso basado en roles. / *Rollenbasierte Zugriffskontrolle.* |
| SPA | Single-Page Application (aplicación web que corre en el navegador). / *Single-Page Application (Web-Anwendung, die im Browser läuft).* |
| object ID | Identificador inmutable de un usuario en Entra. / *Unveränderliche Kennung eines Benutzers in Entra.* |
| UNC | Ruta de red Windows (`\\servidor\carpeta`). / *Windows-Netzwerkpfad (`\\Server\Ordner`).* |
| Modbus server/client | Servidor/esclavo expone registros; cliente/maestro lee/escribe registros de otros. / *Server/Slave stellt Register bereit; Client/Master liest/schreibt Register anderer.* |

---

## 4. Contexto del sistema y arquitectura / Systemkontext und Architektur

Aquafrisch Supervisor es una aplicación web (SCADA/HMI) que supervisa y controla la instalación de lavado.
*Der Aquafrisch Supervisor ist eine Web-Anwendung (SCADA/HMI), die die Waschanlage überwacht und steuert.*

Funciona en un PC industrial (IPC) con pantalla táctil en la máquina, y puede accederse en remoto desde equipos de RhB mediante navegador.
*Er läuft auf einem Industrie-PC (IPC) mit Touchscreen an der Anlage und ist zudem aus der Ferne über den Browser von RhB-Geräten aus erreichbar.*

Arquitectura dual: el PLC y su control funcionan en su propio entorno; el backend del Supervisor (servidor web) funciona en un entorno separado y se comunica con el PLC a través del protocolo ADS de Beckhoff (control de máquina y supervisión desacoplados).
*Duale Architektur: Die SPS und ihre Steuerung laufen in ihrer eigenen Umgebung; das Backend des Supervisors (Webserver) läuft in einer separaten Umgebung und kommuniziert mit der SPS über das ADS-Protokoll von Beckhoff (Anlagensteuerung und Überwachung entkoppelt).*

Red del IPC (según el plano eléctrico del proyecto): el IPC dispone de dos interfaces.
*Netzwerk des IPC (gemäss Elektroschema des Projekts): Der IPC verfügt über zwei Schnittstellen.*

- LAN1 → red de RhB (IP, máscara, gateway y DNS los asigna RhB). Por aquí van el acceso de los clientes y la salida hacia Entra. / *LAN1 → RhB-Netzwerk (IP, Subnetzmaske, Gateway und DNS werden von RhB festgelegt). Hierüber laufen der Client-Zugriff und der Ausgang zu Entra.*
- LAN2 → enlace dedicado y aislado al PLC (punto a punto, `192.168.1.162/30` ↔ PLC `192.168.1.161`, no expuesto). / *LAN2 → dedizierte und isolierte Verbindung zur SPS (Punkt-zu-Punkt, `192.168.1.162/30` ↔ SPS `192.168.1.161`, nicht exponiert).*

---

## Capítulo A — Autenticación SSO con Microsoft Entra ID / Kapitel A — SSO-Authentifizierung mit Microsoft Entra ID

### A.1 Planteamiento / Lösungsansatz

- Inicio de sesión vía Entra ID: los usuarios iniciarán sesión en la aplicación con su identidad de RhB (Entra ID), mediante OpenID Connect (Authorization Code + PKCE). La aplicación recibe un token firmado con su identidad y su rol; no gestionamos ni almacenamos su contraseña. / *Anmeldung über Entra ID: Die Benutzer melden sich mit ihrer RhB-Identität (Entra ID) über OpenID Connect (Authorization Code + PKCE) an der Anwendung an. Die Anwendung erhält ein signiertes Token mit Identität und Rolle; wir verwalten oder speichern das Passwort nicht.*
- Roles: el Supervisor tiene 5 roles — Administrador, Mantenimiento, Operador, Visor, Auditor. / *Rollen: Der Supervisor hat 5 Rollen — Administrator, Wartung, Bediener, Betrachter, Auditor.*
- Acceso de emergencia (break-glass): la máquina debe seguir operando aunque no haya Internet o Entra no esté disponible; por eso el Supervisor conserva una cuenta de administrador local que funciona sin conexión, como respaldo. / *Notfallzugang (Break-glass): Die Anlage muss weiter betrieben werden können, auch ohne Internet oder wenn Entra nicht verfügbar ist; daher behält der Supervisor ein lokales Administratorkonto, das offline funktioniert, als Rückfallebene.*
- Contraseña: el usuario siempre introduce contraseña (IPC compartido por turnos). No se usan métodos sin contraseña. / *Passwort: Der Benutzer gibt immer ein Passwort ein (IPC wird schichtweise gemeinsam genutzt). Es werden keine passwortlosen Verfahren verwendet.*

### A.2 Puntos a confirmar / información requerida de RhB / Zu bestätigende Punkte / von RhB benötigte Angaben

> Los puntos marcados «↳ responder en el formulario 8.x» requieren respuesta de RhB (capítulo 8). Los marcados «informativo» son solo para su conocimiento.
> *Die mit «↳ im Formular 8.x beantworten» markierten Punkte erfordern eine Antwort von RhB (Kapitel 8). Die mit «informativ» markierten dienen nur zur Kenntnisnahme.*

1. *(↳ responder en el formulario 8.1 / im Formular 8.1 beantworten)* **Alcance del inicio de sesión:** Entra ID cubrirá el acceso a la aplicación Aquafrisch. El inicio de sesión de Windows del IPC funciona en modo kiosko: el equipo arranca con una cuenta local anónima y sin contraseña que lanza directamente la aplicación a pantalla completa, sin escritorio ni acceso al sistema operativo; proponemos mantenerlo como hoy. La identificación de cada persona se realiza al entrar en la aplicación. ¿De acuerdo con este alcance?
   *(↳ im Formular 8.1 beantworten)* ***Geltungsbereich der Anmeldung:** Entra ID deckt den Zugang zur Aquafrisch-Anwendung ab. Die Windows-Anmeldung des IPC läuft im Kiosk-Modus: Das Gerät startet mit einem anonymen lokalen Konto ohne Passwort, das die Anwendung direkt im Vollbild startet, ohne Desktop oder Zugriff auf das Betriebssystem; wir schlagen vor, dies wie bisher beizubehalten. Die Identifizierung jeder Person erfolgt beim Einstieg in die Anwendung. Sind Sie mit diesem Geltungsbereich einverstanden?*
2. *(↳ responder en el formulario 8.2 / im Formular 8.2 beantworten)* **Conectividad de red (LAN1):** (a) salida del IPC hacia Entra (`login.microsoftonline.com`, `*.msftauth.net`, y `graph.microsoft.com` si consultamos el perfil): ¿directa, vía proxy o aislada?; (b) entrada de los clientes al IPC por HTTPS: ¿misma red/VLAN, ruteado con firewall o vía Citrix?
   ***Netzwerkanbindung (LAN1):** (a) Ausgang des IPC zu Entra (`login.microsoftonline.com`, `*.msftauth.net`, und `graph.microsoft.com`, falls wir das Profil abfragen): direkt, über Proxy oder isoliert?; (b) Eingang der Clients zum IPC über HTTPS: gleiches Netz/VLAN, geroutet mit Firewall oder über Citrix?*
3. *(↳ responder en el formulario 8.3 / im Formular 8.3 beantworten)* **Registro de la aplicación (App Registration):** deben registrar la aplicación en su tenant y facilitarnos (a) Directory (tenant) ID; (b) Application (client) ID; (c) Redirect URIs. El tipo es SPA que además expone una API. Indíquennos si se accede por Citrix.
   ***Anwendungsregistrierung (App Registration):** Bitte registrieren Sie die Anwendung in Ihrem Tenant und stellen Sie uns (a) Directory-(Tenant-)ID; (b) Application-(Client-)ID; (c) Redirect-URIs bereit. Der Typ ist SPA, die zusätzlich eine API bereitstellt. Bitte teilen Sie uns mit, ob der Zugriff über Citrix erfolgt.*
4. *(informativo / informativ)* **Permisos sobre el directorio (mínimos):** solo `User.Read` (perfil básico del usuario que entra). No se piden permisos de escritura ni globales, ni acceso a recursos de Azure.
   ***Verzeichnisberechtigungen (minimal):** nur `User.Read` (Basisprofil des sich anmeldenden Benutzers). Es werden keine Schreib- oder globalen Berechtigungen und kein Zugriff auf Azure-Ressourcen angefordert.*
5. *(↳ responder en el formulario 8.4 / im Formular 8.4 beantworten)* **Grupos/roles de Entra:** definan los grupos para los 5 roles. Ideal: mismos nombres que nuestros roles (1:1); si no, facilítennos sus nombres/GUID y los mapeamos.
   ***Entra-Gruppen/Rollen:** Bitte definieren Sie die Gruppen für die 5 Rollen. Ideal: gleiche Namen wie unsere Rollen (1:1); andernfalls stellen Sie uns Ihre Namen/GUID bereit, die wir zuordnen.*
6. *(↳ responder en el formulario 8.5 / im Formular 8.5 beantworten)* **Pantalla de inicio de sesión (dos posibilidades):** Opción A (botón «Iniciar sesión con RhB») u Opción C (lista que se rellena con el uso). En ambas, la autenticación final (contraseña + MFA) se hace en la página de Microsoft, y queda el acceso local discreto. Nos inclinamos por C.
   ***Anmeldebildschirm (zwei Möglichkeiten):** Option A (Schaltfläche «Mit RhB anmelden») oder Option C (Liste, die sich mit der Nutzung füllt). In beiden Fällen erfolgt die finale Authentifizierung (Passwort + MFA) auf der Microsoft-Seite, und der lokale Zugang bleibt diskret erhalten. Wir bevorzugen C.*
7. *(↳ responder en el formulario 8.6 / im Formular 8.6 beantworten)* **MFA y pantalla táctil:** la contraseña se mantiene; para reducir el segundo factor en un IPC compartido, ¿Conditional Access (no re-pedir MFA cada turno en el dispositivo de confianza) o aprobación push (1 toque)?
   ***MFA und Touchscreen:** Das Passwort bleibt; um den zweiten Faktor an einem gemeinsam genutzten IPC zu reduzieren, Conditional Access (MFA nicht bei jeder Schicht erneut anfordern auf dem vertrauenswürdigen Gerät) oder Push-Bestätigung (1 Tipp)?*
8. *(↳ responder en el formulario 8.7 / im Formular 8.7 beantworten)* **Certificado HTTPS:** hoy self-signed. Para que Edge no muestre advertencias: (a) distribuir nuestra CA raíz (Intune/GPO), o (b) proporcionarnos un certificado de su entidad de confianza.
   ***HTTPS-Zertifikat:** derzeit selbstsigniert. Damit Edge keine Warnungen anzeigt: (a) Verteilung unserer Stammzertifizierungsstelle (Intune/GPO) oder (b) Bereitstellung eines Zertifikats Ihrer vertrauenswürdigen Stelle.*
9. *(informativo / informativ)* **Protección de interfaces internas:** la API REST y el canal de tiempo real (SignalR) que usa el frontend se protegerán con el token de Entra del usuario (cumple RhB 2.4.3).
   ***Schutz der internen Schnittstellen:** Die REST-API und der Echtzeitkanal (SignalR), die das Frontend nutzt, werden mit dem Entra-Token des Benutzers geschützt (erfüllt RhB 2.4.3).*

---

## Capítulo B — Cumplimiento general (RhB IT Standards) / Kapitel B — Allgemeine Konformität (RhB IT Standards)

Para su información y, donde corresponda, para que nos indiquen sus preferencias:
*Zu Ihrer Information und, wo zutreffend, zur Angabe Ihrer Präferenzen:*

- *(informativo / informativ)* **Arquitectura/Hosting:** sistema dual (PLC y backend en entornos separados); es la arquitectura del producto. / ***Architektur/Hosting:** duales System (SPS und Backend in getrennten Umgebungen); dies ist die Produktarchitektur.*
- *(informativo / informativ)* **Base de datos:** SQLite, base de datos embebida (sin servidor aparte ni coste de licencia; no es MS Access). / ***Datenbank:** SQLite, eingebettete Datenbank (kein separater Server, keine Lizenzkosten; nicht MS Access).*
- *(↳ responder en el formulario 8.8 / im Formular 8.8 beantworten)* **Envío de correos:** servidor SMTP configurable; lo apuntaremos a su Exchange Online. Necesitamos los datos del relay SMTP (host, puerto, autenticación) y qué contenido desean recibir (estadísticas, histórico de alarmas, informes…). / ***E-Mail-Versand:** konfigurierbarer SMTP-Server; wir richten ihn auf Ihr Exchange Online aus. Wir benötigen die SMTP-Relay-Daten (Host, Port, Authentifizierung) und welche Inhalte Sie erhalten möchten (Statistiken, Alarmhistorie, Berichte …).*
- *(↳ responder en el formulario 8.9 / im Formular 8.9 beantworten)* **Red y direccionamiento (LAN1):** necesitamos los parámetros de red (IP/máscara/gateway/DNS), el nombre de host/DNS de publicación (influye en el certificado) y las reglas de firewall (HTTPS entrante y saliente). / ***Netzwerk und Adressierung (LAN1):** wir benötigen die Netzwerkparameter (IP/Maske/Gateway/DNS), den Host-/DNS-Namen für die Veröffentlichung (beeinflusst das Zertifikat) und die Firewall-Regeln (HTTPS eingehend und ausgehend).*
- *(↳ responder en el formulario 8.11 / im Formular 8.11 beantworten)* **Código fuente:** podemos entregarles el código fuente del software del PLC. / ***Quellcode:** Wir können Ihnen den Quellcode der SPS-Software übergeben.*
- *(↳ responder en el formulario 8.11 / im Formular 8.11 beantworten)* **Antivirus:** según nos indicaron, lo instalan ustedes; lo recordamos para coordinar exclusiones si hiciera falta. / ***Virenschutz:** laut Ihrer Angabe installieren Sie ihn; wir weisen darauf hin, um bei Bedarf Ausnahmen abzustimmen.*
- *(↳ responder en el formulario 8.10 / im Formular 8.10 beantworten)* **Monitorización:** el Supervisor puede exponer un endpoint de estado por HTTP (p. ej. `/health`) para su Zabbix: disponibilidad, conexión con el PLC, base de datos y servicios externos (p.ej. Modbus). / ***Monitoring:** Der Supervisor kann einen HTTP-Statusendpunkt (z. B. `/health`) für Ihr Zabbix bereitstellen: Verfügbarkeit, SPS-Verbindung, Datenbank und externe Dienste (z. B. Modbus).*
- *(↳ responder en el formulario 8.10 / im Formular 8.10 beantworten)* **Copias de seguridad:** el Supervisor genera sus propias copias firmadas (ZIP: configuración, base de datos y modelos) con restauración y verificación. Podemos depositarlas en una carpeta de su red (UNC) para que su Veeam las recoja. / ***Datensicherung:** Der Supervisor erstellt eigene signierte Sicherungen (ZIP: Konfiguration, Datenbank und Modelle) mit Wiederherstellung und Prüfung. Wir können sie in einem Netzwerkordner (UNC) ablegen, damit Ihr Veeam sie abholt.*

---

## Capítulo C — Integración Modbus / Kapitel C — Modbus-Integration

### C.1 Contexto / Kontext

El Supervisor puede integrar Modbus TCP con doble rol (se habilita por configuración, por proyecto):
*Der Supervisor kann Modbus TCP in einer Doppelrolle integrieren (per Konfiguration, projektweise aktivierbar):*

- Servidor / esclavo: expone datos del PLC como registros Modbus para que otros sistemas de RhB los consuman. / *Server / Slave: stellt SPS-Daten als Modbus-Register bereit, damit andere RhB-Systeme sie nutzen.*
- Cliente / maestro: lee/escribe registros en dispositivos Modbus externos. / *Client / Master: liest/schreibt Register in externen Modbus-Geräten.*

### C.2 Información requerida de RhB / Von RhB benötigte Angaben

> *(↳ responder en el formulario 8.12 / im Formular 8.12 beantworten)* Para las variables, el mapa de registros y las alarmas, adjuntamos el Excel `RhB_Modbus_V1` (hojas `Modbus_Variables` y `Modbus_Alarms`): rellénenlo y devuélvanlo con el formulario.
> *Für die Variablen, den Registerplan und die Alarme fügen wir die Excel-Datei `RhB_Modbus_V1` (Blätter `Modbus_Variables` und `Modbus_Alarms`) bei: bitte ausfüllen und mit dem Formular zurücksenden.*

1. **Rol del Supervisor:** ¿servidor/esclavo, cliente/maestro, o ambos? / ***Rolle des Supervisors:** Server/Slave, Client/Master oder beides?*
2. **Interlocutor:** ¿qué sistema de RhB consume nuestros datos, o a qué dispositivo(s) nos conectamos? IP(s) y puerto (por defecto 502). / ***Gegenstelle:** Welches RhB-System nutzt unsere Daten, oder mit welchem/welchen Gerät(en) verbinden wir uns? IP(s) und Port (Standard 502).*
3. **Variables / señales:** lista con dirección (lectura/escritura), tipo y unidades/escalado. / ***Variablen / Signale:** Liste mit Richtung (Lesen/Schreiben), Typ und Einheiten/Skalierung.*
4. **Mapa de registros Modbus:** direcciones (coils, discrete inputs, input/holding registers) — ¿las definen ustedes o las proponemos? / ***Modbus-Registerplan:** Adressen (Coils, Discrete Inputs, Input/Holding Registers) — definieren Sie sie oder schlagen wir sie vor?*
5. **Alarmas:** ¿hay alarmas a exponer/consumir por Modbus? / ***Alarme:** Sollen Alarme über Modbus bereitgestellt/genutzt werden?*
6. **Cadencia / tiempo real:** cada cuánto se leen/escriben los datos. / ***Taktung / Echtzeit:** in welchem Intervall die Daten gelesen/geschrieben werden.*
7. **Red:** ¿a través de qué interfaz va el Modbus (LAN1 hacia un sistema de RhB, o una red dedicada)? / ***Netzwerk:** Über welche Schnittstelle läuft Modbus (LAN1 zu einem RhB-System oder ein dediziertes Netz)?*

---

## 5. Entorno de pruebas (Entra ID) / Testumgebung (Entra ID)

Nos ofrecieron un entorno de prueba; para desarrollar y validar necesitamos:
*Sie haben uns eine Testumgebung angeboten; zur Entwicklung und Validierung benötigen wir:*

> *(↳ responder en el formulario 8.13 / im Formular 8.13 beantworten)*

- Tenant ID + Client ID de un registro de pruebas, con redirect URIs (desarrollo `http://localhost` y equipo de test). / *Tenant-ID + Client-ID einer Test-Registrierung, mit Redirect-URIs (Entwicklung `http://localhost` und Testgerät).*
- Usuarios de prueba con credenciales, uno por cada uno de los 5 roles. / *Testbenutzer mit Anmeldedaten, einer pro Rolle (5).*
- Grupos/roles de prueba asignados a los 5 roles, con el registro configurado para incluir el grupo/rol en el token. / *Testgruppen/-rollen, den 5 Rollen zugeordnet, wobei die Registrierung so konfiguriert ist, dass die Gruppe/Rolle im Token enthalten ist.*
- Registro de tipo SPA (cliente público, con PKCE) que expone la API, con `User.Read` consentido; sin secreto ni certificado de cliente. / *Registrierung vom Typ SPA (öffentlicher Client, mit PKCE), die die API bereitstellt, mit eingewilligtem `User.Read`; ohne Client-Secret oder -Zertifikat.*
- Política MFA / Conditional Access de prueba (relajada para validar el flujo). / *Test-Richtlinie für MFA / Conditional Access (gelockert zur Validierung des Ablaufs).*
- Acceso para probar: ¿desde nuestras instalaciones contra su tenant de pruebas, o dentro de su red (VPN/Citrix)? ¿Acuerdo de confidencialidad? / *Zugang zum Testen: von unseren Standorten aus gegen Ihren Test-Tenant oder innerhalb Ihres Netzes (VPN/Citrix)? Vertraulichkeitsvereinbarung?*
- Especificaciones del equipo/VM si debe ejecutarse en su infraestructura. / *Spezifikationen des Geräts/der VM, falls die Ausführung in Ihrer Infrastruktur erfolgen muss.*
- Persona de contacto técnico de RhB IT. / *Technische Ansprechperson der RhB IT.*

---

## 6. Trazabilidad de requisitos (CRA / IEC 62443 / RhB) / Anforderungs-Nachverfolgbarkeit (CRA / IEC 62443 / RhB)

> Mapeo orientativo de las decisiones de diseño a los marcos de cumplimiento (evidencia, no certificación).
> *Orientierende Zuordnung der Designentscheidungen zu den Konformitätsrahmen (Nachweis, keine Zertifizierung).*

| Tema / Thema | RhB IT Standards | IEC 62443-4-2 (FR) | EU CRA (Anexo I / Anhang I) |
|---|---|---|---|
| SSO Entra ID / OIDC | 2.4, 7.2 | FR1 – IAC (CR1.1) | Autenticación segura / Sichere Authentifizierung |
| MFA | 7.2.2 | FR1 – IAC (CR1.11) | Autenticación reforzada / Verstärkte Authentifizierung |
| Roles (RBAC) | 2.4.5 | FR2 – UC (CR2.1) | Control de acceso / Zugriffskontrolle |
| Permisos Graph mínimos / Minimale Graph-Berechtigungen | 2.4.9 | FR1 – IAC | Minimización de acceso / Zugriffsminimierung |
| Break-glass / disponibilidad / Verfügbarkeit | — | FR7 – Resource Availability | Disponibilidad / Verfügbarkeit |
| Validación de tokens (API/SignalR) / Token-Validierung | 2.4.3 | FR1 – IAC | Autenticación de interfaces / Schnittstellen-Authentifizierung |
| HTTPS / certificados / Zertifikate | 7.2.3 | FR3/FR4 | Cifrado en tránsito / Verschlüsselung bei Übertragung |
| Sin LDAP / Kein LDAP | 9.1.4 | FR1 – IAC | — |
| Audit log local / Lokales Audit-Log | — | FR6 | Registro de eventos / Ereignisprotokollierung |
| SBOM | 2.2.2 | IEC 62443-4-1 | Anexo I/VII / Anhang I/VII |
| Modbus | 9 | FR1/FR3 | Interfaces seguras / Sichere Schnittstellen |

---

## 7. Plan (resumen) / Plan (Zusammenfassung)

Tras la confirmación de los puntos abiertos (cap. 8) y la provisión del entorno de pruebas (cap. 5): desarrollo y validación en el entorno de test → despliegue.
*Nach Bestätigung der offenen Punkte (Kap. 8) und Bereitstellung der Testumgebung (Kap. 5): Entwicklung und Validierung in der Testumgebung → Inbetriebnahme.*

---

## 8. Formulario de respuesta de RhB / Antwortformular RhB

> A RhB: respondan cada punto en su área «Respuesta de RhB» (pueden extenderse libremente o adjuntar un anexo). Al final, completen el bloque de validación. Una vez cumplimentado, constituye el registro de acuerdo y forma parte de la evidencia de cumplimiento.
> *An RhB: Beantworten Sie jeden Punkt im Bereich «Antwort von RhB» (Sie können beliebig ausführlich antworten oder einen Anhang beifügen). Füllen Sie am Ende den Validierungsblock aus. Nach Ausfüllung gilt es als Vereinbarungsnachweis und ist Teil des Konformitätsnachweises.*

### 8.1 · Alcance del SSO / Geltungsbereich des SSO  *(ref. A.2.1)*
**Qué necesitamos / Was wir benötigen:** su conformidad con que Entra ID se use solo para entrar en la aplicación Aquafrisch, manteniéndose el inicio de sesión de Windows del IPC como hoy (modo kiosko: arranque directo en la aplicación, sin escritorio).
*Ihre Zustimmung, dass Entra ID nur für die Anmeldung in der Aquafrisch-Anwendung verwendet wird und die Windows-Anmeldung des IPC unverändert bleibt (Kiosk-Modus: direkter Start in der Anwendung, ohne Desktop).*
**Respuesta de RhB / Antwort von RhB:**
_(escriba aquí / hier eintragen)_

### 8.2 · Conectividad de red (LAN1) / Netzwerkanbindung (LAN1)  *(ref. A.2.2)*
**Qué necesitamos / Was wir benötigen:** (a) cómo sale el IPC hacia Entra (directa / vía proxy con su dirección y dominios autorizados / aislada); y (b) cómo llegan los clientes al IPC (misma red o VLAN / a través de firewall / por Citrix).
*(a) wie der IPC zu Entra ausgeht (direkt / über Proxy mit Adresse und freigegebenen Domänen / isoliert); und (b) wie die Clients zum IPC gelangen (gleiches Netz oder VLAN / über Firewall / über Citrix).*
**Respuesta de RhB / Antwort von RhB:**
_(escriba aquí / hier eintragen)_

### 8.3 · App Registration  *(ref. A.2.3)*
**Qué necesitamos / Was wir benötigen:** tras registrar la aplicación en su tenant, el Directory (tenant) ID, el Application (client) ID y las Redirect URIs autorizadas (incluida la URL de Citrix si aplica).
*Nach Registrierung der Anwendung in Ihrem Tenant: die Directory-(Tenant-)ID, die Application-(Client-)ID und die zugelassenen Redirect-URIs (inkl. Citrix-URL, falls zutreffend).*
**Respuesta de RhB / Antwort von RhB:**
_(escriba aquí o adjunte anexo / hier eintragen oder Anhang beifügen)_

### 8.4 · Grupos / roles de Entra / Entra-Gruppen / -Rollen  *(ref. A.2.5)*
**Qué necesitamos / Was wir benötigen:** los grupos (o app-roles) de Entra para los 5 roles y sus nombres (idealmente iguales a Administrador / Mantenimiento / Operador / Visor / Auditor) o sus GUID.
*Die Entra-Gruppen (oder App-Rollen) für die 5 Rollen und deren Namen (idealerweise gleich wie Administrator / Wartung / Bediener / Betrachter / Auditor) oder deren GUID.*
**Respuesta de RhB / Antwort von RhB:**
_(escriba aquí o adjunte anexo / hier eintragen oder Anhang beifügen)_

### 8.5 · Pantalla de inicio de sesión / Anmeldebildschirm  *(ref. A.2.6)*
**Qué necesitamos / Was wir benötigen:** su elección — Opción A (botón) u Opción C (lista que se rellena con el uso). Recomendamos C (ambas se explican en el cap. A, punto 6).
*Ihre Wahl — Option A (Schaltfläche) oder Option C (Liste, die sich mit der Nutzung füllt). Wir empfehlen C (beide werden in Kap. A, Punkt 6 erläutert).*
**Respuesta de RhB / Antwort von RhB:**
_(escriba aquí / hier eintragen)_

### 8.6 · MFA / Conditional Access  *(ref. A.2.7)*
**Qué necesitamos / Was wir benötigen:** la política para el segundo factor (MFA) en este IPC compartido — Conditional Access (no re-pedir MFA cada turno en el dispositivo de confianza) y/o aprobación push.
*Die Richtlinie für den zweiten Faktor (MFA) an diesem gemeinsam genutzten IPC — Conditional Access (MFA nicht bei jeder Schicht erneut anfordern auf dem vertrauenswürdigen Gerät) und/oder Push-Bestätigung.*
**Respuesta de RhB / Antwort von RhB:**
_(escriba aquí / hier eintragen)_

### 8.7 · Certificado HTTPS / HTTPS-Zertifikat  *(ref. A.2.8)*
**Qué necesitamos / Was wir benötigen:** su decisión — (a) distribuir nuestra CA raíz (Intune/GPO), o (b) proporcionarnos un certificado de su entidad de confianza para el nombre del host.
*Ihre Entscheidung — (a) Verteilung unserer Stamm-CA (Intune/GPO) oder (b) Bereitstellung eines Zertifikats Ihrer vertrauenswürdigen Stelle für den Hostnamen.*
**Respuesta de RhB / Antwort von RhB:**
_(escriba aquí / hier eintragen)_

### 8.8 · Correo (SMTP) / E-Mail (SMTP)  *(ref. Cap. B)*
**Qué necesitamos / Was wir benötigen:** los datos del relay SMTP de su Exchange Online (host, puerto y método de autenticación) y qué contenido desean recibir por correo (qué estadísticas, histórico de alarmas, informes).
*Die SMTP-Relay-Daten Ihres Exchange Online (Host, Port und Authentifizierungsmethode) und welche Inhalte Sie per E-Mail erhalten möchten (welche Statistiken, Alarmhistorie, Berichte).*
**Respuesta de RhB / Antwort von RhB:**
_(escriba aquí o adjunte anexo / hier eintragen oder Anhang beifügen)_

### 8.9 · Red y direccionamiento (LAN1) / Netzwerk und Adressierung (LAN1)  *(ref. Cap. B)*
**Qué necesitamos / Was wir benötigen:** los parámetros de red de LAN1 (IP, máscara, gateway, DNS), el nombre de host/DNS de publicación, y las reglas de firewall (HTTPS entrante cliente→IPC y saliente IPC→Entra).
*Die Netzwerkparameter von LAN1 (IP, Maske, Gateway, DNS), den Host-/DNS-Namen für die Veröffentlichung und die Firewall-Regeln (HTTPS eingehend Client→IPC und ausgehend IPC→Entra).*
**Respuesta de RhB / Antwort von RhB:**
_(escriba aquí o adjunte anexo / hier eintragen oder Anhang beifügen)_

### 8.10 · Monitorización y backup / Monitoring und Backup  *(ref. Cap. B)*
**Qué necesitamos / Was wir benötigen:** (a) cómo integrar la monitorización con Zabbix y qué parámetros vigilar; y (b) para el backup, si depositamos las copias en una carpeta de red (UNC) para Veeam (con carpeta y permisos) o prefieren otra forma.
*(a) wie das Monitoring mit Zabbix integriert wird und welche Parameter überwacht werden; und (b) für das Backup, ob wir die Sicherungen in einem Netzwerkordner (UNC) für Veeam ablegen (mit Ordner und Berechtigungen) oder ob Sie eine andere Form bevorzugen.*
**Respuesta de RhB / Antwort von RhB:**
_(escriba aquí / hier eintragen)_

### 8.11 · Código fuente y antivirus / Quellcode und Virenschutz  *(ref. Cap. B)*
**Qué necesitamos / Was wir benötigen:** su confirmación de que (a) basta con la entrega del código fuente del software del PLC, y (b) el antivirus lo instalan ustedes (indicando si harán falta exclusiones).
*Ihre Bestätigung, dass (a) die Übergabe des Quellcodes der SPS-Software ausreicht und (b) der Virenschutz von Ihnen installiert wird (mit Angabe, ob Ausnahmen erforderlich sind).*
**Respuesta de RhB / Antwort von RhB:**
_(escriba aquí / hier eintragen)_

### 8.12 · Integración Modbus / Modbus-Integration  *(ref. Cap. C)*
**Qué necesitamos / Was wir benötigen:** la definición de la integración Modbus — rol (servidor/cliente/ambos), interlocutor con IP y puerto (por defecto 502), cadencia y red. Para las variables, el mapa de registros y las alarmas, rellenen el Excel adjunto `RhB_Modbus_V1` (hojas `Modbus_Variables` y `Modbus_Alarms`).
*Die Definition der Modbus-Integration — Rolle (Server/Client/beides), Gegenstelle mit IP und Port (Standard 502), Taktung und Netzwerk. Für die Variablen, den Registerplan und die Alarme füllen Sie bitte die beigefügte Excel-Datei `RhB_Modbus_V1` aus (Blätter `Modbus_Variables` und `Modbus_Alarms`).*
**Respuesta de RhB / Antwort von RhB:**
_(escriba aquí; adjunte el Excel `RhB_Modbus_V1` cumplimentado / hier eintragen; ausgefüllte Excel-Datei `RhB_Modbus_V1` beifügen)_

### 8.13 · Entorno de pruebas / Testumgebung  *(ref. Cap. 5)*
**Qué necesitamos / Was wir benötigen:** el entorno de pruebas — registro/tenant de test, usuarios de prueba (uno por rol) y grupos de test, el modo de acceso (desde nuestras instalaciones o dentro de su red) y la persona de contacto técnico.
*Die Testumgebung — Test-Registrierung/-Tenant, Testbenutzer (einer pro Rolle) und Testgruppen, die Zugangsart (von unseren Standorten oder innerhalb Ihres Netzes) und die technische Ansprechperson.*
**Respuesta de RhB / Antwort von RhB:**
_(escriba aquí o adjunte anexo / hier eintragen oder Anhang beifügen)_

---

### Validación de la respuesta / Validierung der Antwort

| Respondido por / Beantwortet von | Cargo / Departamento — Funktion / Abteilung | Fecha / Datum |
|---|---|---|
|  |  |  |

> Si distintas personas responden distintos apartados, indíquenlo junto a cada respuesta.
> *Falls verschiedene Personen verschiedene Abschnitte beantworten, geben Sie dies bei der jeweiligen Antwort an.*
