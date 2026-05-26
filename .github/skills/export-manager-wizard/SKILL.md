---
name: export-manager-wizard
description: 'Implement or extend the Aquafrisch Export Manager Wizard — a reusable multi-step (qué / formato / destino / configurar / automatización / resumen) export configurator launched from the common ExportModal via a 3rd button gated by Excel `SystemConfig.EnableFileExport`. Every wizard run produces a persistent `ExportTask` listed inside the host modal (manual, scheduled or PLC-triggered). USE WHEN: adding the "Gestor de Exportaciones" button, building the wizard, wiring destinations (carpeta local/red, email), persisting tasks, listing/editing/forcing tasks, hooking PLC triggers or cron scheduler, or integrating into a new modal (Estadísticas, Mantenimiento, Alarmas…). DO NOT USE FOR: existing Print/QR buttons (leave intact), FTP/SFTP/WhatsApp (futuro), installing new PDF/Excel libs (PROHIBIDO), or unrelated PDF/QR generation. Trigger phrases: "gestor de exportaciones", "export manager wizard", "wizard exportación", "EnableFileExport button", "tareas de exportación", "ExportTask", "exportaciones programadas", "automatización export", "destino email export", "trigger PLC export".'
argument-hint: 'Indica fase (1-3) y modal anfitrión, p.ej. "fase 1 en ExportModal Estadísticas"'
---

# Export Manager Wizard — Implementation Skill

Aquafrisch SCADA tiene un `ExportModal` común con **Imprimir** + **QR**. Cuando `EnableFileExport=TRUE` en Excel `System Config`, aparece un **3er botón** "Gestor de Exportaciones" que abre un wizard. **Cada paso por el wizard genera una `ExportTask` persistente** (manual, programada o disparada por PLC). El propio `ExportModal` lista las tareas existentes de su módulo, permite ejecutarlas, editarlas, pausarlas o eliminarlas.

## Decisiones cerradas con el usuario

| Tema | Decisión |
|------|----------|
| Componente | **Genérico y reutilizable**. Cada modal pasa `source` + `schema` + `data` + `dynamicFields`. |
| Generación del archivo | **Backend C#** (no navegador). Permite guardar en `C:\`, `\\server\share\`, `Z:\` mapeada. |
| Persistencia | **TODAS** las configuraciones del wizard se guardan como `ExportTask` (incluidas manuales). Sin tablas separadas. |
| Listado | Cada `ExportModal` muestra **sus** tareas (filtradas por `source`). Acciones: ▶ Ejecutar ahora, ✎ Editar, ⏸ Pausar, 🗑 Eliminar. |
| Roles | `Administrator`, `SuperAdmin` (oculto al cliente), `Maintenance` (nombres EXACTOS del enum `SystemRole`). |
| Canales v1 | Carpeta local/red + Email. **Son destinos independientes (checkboxes)**: el usuario marca uno, los dos, o ninguno (validación: al menos 1). Marcar solo Email = no se toca ninguna carpeta y la regla de `AllowedExportFolders` **no aplica**. |
| FTP/SFTP/WhatsApp | NO se implementan ni se muestran. Patrón `IExportRunner` deja la puerta abierta. |
| **Librerías** | **PROHIBIDO instalar nada nuevo**. Solo lo ya presente: `ClosedXML` (XLSX), `System.Text.Json`, CSV manual, `Markdig` (HTML). |
| **PDF** | **NO se incluye en ningún formato del wizard**. Motivo: QuestPDF y similares con coste de licencia para uso comercial son incompatibles con la transparencia CRA/SBOM exigida en este producto. La impresión a PDF sigue disponible **solo** mediante el botón existente "Imprimir" del `ExportModal` (no es parte del Gestor). |
| Carpeta local | **Solo aplica si el usuario marca "Carpeta local" en Step 2.** En ese caso: **sin fallback automático**. Si `AllowedExportFolders` está vacío en Excel → checkbox **deshabilitado** con tooltip "Configure rutas permitidas en Excel `System Config → AllowedExportFolders`". El usuario puede seguir usando Email (que es independiente). |
| **Selección de dataset** | **Declarativa por módulo**. Cada `ExportModal` pasa un prop `datasets=[{id,label,fields[],filters[],backendProvider,captureMode?}]`. El wizard expone en Step 0 los **campos seleccionables** (checkboxes con `default`/`required`) y los **filtros** que el módulo declara — **idénticos a los controles que el módulo ya tiene en pantalla**. Los filtros se pre-rellenan desde `currentFilters` para que el usuario solo confirme. **No se inventan filtros ni se cargan opciones desde endpoints nuevos**: si una lista de opciones existe en la UI, el módulo la pasa inline. |
| **Preview** | Step 0 muestra **preview de 5 filas** con la selección actual (`POST /api/export/preview` con `{datasetProvider, selection}`). |
| Traducciones | Al final del roadmap. En dev usar `t('clave', 'fallback ES')`. |

