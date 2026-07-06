# Gap Analysis — Aquafrisch Supervisor vs RhB IT Standards v9.0.4

> **Alcance:** este documento va **más allá de Entra ID**. Recoge **todos** los puntos del *RhB IT
> Standards v9.0.4* que el Supervisor **(a)** no cumple y debemos implementar, **(b)** requieren **input de
> RhB**, o **(c)** exigen **declarar una desviación**. Vive junto a [`SKILL.md`](./SKILL.md) (Entra) y
> [`correo-rhb.md`](./correo-rhb.md) (comunicación). `X` = criterio Muss (obligatorio).
>
> Leyenda de **Acción**: ✅ ya cumple · 🔧 implementar (nosotros) · ❓ input de RhB · ⚠️ declarar desviación.

## 0. Resumen ejecutivo — brechas de alto impacto (no-Entra)

1. **Hosting del backend (3.2.1 X «no bare metal»).** **Sistema dual:** el PLC funciona en su **propio
   entorno** y el **backend** del Supervisor en uno **separado**. Se lo **informamos** a RhB; si exigen una
   disposición concreta del backend (VM/contenedor), se coordina. (Ya no se plantea como simple desviación.)
2. **Base de datos (Cap. 8).** Usamos **SQLite** (embebida, sin coste, **no es Access**). El estándar habla
   de MS-SQL. ❓ ¿RhB acepta SQLite o **exige MS-SQL**? (exigir MS-SQL = migración grande).
3. **Email vía RhB (2.1.7 X).** Ya enviamos por SMTP configurable → ❓ datos del **relay Exchange Online**.
4. **Hostnames, no IP (2.1.9 X).** Hoy usamos **IP** (p.ej. 192.168.2.161). 🔧 cambiar a nombre + ❓ DNS de RhB.
5. **HTTPS cert público (7.2.3 X).** Hoy **self-signed**. ❓ (=D9 del correo Entra) + 🔧 distribución del cert.
6. **Código fuente en Bitbucket (2.1.19).** ❓ ¿RhB **exige depósito** del código fuente del producto? (negociar).
7. **Monitoring Zabbix (3.3) / Backup Veeam (3.4) / Antivirus (5.3.3 X).** ❓ cómo integran y **quién** los
   opera sobre un IPC «RhB-fremd».

## 1. Generales (Cap. 2.1)

| Ref | Requisito | Estado Aquafrisch | Acción |
|---|---|---|---|
| 2.1.2 X | Web app usable en workstation/móvil/tablet | HMI pensado para IPC táctil; responsive en móvil **por verificar** | 🔧 verificar/ajustar |
| 2.1.3 X | Usar plataformas RhB (Anhang A) donde aplique | Entra (sí), Exchange (email), Confluence (docs)… | 🔧 + ❓ |
| 2.1.7 X | Email on-prem vía servidor RhB | SMTP configurable (`SmtpClient`) | ❓ relay Exchange + 🔧 config |
| 2.1.9 X | Direccionar por host/servicename, no IP | Deploy/URLs por **IP** | 🔧 + ❓ DNS |
| 2.1.10 | Sin phone-home / sin internet directo (proxy) | Salientes: Entra/Graph; sin telemetría | ⚠️ declarar + 🔧 proxy |
| 2.1.12 X | Compatible con su Endpoint Protection | Por verificar con su EPP | ❓ + 🔧 (exclusiones) |
| 2.1.15 | Comunicación cifrada entre sistemas | HTTPS + SignalR sobre TLS | ✅/🔧 verificar |
| 2.1.16 X | Protección de datos (derecho suizo) | Audit log con datos de usuario | 🔧 + ❓ (¿DPA?) |
| 2.1.18 | IA en desarrollo sin guardar prompts (training) | Usamos Copilot (enterprise) | ⚠️ declarar (no-training) |
| 2.1.19 | Código fuente en GIT de RhB (Bitbucket) | Producto propietario | ❓ ¿exigido? (negociar) |

## 2. Documentación (Cap. 2.2)