## Arquitectura objetivo

```
ExportModal source="estadisticas"
  ├── [🖨 Imprimir]
  ├── [📱 QR]
  └── [⚙ Gestor de exportaciones]      ← gated EnableFileExport + rol
         │
         └─► Panel "Tareas de este módulo"
                ┌─ Tabla de ExportTask (source="estadisticas")
                │    nombre · tipo · destino · último resultado · [▶ ✎ ⏸ 🗑]
                └─ [+ Nueva tarea] ──► <ExportManagerWizard />
                                          │
                                          ├── Step 0: ¿Qué exportar?
                                          │     - Combo dataset (si hay >1)
                                          │     - Checkboxes de campos (default/required)
                                          │     - Filtros declarados por el módulo (pre-rellenos)
                                          │     - Preview de 5 filas
                                          ├── Step 1: Formato (XLSX, CSV, JSON, HTML, PNG)
                                          ├── Step 2: Destinos (☐ Carpeta  ☐ Email — independientes, ≥1)
                                          ├── Step 3: Configurar destinos marcados (filename + tokens, email)
                                          ├── Step 4: Tipo de ejecución (Manual / Programada / Trigger PLC)
                                          └── Step 5: Resumen + [Guardar tarea]

Backend:
  POST   /api/export/tasks          (crear)
  GET    /api/export/tasks?source=  (listar por módulo)
  PUT    /api/export/tasks/{id}     (editar)
  DELETE /api/export/tasks/{id}     (borrar)
  POST   /api/export/tasks/{id}/run (ejecutar ahora)
  POST   /api/export/tasks/{id}/toggle (pausar/reanudar)
  POST   /api/export/preview            (5 filas para Step 0)
```

### Patrón `IExportRunner` (extensible)

```csharp
public interface IExportRunner {
    string DestinationType { get; }   // "local" | "email"; futuro: "ftp", "sftp", "whatsapp"
    Task<ExportResult> RunAsync(ExportConfig cfg, byte[] file, string filename);
}
```
Añadir un destino futuro = nuevo `IExportRunner` + opción en Step 2. Sin tocar el wizard ni el modelo `ExportTask`.

### Patrón `IExportDatasetProvider` (selección declarativa)

Cada módulo registra **un provider por dataset**. El frontend declara qué datasets ofrece y el provider backend resuelve la query con la selección del usuario.

```csharp
public interface IExportDatasetProvider {
    string DatasetId { get; }                       // "estadisticas.tabla-ciclos"
    Task<ExportDataset> GetAsync(ExportSelection sel, CancellationToken ct);
}

public class ExportSelection {
    public string[] Fields { get; init; } = Array.Empty<string>();
    public Dictionary<string, object?> Filters { get; init; } = new();
    public int? PreviewLimit { get; init; }         // 5 para preview, null para export real
}

public class ExportDataset {
    public string[] Columns { get; init; } = Array.Empty<string>();
    public object?[][] Rows { get; init; } = Array.Empty<object?[]>();
    public Dictionary<string, object?> Metadata { get; init; } = new(); // título, rango, totales
    public int TotalRows { get; init; }             // antes de PreviewLimit
}
```

Registro en `Program.cs`:
```csharp
builder.Services.AddScoped<IExportDatasetProvider, EstadisticasCiclosProvider>();
builder.Services.AddScoped<IExportDatasetProvider, AuditoriaProvider>();
```

`ExportService` resuelve por `DatasetId` desde el `IEnumerable<IExportDatasetProvider>` inyectado.

### Modelo `ExportTask` (BD del proyecto)

```csharp
public class ExportTask {
    public int Id { get; set; }
    public string ProjectId { get; set; }
    public string Source { get; set; }        // "estadisticas" | "mantenimiento" | "alarmas" | ...
    public string Name { get; set; }          // descriptivo (lo escribe el usuario)
    public string ExecutionType { get; set; } // "manual" | "cron" | "plc"
    public string? CronExpression { get; set; }
    public string? PlcVariable { get; set; }  // bool, p.ej. "EXPORT_REPORT_TRIGGER"
    public string Format { get; set; }        // xlsx|csv|json|html|png
    public string Destinations { get; set; }  // CSV de destinos activos: "local", "email", "local,email"
    public string ConfigJson { get; set; }    // { filename, folder?, email? } — solo las claves de destinos activos
    public string DatasetProvider { get; set; } // DatasetId, ej. "estadisticas.tabla-ciclos"
    public string SelectionJson { get; set; } // ExportSelection serializado (Fields + Filters)
    public bool Enabled { get; set; }
    public string CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public string? LastResult { get; set; }   // "ok" | "error: ..."
}
```

Migration EF Core en BD del proyecto activo (`Projects/{id}/data/project.db`).

## Roadmap por fases

### Fase 1 — Tareas manuales + Email + Carpeta local/red
- Wizard sin Step 4 visible (todas las tareas creadas como `ExecutionType="manual"`).
- Lista de tareas en el modal con [▶ Ejecutar ahora].
- Backend genera el archivo y lo guarda/envía.

### Fase 2 — Trigger PLC
- Step 4 habilita opción "PLC". Selector de variable bool.
- `PlcNotificationService` carga tareas con `ExecutionType="plc"` y se suscribe.
- Flanco `false→true` ejecuta la tarea.

### Fase 3 — Programada (cron) — opcional
- Step 4 habilita opción "Programada" (preset: cada hora, cada turno, medianoche, custom cron).
- Nuevo `ExportSchedulerService : BackgroundService` evalúa cada minuto.

### Futuro (NO planificado)
FTP, SFTP, WhatsApp. **PDF queda fuera definitivamente** mientras no exista una lib OSS compatible con la postura CRA/SBOM del producto.

---

## Workflow — Fase 1 (paso a paso)

### Paso 1.1 — Botón condicional + lista en `ExportModal`

[my-3d-app/src/components/ExportModal.js](my-3d-app/src/components/ExportModal.js):
```jsx
const { canExportFiles } = useSystemFeatures();
const { user } = useAuth();
const ALLOWED = ['Administrator','SuperAdmin','Maintenance']; // comparar contra user.role tal cual viene del backend
const canUseManager = canExportFiles && ALLOWED.includes(user?.role?.toLowerCase());

// Nueva prop: source (identificador del módulo anfitrión)
// p.ej. <ExportModal source="estadisticas" ... />

{canUseManager && (
  <button onClick={() => setShowManager(true)}>⚙ Gestor de exportaciones</button>
)}

{showManager && (
  <ExportTasksPanel
    source={source}
    datasets={datasets}            // declaración del módulo
    currentFilters={currentFilters} // valores actuales en pantalla (pre-rellenar wizard)
    onClose={...}
  />
)}
```

**NO** tocar handlers Print/QR. Añadir `source`, `datasets`, `currentFilters` como props pero **mantener compat**: si `source` o `datasets` no se pasan, ocultar el botón Gestor (no romper modales que aún no lo usan).

### Paso 1.1.b — Cómo declara un módulo sus datasets