| Ref | Requisito | Estado | Acción |
|---|---|---|---|
| 2.2.1 | HW/SW/red + arquitectura + diagrama de contexto | Parcial (docs internas) | 🔧 producir |
| 2.2.2 X | **SBOM** con cada versión | **Ya generamos SBOM** (CRA) | ✅ entregar |
| 2.2.3/2.2.4 | Documentar en **Confluence**, aceptado por RhB IT | No | 🔧 + ❓ acceso Confluence |
| 2.2.5 | Betriebshandbuch, manuales, protocolo restore/recovery… | Parcial | 🔧 producir |

## 3. Impresión (Cap. 2.3) — solo si se imprime

| Ref | Requisito | Estado | Acción |
|---|---|---|---|
| 2.3.2/2.3.6 | Windows print + Follow-Me (Ricoh Streamline / Universal Driver) | Botón «Imprimir» en ExportModal | ❓ ¿se imprime a impresoras de red? → alinear |

## 4. Almacenamiento (Cap. 2.5) · Servidor (Cap. 3)

| Ref | Requisito | Estado | Acción |
|---|---|---|---|
| 2.5.1 | Evitar almacenamiento local; preferir SharePoint | Datos locales (SQLite, exports) en la máquina | ⚠️ declarar (máquina) |
| 3.2.1 X | **No bare metal**; VM (VMware) / contenedor (OpenShift) | Backend en **IPC de la máquina** (bare metal) | ⚠️ declarar + ❓ aceptación |
| 3.2.3 X | OS servidor de Anhang A (Win Server 2022) | IPC con Windows 10/11 industrial | ⚠️ declarar + ❓ |
| 3.3 | Monitoring (WMI/SNMP/REST) → **Zabbix** | Hoy solo `HEAD /api/models` (200); **no** hay `/health` dedicado, SNMP ni WMI. Añadir `/health` = 1 línea (`MapHealthChecks`) | 🔧 + ❓ integración Zabbix |
| 3.4 | Backup **Veeam** | Backup propio **firmado** (ZIP: config + BD + modelos) + restore/verify (`BackupController`); se puede **depositar en carpeta de red (UNC)** para Veeam | ❓ carpeta/permisos de red |
| 3.6.1.1 X | Web sin plug-ins cliente | React, sin plugins | ✅ |
| 3.6.1.3 | Sin datos sensibles en URL | Por verificar | 🔧 verificar |
| 3.6.1.4 | PWA configurable | No | 🔧 (opcional/bajo) |
| 3.6.2 X | Soportar **MS Edge**; render correcto; móvil | Edge ok; móvil por verificar | 🔧 verificar |

## 5. Servicios Windows (Cap. 3.7)

| Ref | Requisito | Estado | Acción |
|---|---|---|---|
| 3.7.2 X | Gestionable en remoto sin RDP | Windows Service + APIs | ✅/🔧 verificar |
| 3.7.3 X | Resistente a boot/patch/Service-Pack (auto-arranque) | **Ya es Windows Service** | ✅/🔧 verificar |
| 3.7.4 X | Service-User dedicado (sin login interactivo) | Por definir | 🔧 + ❓ service account |
| 3.7.6 X | Rutas **UNC**, sin mapeo de letras de unidad | Export a carpetas locales/red | 🔧 (UNC en export) |
| 3.7.7 X | RDP FIPS compliant | Config OS (RhB) | ❓ (RhB OS) |
| 3.7.8 X | **NetBIOS desactivado** | Config OS | ❓/🔧 verificar |

## 6. Máquina (Cap. 5) · Bases de datos (Cap. 8) · Interfaces (Cap. 9)