Ejemplo en Estadísticas:
```jsx
<ExportModal
  source="estadisticas"
  datasets={[
    {
      id: 'estadisticas.tabla-ciclos',
      label: t('export.ds.ciclos', 'Tabla de ciclos'),
      backendProvider: 'estadisticas.tabla-ciclos',
      fields: [
        { id: 'fecha',    label: t('col.fecha','Fecha'),    required: true,  default: true },
        { id: 'producto', label: t('col.producto','Producto'), default: true },
        { id: 'duracion', label: t('col.duracion','Duración'), default: true },
        { id: 'consumo',  label: t('col.consumo','Consumo'),   default: false },
      ],
      filters: [
        { id: 'rangoFechas', type: 'dateRange', label: t('flt.rango','Rango fechas') },
        { id: 'producto',    type: 'multiSelect', label: t('flt.prod','Producto'),
          options: productosVisiblesEnPantalla },   // inline, ya cargados en la vista
        { id: 'soloErrores', type: 'boolean', label: t('flt.err','Solo con error') },
      ],
    },
    {
      id: 'estadisticas.grafica-consumo',
      label: t('export.ds.consumo','Gráfica de consumo'),
      captureMode: 'echarts',                       // PNG vía getDataURL
      echartsRef: consumoChartRef,
      fields: [],
      filters: [],
    },
  ]}
  currentFilters={{ rangoFechas: rango, producto: prodSel, soloErrores: errOnly }}
/>
```

**Reglas obligatorias para el módulo anfitrión:**
- Solo declarar campos que la vista realmente muestra/sabe pintar.
- Solo declarar filtros que ya existen en su UI; pasar las opciones inline (no añadir endpoints nuevos).
- `currentFilters` debe contener los valores **actualmente aplicados** en pantalla.
- Para datasets de tipo gráfica, `captureMode: 'echarts'` + `echartsRef` (ref del componente echarts).

Tipos de filtro soportados (suficientes para todos los módulos actuales): `dateRange`, `multiSelect`, `singleSelect`, `boolean`, `number`, `text`.

### Paso 1.2 — Componente `ExportTasksPanel`

```
my-3d-app/src/components/ExportManager/
├── ExportTasksPanel.js          ← lista + botón "Nueva tarea"
├── ExportManagerWizard/
│   ├── ExportManagerWizard.js
│   ├── steps/
│   │   ├── Step0WhatToExport.js
│   │   ├── Step1Format.js
│   │   ├── Step2Destination.js
│   │   ├── Step3ConfigureDestination.js
│   │   └── Step5Summary.js      (Step4 reservado para Fase 2/3)
│   └── utils/dynamicFields.js
└── styles.module.css
```

`ExportTasksPanel`:
- Llama `GET /api/export/tasks?source=estadisticas` al abrir.
- Tabla con columnas: Nombre · Tipo · Formato · Destino · Último resultado · Acciones.
- Botón "+ Nueva tarea" abre `ExportManagerWizard`.
- Acciones por fila:
  - ▶ `POST /api/export/tasks/{id}/run` → muestra spinner + resultado.
  - ✎ Abrir wizard pre-cargado con la tarea (modo edición).
  - ⏸ `POST /api/export/tasks/{id}/toggle`.
  - 🗑 `DELETE /api/export/tasks/{id}` (con confirmación).

### Paso 1.3 — Wizard estado

```ts
{
  step: 0|1|2|3|5,
  taskId?: number,                          // modo edición
  name: string,
  // Step 0:
  datasetId: string,                        // id del dataset elegido
  selection: {
    fields: string[],                       // campos marcados
    filters: Record<string, any>            // valores (pre-rellenos desde currentFilters)
  },
  // Step 1:
  format: 'xlsx'|'csv'|'json'|'html'|'png',
  // Step 2: destinos independientes (≥1)
  destinations: {
    local: boolean,
    email: boolean
  },
  // Step 3: solo las claves de destinos activos
  config: {
    filename: string,
    folder?: string,                        // solo si destinations.local
    email?: { to:[], cc:[], cco:[], subject, body }  // solo si destinations.email
  },
  executionType: 'manual'                   // forzado en Fase 1
}
```

Validación Step 2: `destinations.local || destinations.email` (al menos uno).

Al "Guardar tarea":
- Si `taskId` → `PUT /api/export/tasks/{taskId}`.
- Si no → `POST /api/export/tasks`.
- Cerrar wizard y refrescar lista en `ExportTasksPanel`.

### Paso 1.4 — Formatos disponibles

Step 1 muestra: **XLSX, CSV, JSON, HTML, PNG**. PDF **no** aparece en el wizard (la impresión a PDF sigue disponible vía el botón "Imprimir" existente, fuera del Gestor).

### Paso 1.5 — Tokens dinámicos

`utils/dynamicFields.js`: `{fecha}`, `{hora}`, `{fecha_hora}`, `{año}`, `{mes}`, `{día}`, `{ciclo}`, `{plc}`, `{linea}`, `{turno}`, `{producto}`.

`resolveFilename(template, fields)` → reemplaza + sanitiza (sin `:`, `/`, `\`, `*`, `?`, `"`, `<`, `>`, `|`).

Tokens disponibles los provee cada `IExportDatasetProvider` del backend al momento de ejecutar (no se evalúan en el wizard, se guardan literales con `{}`).

### Paso 1.6 — Backend: modelo, controller y servicios

Archivos nuevos:
```
Models/Export/ExportTask.cs
Models/Export/ExportConfig.cs                  (DTO config)
Models/Export/ExportResult.cs
Data/Migrations/<timestamp>_AddExportTasks.cs
Controllers/ExportTasksController.cs
Services/Export/
├── IExportRunner.cs
├── LocalFileRunner.cs
├── EmailRunner.cs
├── ExportFormatterService.cs                  (genera bytes por formato)
├── IExportDatasetProvider.cs                  (cada módulo registra el suyo)
└── ExportService.cs                           (orquesta: dataset → bytes → runner)
```

### Paso 1.7 — `ExportFormatterService` (formatos backend)

| Formato | Lib | Notas |
|---------|-----|-------|
| XLSX | `ClosedXML` (ya instalado) | Tabla simple con headers + rows |
| CSV | Manual `StringBuilder` | UTF-8 BOM, separador `;` (Excel ES), escape `"` |
| JSON | `System.Text.Json` | Indented |
| HTML | Manual + `Markdig` si hace falta | Plantilla básica con CSS inline |
| PNG | Frontend manda base64 | El cliente captura el chart con echarts `getDataURL()` y lo envía en el payload |

**PDF NO se soporta** en el Gestor (decisión por compliance CRA/SBOM). Si el usuario quiere PDF → usar el botón "Imprimir" existente del `ExportModal`.

### Paso 1.8 — `LocalFileRunner`

**Solo se invoca si `task.Destinations` incluye `"local"`.** Si el usuario solo marcó Email, el orquestador `ExportService` no llama nunca a este runner y `AllowedExportFolders` es irrelevante para esa tarea.

```csharp
public async Task<ExportResult> RunAsync(ExportConfig cfg, byte[] file, string filename) {
    if (string.IsNullOrWhiteSpace(cfg.Folder))
        throw new InvalidOperationException("Folder requerido para destino local");
    ValidateAgainstAllowList(cfg.Folder);                   // 403 si no permitido
    Directory.CreateDirectory(cfg.Folder);
    var fullPath = Path.Combine(cfg.Folder, filename);
    var safePath = Path.GetFullPath(fullPath);
    if (!safePath.StartsWith(Path.GetFullPath(cfg.Folder), StringComparison.OrdinalIgnoreCase))
        throw new SecurityException("Path traversal detected");
    await File.WriteAllBytesAsync(safePath, file);
    return new ExportResult { Success = true, Path = safePath, SizeBytes = file.Length };
}
```

`AllowedExportFolders` (Excel `System Config`, lista separada por `;`) — **solo aplica cuando el destino "local" está activo**:
- Si **vacío** → POST/PUT de una tarea con `"local"` entre destinos devuelve `400 Bad Request: "Carpeta local no configurada. Configure AllowedExportFolders en Excel System Config."`. El frontend ya debería haber deshabilitado el checkbox en Step 2.
- Si **lleno** → validar `Path.GetFullPath(folder)` empieza por uno de los permitidos (case-insensitive). 403 si no.
- **No hay fallback automático**: sin configuración explícita no se guarda en disco.
- Tareas con destino **solo Email** se aceptan siempre (ignorando `AllowedExportFolders`).

### Paso 1.9 — `EmailRunner`

Usar `System.Net.Mail.SmtpClient` con config del proyecto activo desde `SystemConfig`:
- `Smtp:Host`, `Smtp:Port`, `Smtp:User`, `Smtp:Pass`, `Smtp:From`, `Smtp:EnableSsl`.
- Extender el switch en [SW.PC.API.Backend_/Services/ExcelConfigService.cs](SW.PC.API.Backend_/Services/ExcelConfigService.cs) para parsear estas claves.
- Validar `EnableEmailSending=true` antes de enviar.
- Audit log con destinatarios.

### Paso 1.10 — Endpoints

[Controllers/ExportTasksController.cs] — `[Authorize(Roles="Administrator,SuperAdmin,Maintenance")]`:

| Método | Ruta | Acción |
|--------|------|--------|
| GET | `/api/export/tasks?source={src}` | Lista filtrada por source |
| POST | `/api/export/tasks` | Crea (valida ≥1 destino; si incluye `local` valida `AllowedExportFolders`) |
| PUT | `/api/export/tasks/{id}` | Edita (mismas validaciones) |
| DELETE | `/api/export/tasks/{id}` | Borra |
| POST | `/api/export/tasks/{id}/run` | Ejecuta los destinos activos en paralelo (sync, devuelve `ExportResult[]`) |
| POST | `/api/export/tasks/{id}/toggle` | Enabled true↔false |
| POST | `/api/export/preview` | `{datasetProvider, selection}` → 5 filas para Step 0 |

Cada operación → `AuditService.LogAsync()` con usuario, IP, taskId, source.

### Paso 1.11 — Checklist Fase 1

- [ ] Botón "Gestor" solo con `EnableFileExport=true` Y rol permitido.
- [ ] Print/QR intactos.
- [ ] `ExportTasksPanel` lista, crea, edita, borra, pausa, ejecuta tareas.
- [ ] Wizard 5 pasos (Step 4 oculto). Modo edición funcional.
- [ ] Step 0 muestra campos+filtros declarados por el módulo, pre-rellenos desde `currentFilters`, con preview de 5 filas.
- [ ] Step 2 son **checkboxes independientes**; validación de ≥1 destino; "Carpeta local" deshabilitada solo si `AllowedExportFolders` vacío.
- [ ] Tarea solo-Email funciona aunque `AllowedExportFolders` esté vacío.
- [ ] XLSX, CSV, JSON, HTML, PNG generados en backend.
- [ ] PDF **no aparece** en el selector de formato del wizard.
- [ ] Tokens dinámicos resueltos correctamente al ejecutar.
- [ ] Email enviado OK (Papercut/MailHog local).
- [ ] Si `AllowedExportFolders` está configurado → path traversal bloqueado (403).
- [ ] Carpetas UNC (`\\server\share\`) funcionan si están en la lista permitida.
- [ ] Backend rechaza con 400 una tarea con `local` cuando `AllowedExportFolders` vacío.
- [ ] Audit log de cada operación CRUD + ejecución.
- [ ] `dotnet build` y `npm run build` sin warnings nuevos.

---

## Workflow — Fase 2 (Trigger PLC)

### Paso 2.1 — Habilitar Step 4

Step 4 con radio buttons (manual / cron / plc). Si selecciona "plc":
- Dropdown variable PLC bool (`/api/plc/variables?type=BOOL`).
- Checkbox "Continuar ejecutando si falla".

### Paso 2.2 — Hook en `PlcNotificationService`

[SW.PC.API.Backend_/Services/TwinCATService.cs](SW.PC.API.Backend_/Services/TwinCATService.cs) (o `PlcNotificationService`):
- Al iniciar, cargar todas las `ExportTask` con `ExecutionType="plc"` y `Enabled=true` del proyecto activo.
- Suscribirse a cada `PlcVariable` (bool).
- Mantener estado previo por variable; ejecutar `ExportService.RunAsync(task)` en flanco `false→true`.
- Actualizar `LastRunAt` y `LastResult`.

### Paso 2.3 — Checklist Fase 2

- [ ] Step 4 visible con opciones manual/cron/plc.
- [ ] Tareas PLC se cargan al arrancar backend.
- [ ] Flanco de subida dispara ejecución.
- [ ] Audit diferenciado manual / plc.
- [ ] Editar/pausar una tarea PLC actualiza la suscripción en caliente.

---

## Workflow — Fase 3 (Cron) — opcional

`ExportSchedulerService : BackgroundService` evalúa cada minuto las tareas `ExecutionType="cron"`. Step 4 ofrece presets (cada hora, cada turno, a medianoche, cron custom) + cuadro cron.

---

## Convenciones del proyecto

- **Multi-proyecto**: tareas, SMTP, `AllowedExportFolders` viven en el proyecto activo.
- **i18n**: en dev `t('clave', 'fallback ES')`; traducciones EN/IT/FR al final.
- **NO instalar nuevas libs**. Solo: `ClosedXML`, `System.Text.Json`, `Markdig`. **PDF excluido por compliance CRA/SBOM**.
- **SuperAdmin** existe en backend `[Authorize]` pero **no se muestra** en UI cliente.
- **Excel SystemConfig** keys lowercase en `ExcelConfigService.cs`.
- **Audit obligatorio** para CRUD de tareas y cada ejecución.
- **HTTPS (5001)** en producción.
- **No tocar** `wwwroot/static/js/main.*.js` (build output).

## Anti-patrones (NO hacer)

- ❌ Modificar handlers Print/QR existentes.
- ❌ Implementar/mostrar FTP/SFTP/WhatsApp.
- ❌ **Instalar nuevas librerías** (PDF, Excel u otras). PDF queda fuera del Gestor por CRA/SBOM.
- ❌ Añadir PDF como opción en el selector de formato del wizard.
- ❌ Generar archivos en el frontend para guardar (limita carpeta destino).
- ❌ Crear fallback automático de carpeta si `AllowedExportFolders` está vacío — debe fallar explícito.
- ❌ Aceptar `folder` sin validar contra `AllowedExportFolders`.
- ❌ Bloquear una tarea **solo-Email** por `AllowedExportFolders` vacío — la regla aplica solo si el destino `local` está activo.
- ❌ Que el wizard invente filtros o cargue opciones desde endpoints nuevos. Solo lo que el módulo declara inline.
- ❌ Ofrecer campos en Step 0 que la vista anfitriona no muestra/sabe pintar.
- ❌ Guardar SMTP/credenciales en `localStorage` o en estado del wizard.
- ❌ Mostrar SuperAdmin en UI de cliente.
- ❌ Ejecutar tareas sin registrar en audit.

## Recursos

- Modal anfitrión: [my-3d-app/src/components/ExportModal.js](my-3d-app/src/components/ExportModal.js)
- Patrón print iframe: [my-3d-app/src/components/PrintPreviewModal.js](my-3d-app/src/components/PrintPreviewModal.js)
- Feature flags: [my-3d-app/src/contexts/SystemFeaturesContext.js](my-3d-app/src/contexts/SystemFeaturesContext.js)
- Endpoint flags: [SW.PC.API.Backend_/Controllers/SystemFeaturesController.cs](SW.PC.API.Backend_/Controllers/SystemFeaturesController.cs)
- Modelo Excel: [SW.PC.API.Backend_/Models/ExcelModels.cs](SW.PC.API.Backend_/Models/ExcelModels.cs)
- Parser Excel: [SW.PC.API.Backend_/Services/ExcelConfigService.cs](SW.PC.API.Backend_/Services/ExcelConfigService.cs)
- DocumentExportService (refer histórica, NO usar): [SW.PC.API.Backend_/Services/DocumentExportService.cs](SW.PC.API.Backend_/Services/DocumentExportService.cs)
- API service frontend: [my-3d-app/src/services/api.js](my-3d-app/src/services/api.js)
- PLC service: [SW.PC.API.Backend_/Services/TwinCATService.cs](SW.PC.API.Backend_/Services/TwinCATService.cs)
- csproj backend (libs disponibles): [SW.PC.API.Backend_/SW.PC.API.Backend.csproj](SW.PC.API.Backend_/SW.PC.API.Backend.csproj)