| Ref | Requisito | Estado | Acción |
|---|---|---|---|
| 5.2 X | Máquina «RhB-fremd» en **zona aislada**, sin acceso a Umsysteme, internet solo por proxy, **RhB DNS**, email por **relay** | Diseño de red por definir (¡tensión con Entra cloud!) | ❓ diseño de red + ⚠️ |
| 5.3.3 X | **Antivirus** instalado/actualizado por el proveedor | Por definir en IPC fremd | ❓ ¿quién? + 🔧 |
| 5.4 X | Fernwartung solo por vías RhB (10.3/10.4) | Por definir | ❓ |
| 8.6 X | **MS Access prohibido** | Usamos SQLite (no Access) | ✅ |
| 8.x | Licenciar DB; preferencia MS-SQL | **SQLite** embebida (sin coste) | ❓ ¿aceptan SQLite o exigen MS-SQL? |
| 9.1.4 X | LDAP prohibido | Iremos con Entra (no LDAP) | ✅ (vía Entra) |
| 9.3.1 X | Sin serie/paralelo/**USB** en servidores; TCP/IP | PLC vía ADS (TCP); ¿USB en IPC? | 🔧 verificar/declarar |

## 7. Operación (Cap. 10)

| Ref | Requisito | Estado | Acción |
|---|---|---|---|
| 10.1 | Entornos Test/Integration/Produktion | (Entorno test ya en correo) | ❓ (ver correo) |
| 10.2.2.1 X | Canal de **soporte** (tel/email/web) | Por declarar | 🔧 declarar |
| 10.3 | Remote access vía **Citrix/VPN** | Por definir | ❓ |
| 10.4 | Remote support con herramienta aprobada (Teams/Citrix Director) + Vertraulichkeitserklärung | Por definir | ❓ |

---

## 8. Preguntas ADICIONALES para RhB (candidatas al correo)

> Estas son las que **necesitan input de RhB** (además de las de Entra). Confirmar con el usuario cuáles
> incluir en el correo (sección D).

1. **Hosting:** informamos la **arquitectura dual** (PLC y backend en entornos separados); coordinar si
   requieren una disposición concreta del backend (VM/contenedor).
2. **Base de datos:** **informamos** que usamos **SQLite** embebida (sin coste, no es Access). No pedimos permiso.
3. **Email:** datos del **relay Exchange Online** (host, puerto, auth) para el envío.
4. **DNS/hostnames:** nombre(s) DNS del IPC/servicio (para no usar IP).
5. **Monitoring:** ¿cómo integran **Zabbix**? ¿les basta un **endpoint REST de estado** (podemos exponer `/health`) o requieren SNMP/WMI?
6. **Backup:** tenemos backup propio firmado (ZIP); podemos **dejar las copias en una carpeta de su red (UNC)** para que **Veeam** las recoja — ¿carpeta y permisos? ¿o respaldan el IPC de otra forma?
7. **Antivirus/EPP:** **lo instalan ellos** (confirmado) — lo **recordamos** y coordinamos exclusiones si hace falta.
8. **Código fuente:** ofrecemos el **código del SW del PLC** (la app de supervisión es producto propio).
9. **Documentación:** acceso a **Confluence** para la documentación de sistema.
10. **Red/zona (Cap. 5.2):** diseño de la zona de la máquina (proxy, DNS, relay email, acceso remoto) — y
    cómo encaja con la conectividad a Entra (cloud).
11. **Impresión:** ¿se imprime a **impresoras de red** (Ricoh Follow-Me) desde el Supervisor?
12. **Service account / OS:** ¿proveen cuenta de servicio dedicada? ¿NetBIOS off / RDP FIPS gestionados por RhB?

## 9. Implementaciones NUESTRAS (no requieren RhB, planificar)

- Cambiar URLs/deploy de **IP → hostname** (2.1.9 X).
- **SMTP** apuntando al relay RhB (2.1.7 X) — config, no código nuevo.
- **UNC paths** en el export wizard (3.7.6 X).
- **Service-User** dedicado para el Windows Service (3.7.4 X).
- Exponer **métricas/health** para Zabbix (3.3).
- Verificar **responsive/Edge/móvil** (2.1.2 X, 3.6.2 X).
- Producir **documentación** (Betriebshandbuch, manuales, protocolo restore) + entregar **SBOM** (2.2).
- Declarar: **sin phone-home** (2.1.10), **IA sin training** (2.1.18), **desviaciones** de hosting/almacenamiento.
- Canal de **soporte** documentado (10.2.2.1 X).
