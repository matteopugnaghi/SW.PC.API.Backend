# Sistema de Gestión Documental (DMS)

> **Versión**: 1.0  
> **Fecha**: 2026-02-11  
> **Estado**: Diseño Aprobado — Implementación Pendiente  
> **Autor**: AI Architecture Team + Aquafrisch Engineering

---

## 📋 Índice

1. [Visión General](#1-visión-general)
2. [Arquitectura del Sistema](#2-arquitectura-del-sistema)
3. [Modelo de Datos](#3-modelo-de-datos)
4. [Categorías y Taxonomía Documental](#4-categorías-y-taxonomía-documental)
5. [Control de Acceso](#5-control-de-acceso)
6. [Flujo de Trabajo de Documentos](#6-flujo-de-trabajo-de-documentos)
7. [API Backend](#7-api-backend)
8. [Frontend - Vista Documental](#8-frontend---vista-documental)
9. [Versionado y Git](#9-versionado-y-git)
10. [Conversión de Formatos](#10-conversión-de-formatos)
11. [Integración CRA](#11-integración-cra)
12. [Escalabilidad Empresa](#12-escalabilidad-empresa)
13. [Plan de Implementación](#13-plan-de-implementación)

---

## 1. Visión General

### Problema
- Documentación actualmente son ficheros `.md` estáticos en el repositorio
- **Cumplimiento documental CRA: ~30%** — necesita llegar al 100% antes de Sept 2026
- No hay interfaz para visualizar/gestionar documentación
- No hay conversión MD ↔ Word/PDF
- No hay control de acceso por documento
- No hay versionado de documentos a nivel individual

### Solución: DMS en 2 Fases

```
┌─────────────────────────────────────────────────────┐
│ FASE 1: DMS de Proyecto (este SW)                    │
│ • Documentación por proyecto industrial              │
│ • Vista en sidebar del SW SCADA                      │
│ • MD como fuente de verdad + export Word/PDF         │
│ • Control de acceso por rol                          │
│ • Versionado vía Git integrado                       │
│ • Cumplimiento CRA completo                          │
└──────────────────────┬──────────────────────────────┘
                       │ Base reutilizable
                       ▼
┌─────────────────────────────────────────────────────┐
│ FASE 2: DMS Empresarial (futuro)                     │
│ • Documentación de toda la empresa                   │
│ • Multi-departamento con permisos granulares         │
│ • Integración IA para generación/análisis            │
│ • Workflows de aprobación                            │
│ • Firma digital de documentos                        │
└─────────────────────────────────────────────────────┘
```

### Principios de Diseño

| Principio | Descripción |
|-----------|-------------|
| **Markdown-first** | `.md` es la fuente de verdad — versionable, diffable, ligero |
| **Git-native** | Todo documento vive en el repo Git → historial completo |
| **Per-project** | Cada proyecto industrial tiene su carpeta `docs/` independiente |
| **Role-based** | Acceso controlado por el sistema RBAC existente |
| **Export-ready** | MD → Word (.docx) y MD → PDF bajo demanda |
| **Import-capable** | Word → MD para re-versionar; PDF como adjuntos binarios |
| **CRA-compliant** | Trazabilidad, integridad, auditoría de cada cambio |

---

## 2. Arquitectura del Sistema

### Diagrama de Componentes

```
┌─────────────────────────────────┐
│         React Frontend          │
│  ┌───────────────────────────┐  │
│  │   DocumentsView (sidebar) │  │
│  │  ┌─────────┬────────────┐ │  │
│  │  │ Tree    │ Viewer/    │ │  │
│  │  │ Browser │ Editor     │ │  │
│  │  │         │            │ │  │
│  │  │ • Folders│ • MD render│ │  │
│  │  │ • Search │ • Metadata │ │  │
│  │  │ • Tags  │ • History  │ │  │
│  │  │ • Filter│ • Export   │ │  │
│  │  └─────────┴────────────┘ │  │
│  └───────────────────────────┘  │
└──────────────┬──────────────────┘
               │ REST API + SignalR
               ▼
┌─────────────────────────────────┐
│       ASP.NET Core Backend      │
│  ┌────────────────────────────┐ │
│  │   DocumentsController.cs   │ │
│  │   • CRUD documentos        │ │
│  │   • Upload/Download        │ │
│  │   • Export Word/PDF        │ │
│  │   • Import Word→MD         │ │
│  │   • Historial versiones    │ │
│  └──────────┬─────────────────┘ │
│  ┌──────────▼─────────────────┐ │
│  │   DocumentService.cs       │ │
│  │   • Gestión ficheros       │ │
│  │   • Metadata DB            │ │
│  │   • Categorización         │ │
│  │   • Búsqueda full-text     │ │
│  └──────────┬─────────────────┘ │
│  ┌──────────▼─────────────────┐ │
│  │  DocumentConversionSvc.cs  │ │
│  │   • MD → HTML (Markdig)    │ │
│  │   • MD → DOCX              │ │
│  │   • MD → PDF               │ │
│  │   • DOCX → MD              │ │
│  └────────────────────────────┘ │
│  ┌────────────────────────────┐ │
│  │  Integración existente     │ │
│  │   • GitController (vers.)  │ │
│  │   • AuditLogService        │ │
│  │   • BackupService          │ │
│  │   • ProjectContextService  │ │
│  └────────────────────────────┘ │
└──────────────┬──────────────────┘
               │
        ┌──────▼──────┐
        │ File System  │
        │              │
        │ Projects/    │
        │  {id}/       │
        │   docs/      │  ← MD files + attachments
        │   data/      │  ← SQLite (metadata)
        └──────┬───────┘
               │
          Git Repository  ← Versionado automático
```

### Stack Tecnológico Adicional

| Componente | Librería | Propósito |
|-----------|----------|-----------|
| MD → HTML | **Markdig** (NuGet) | Renderizado Markdown en servidor |
| MD → PDF | **QuestPDF** (NuGet) | Generación PDF nativa .NET |
| MD → DOCX | **DocumentFormat.OpenXml** (NuGet) | Generación Word sin Office |
| DOCX → MD | **DocumentFormat.OpenXml** + parser custom | Importación Word a Markdown |
| Frontend MD | **react-markdown** + **remark-gfm** (npm) | Renderizado MD en browser |
| Frontend Editor | **@uiw/react-md-editor** (npm) | Editor Markdown WYSIWYG |
| Búsqueda | SQLite FTS5 | Full-text search en metadata + contenido |

---

## 3. Modelo de Datos

### 3.1 Estructura de Carpetas por Proyecto

```
Projects/{projectId}/
├── docs/                           ← NUEVO: Carpeta documental
│   ├── _metadata.json              ← Índice y metadata de documentos
│   │
│   ├── technical/                  ← Documentación técnica
│   │   ├── architecture/
│   │   ├── api/
│   │   └── installation/
│   │
│   ├── compliance/                 ← CRA y normativa
│   │   ├── risk-assessment/
│   │   ├── annexes/
│   │   └── declarations/
│   │
│   ├── user-guides/                ← Manuales de usuario
│   │   ├── operator/
│   │   └── maintenance/
│   │
│   ├── electrical/                 ← Esquemas eléctricos (PDF adjuntos)
│   │   └── schematics/
│   │
│   ├── maintenance/                ← Procedimientos mantenimiento
│   │
│   └── _attachments/               ← Binarios (PDF, imágenes, etc.)
│       ├── electrical-schema-v2.pdf
│       └── plant-photo-001.jpg
│
├── config/
├── models/
├── data/
│   └── project.db                  ← Tabla Documents para metadata
└── ...
```

### 3.2 Tabla SQLite: `Documents`

```sql
CREATE TABLE Documents (
    -- Identificación
    Id              TEXT PRIMARY KEY,          -- GUID
    Slug            TEXT NOT NULL UNIQUE,      -- URL-friendly: "manual-operador-v2"
    
    -- Contenido
    Title           TEXT NOT NULL,             -- "Manual del Operador"
    Description     TEXT,                      -- Resumen breve
    FilePath        TEXT NOT NULL,             -- Ruta relativa: "user-guides/operator/manual.md"
    FileType        TEXT NOT NULL DEFAULT 'md',-- 'md', 'pdf', 'docx', 'png', 'jpg'
    ContentHash     TEXT,                      -- SHA256 del contenido actual
    
    -- Categorización
    Category        TEXT NOT NULL,             -- 'technical', 'compliance', 'user-guide', etc.
    SubCategory     TEXT,                      -- Sub-categoría libre
    Tags            TEXT,                      -- JSON array: ["cra","seguridad","plc"]
    
    -- Control de acceso
    AccessLevel     TEXT NOT NULL DEFAULT 'all',  -- 'public','operator','maintenance','admin','internal'
    MinimumRole     TEXT NOT NULL DEFAULT 'Viewer', -- SystemRole mínimo requerido
    
    -- Versionado
    Version         TEXT NOT NULL DEFAULT '1.0',   -- Versión semántica del documento
    Status          TEXT NOT NULL DEFAULT 'draft',  -- 'draft','review','approved','archived','obsolete'
    
    -- CRA / Compliance
    CraRelevant     INTEGER DEFAULT 0,         -- ¿Relevante para CRA?
    CraArticle      TEXT,                      -- "Art. 13", "Annex VII", etc.
    ApprovedBy      TEXT,                      -- Quién aprobó
    ApprovedAt      TEXT,                      -- Fecha aprobación ISO 8601
    
    -- Auditoría
    CreatedBy       TEXT NOT NULL,
    CreatedAt       TEXT NOT NULL,
    UpdatedBy       TEXT,
    UpdatedAt       TEXT,
    
    -- Relaciones
    ParentDocId     TEXT,                      -- Documento padre (para jerarquía)
    RelatedDocIds   TEXT,                      -- JSON array de IDs relacionados
    
    -- Búsqueda
    SearchContent   TEXT                       -- Contenido indexable (texto plano del MD)
);

-- Índices para búsqueda rápida
CREATE INDEX idx_documents_category ON Documents(Category);
CREATE INDEX idx_documents_status ON Documents(Status);
CREATE INDEX idx_documents_access ON Documents(AccessLevel, MinimumRole);
CREATE INDEX idx_documents_cra ON Documents(CraRelevant);

-- Full-Text Search (FTS5)
CREATE VIRTUAL TABLE DocumentsFTS USING fts5(
    Title, Description, SearchContent, Tags,
    content=Documents,
    content_rowid=rowid
);
```

### 3.3 Tabla SQLite: `DocumentHistory`

```sql
CREATE TABLE DocumentHistory (
    Id              TEXT PRIMARY KEY,
    DocumentId      TEXT NOT NULL,
    Version         TEXT NOT NULL,
    Action          TEXT NOT NULL,       -- 'created','edited','approved','exported','imported'
    ChangedBy       TEXT NOT NULL,
    ChangedAt       TEXT NOT NULL,
    CommitHash      TEXT,                -- Git commit hash si aplica
    ContentHash     TEXT,                -- SHA256 en ese momento
    ChangeNote      TEXT,                -- Nota del cambio
    PreviousContent TEXT,                -- Contenido anterior (para diff)
    
    FOREIGN KEY (DocumentId) REFERENCES Documents(Id)
);

CREATE INDEX idx_dochistory_doc ON DocumentHistory(DocumentId);
CREATE INDEX idx_dochistory_date ON DocumentHistory(ChangedAt);
```

### 3.4 Modelo C# (`Models/DocumentModels.cs`)

```csharp
// Categorías de documentos
public enum DocumentCategory
{
    Technical,          // Documentación técnica del SW
    Compliance,         // CRA, normativa, seguridad
    UserGuide,          // Manuales para operadores
    Maintenance,        // Procedimientos de mantenimiento
    Electrical,         // Esquemas eléctricos (adjuntos)
    Configuration,      // Guías de configuración
    Internal,           // Documentación interna (no compartir)
    Other
}

// Niveles de acceso documental
public enum DocumentAccessLevel
{
    Public,             // Cualquier usuario autenticado
    Operator,           // Operador y superiores
    Maintenance,        // Mantenimiento y superiores
    Admin,              // Solo Admin/SuperAdmin
    Internal            // Solo SuperAdmin (Aquafrisch)
}

// Estados del documento
public enum DocumentStatus
{
    Draft,              // Borrador en edición
    Review,             // En revisión
    Approved,           // Aprobado para uso
    Archived,           // Archivado (histórico)
    Obsolete            // Obsoleto (reemplazado)
}
```

---

## 4. Categorías y Taxonomía Documental

### Estructura de Categorías Orientada a CRA

```
📁 Documentación del Proyecto
├── 📂 Técnica (technical/)
│   ├── Arquitectura del Sistema
│   ├── API Reference
│   ├── Guía de Instalación
│   ├── Guía de Desarrollo
│   └── Especificaciones Técnicas
│
├── 📂 Cumplimiento/Compliance (compliance/)
│   ├── 🔒 Evaluación de Riesgos        ← CRA Art. 13(2)
│   ├── 🔒 Documentación Técnica         ← CRA Anexo VII
│   ├── 🔒 Manual Seguridad Usuario      ← CRA Anexo II
│   ├── 🔒 Declaración UE Conformidad    ← CRA Art. 28
│   ├── 🔒 SBOM                          ← CRA Art. 13(5)
│   ├── 🔒 Gestión Vulnerabilidades      ← CRA Art. 14
│   └── 🔒 Auditoría de Seguridad        ← CRA Art. 13
│
├── 📂 Guías de Usuario (user-guides/)
│   ├── Manual del Operador
│   ├── Manual de Mantenimiento
│   ├── Guía de Recuperación
│   └── FAQ / Troubleshooting
│
├── 📂 Eléctrico (electrical/)
│   ├── Esquemas de Potencia (PDF)
│   ├── Esquemas de Control (PDF)
│   └── Lista de Cables
│
├── 📂 Mantenimiento (maintenance/)
│   ├── Procedimientos Preventivos
│   ├── Procedimientos Correctivos
│   └── Checklist de Mantenimiento
│
├── 📂 Configuración (configuration/)
│   ├── Configuración Excel
│   ├── Configuración PLC
│   └── Parámetros del Sistema
│
└── 📂 Interno (internal/) ⚠️ Solo Aquafrisch
    ├── Credenciales
    ├── Procesos Internos
    └── Vocabulario Máquina
```

### Mapeo CRA → Documentos

| Artículo CRA | Documento Requerido | Estado | Prioridad |
|--------------|---------------------|--------|-----------|
| Art. 13(2) | Evaluación Riesgos Ciberseguridad | 🔴 Pendiente | **ALTA** (Mar 2026) |
| Anexo VII | Documentación Técnica Completa | 🔴 Pendiente | **ALTA** (Jun 2026) |
| Anexo II | Manual Seguridad Usuario | 🔴 Pendiente | **ALTA** (Jun 2026) |
| Art. 28 | Declaración UE Conformidad | 🔴 Pendiente | **MEDIA** (Sept 2026) |
| Art. 13(5) | SBOM | ✅ Implementado | — |
| Art. 14 | Proceso Notificación ENISA | ⏳ Esperando (Sept 2026) | — |

---

## 5. Control de Acceso

### Matriz de Permisos por Rol

| Permiso → | View | Create | Edit | Delete | Export | Execute¹ |
|-----------|------|--------|------|--------|--------|----------|
| **SuperAdmin** | ✅ Todos | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Administrator** | ✅ Todos²| ✅ | ✅ | ✅ | ✅ | ✅ |
| **Maintenance** | ✅ Tech+Maint+User | ✅ | ✅ | ❌ | ✅ | ❌ |
| **Operator** | ✅ UserGuides+Maint | ❌ | ❌ | ❌ | ✅ | ❌ |
| **Viewer** | ✅ UserGuides | ❌ | ❌ | ❌ | ✅ | ❌ |
| **Auditor** | ✅ Compliance+Audit | ❌ | ❌ | ❌ | ✅ | ❌ |

¹ Execute = Aprobar documentos, importar/convertir  
² Todos excepto `internal/` (solo SuperAdmin)

### Integración con Sistema RBAC Existente

Se añade un nuevo módulo `DocumentsView` al sistema de permisos:

```csharp
// En ModulePermissions (RolePermissions.cs)
public ViewPermission DocumentsView { get; set; } = new();
```

Y el `DocumentAccessLevel` del documento actúa como **segundo filtro**:

```
¿Puede ver? = canView('DocumentsView') 
              AND userRole >= document.MinimumRole
              AND (document.AccessLevel != 'Internal' OR userRole == 'SuperAdmin')
```

---

## 6. Flujo de Trabajo de Documentos

### 6.1 Crear/Editar Documento

```
┌──────────┐    ┌─────────────┐    ┌──────────┐    ┌─────────┐
│ Frontend  │───▶│ Editor MD   │───▶│ Save API │───▶│ Disco   │
│ (sidebar) │    │ (WYSIWYG)   │    │ POST/PUT │    │ .md file│
└──────────┘    └─────────────┘    └────┬─────┘    └────┬────┘
                                        │               │
                                   ┌────▼─────┐    ┌────▼────┐
                                   │ Metadata  │    │ Git     │
                                   │ DB update │    │ commit  │
                                   └──────────┘    └─────────┘
```

### 6.2 Exportar a Word/PDF

```
┌──────────┐    ┌──────────┐    ┌──────────────┐    ┌──────────┐
│ Usuario   │───▶│ Export   │───▶│ Conversion   │───▶│ Download │
│ click     │    │ API      │    │ Service      │    │ .docx/.pdf│
│ "Export"  │    │ GET      │    │ MD→DOCX/PDF  │    │          │
└──────────┘    └──────────┘    └──────────────┘    └──────────┘
```

### 6.3 Importar Word → MD

```
┌──────────┐    ┌──────────┐    ┌──────────────┐    ┌──────────┐
│ Técnico   │───▶│ Upload   │───▶│ Conversion   │───▶│ Review   │
│ upload    │    │ API      │    │ Service      │    │ diff     │
│ .docx     │    │ POST     │    │ DOCX→MD      │    │ + merge  │
└──────────┘    └──────────┘    └──────────────┘    └──────────┘
```

### 6.4 Adjuntar PDF (esquemas eléctricos)

```
┌──────────┐    ┌──────────┐    ┌──────────────┐    ┌──────────┐
│ Técnico   │───▶│ Upload   │───▶│ Store in     │───▶│ Register │
│ upload    │    │ API      │    │ _attachments/│    │ metadata │
│ .pdf      │    │ POST     │    │ + Git track  │    │ + index  │
└──────────┘    └──────────┘    └──────────────┘    └──────────┘
```

### 6.5 Ciclo de Vida del Documento

```
  Draft ──────▶ Review ──────▶ Approved ──────▶ Archived
    │              │              │                 │
    │              │              │                 │
    ▼              ▼              ▼                 ▼
  Editable    Editable       Read-only         Read-only
  por autor   por revisor    (nueva versión    (histórico)
                              para cambios)
                                   │
                                   ▼
                               Obsolete
                              (reemplazado)
```

---

## 7. API Backend

### Endpoints del DocumentsController

```
/api/documents
├── GET    /                           → Lista documentos (filtros: category, status, access, tag, search)
├── GET    /{id}                       → Detalle documento + metadata
├── GET    /{id}/content               → Contenido raw del MD
├── GET    /{id}/render                → Contenido renderizado HTML
├── POST   /                           → Crear nuevo documento
├── PUT    /{id}                       → Actualizar documento
├── DELETE /{id}                       → Eliminar documento (soft delete → Archived)
│
├── GET    /{id}/history               → Historial de versiones
├── GET    /{id}/history/{historyId}   → Versión específica
├── POST   /{id}/revert/{historyId}    → Revertir a versión anterior
│
├── POST   /{id}/export/pdf            → Exportar a PDF
├── POST   /{id}/export/docx           → Exportar a Word
├── GET    /{id}/export/md             → Descargar MD raw
│
├── POST   /import/docx               → Importar Word → MD
├── POST   /import/pdf                 → Adjuntar PDF como documento
│
├── POST   /{id}/approve               → Aprobar documento (cambiar status)
├── POST   /{id}/archive               → Archivar documento
│
├── GET    /categories                  → Lista categorías disponibles
├── GET    /tags                        → Lista tags usados
├── GET    /stats                       → Estadísticas documentales
├── GET    /cra-status                  → Estado cumplimiento CRA documental
│
├── POST   /upload-attachment           → Subir adjunto (imagen, PDF, etc.)
├── GET    /attachments/{filename}      → Servir adjunto
│
└── GET    /tree                        → Árbol de carpetas/documentos
```

### Servicios Backend

```csharp
// Servicio principal de documentos
public interface IDocumentService
{
    // CRUD
    Task<IEnumerable<DocumentInfo>> ListDocumentsAsync(DocumentFilter filter);
    Task<DocumentDetail> GetDocumentAsync(string id);
    Task<string> GetDocumentContentAsync(string id);
    Task<string> RenderDocumentAsync(string id);    // MD → HTML
    Task<DocumentInfo> CreateDocumentAsync(CreateDocumentRequest request, string userId);
    Task<DocumentInfo> UpdateDocumentAsync(string id, UpdateDocumentRequest request, string userId);
    Task DeleteDocumentAsync(string id, string userId);
    
    // Versioning
    Task<IEnumerable<DocumentHistoryEntry>> GetHistoryAsync(string id);
    Task RevertToVersionAsync(string id, string historyId, string userId);
    
    // Workflow
    Task<DocumentInfo> ApproveDocumentAsync(string id, string userId);
    Task<DocumentInfo> ArchiveDocumentAsync(string id, string userId);
    
    // Search & Navigation
    Task<IEnumerable<DocumentInfo>> SearchAsync(string query);
    Task<DocumentTree> GetDocumentTreeAsync(string userRole);
    Task<DocumentStats> GetStatsAsync();
    Task<CraDocumentStatus> GetCraStatusAsync();
    
    // Import/Export
    Task<byte[]> ExportToPdfAsync(string id);
    Task<byte[]> ExportToDocxAsync(string id);
    Task<DocumentInfo> ImportFromDocxAsync(Stream docxStream, string category, string userId);
    Task<DocumentInfo> ImportPdfAttachmentAsync(Stream pdfStream, string filename, string category, string userId);
    
    // Sync
    Task SyncMetadataFromDiskAsync();   // Escanear docs/ y actualizar DB
    Task<string> CommitChangesAsync(string message, string userId);
}

// Servicio de conversión de formatos
public interface IDocumentConversionService
{
    Task<string> MarkdownToHtmlAsync(string markdown);
    Task<byte[]> MarkdownToPdfAsync(string markdown, DocumentMetadata meta);
    Task<byte[]> MarkdownToDocxAsync(string markdown, DocumentMetadata meta);
    Task<string> DocxToMarkdownAsync(Stream docxStream);
}
```

---

## 8. Frontend - Vista Documental

### 8.1 Nuevo Item en EpicSideMenu

```javascript
{
    id: 'documentos',
    icon: '📄',
    labelKey: 'menu.documentos',
    color: '#4ecdc4',
    visible: canView('DocumentsView')
}
```

### 8.2 Layout de la Vista

```
┌─────────────────────────────────────────────────────────┐
│  📄 Gestión Documental                    🔍 Buscar... │
├────────────────┬────────────────────────────────────────┤
│                │                                        │
│  📁 Técnica    │  📄 Manual del Operador               │
│  ├── Arquit.   │  ─────────────────────────             │
│  ├── API       │  **Estado**: ✅ Aprobado               │
│  └── Install.  │  **Versión**: 2.1                     │
│                │  **CRA**: Art. Anexo II                │
│  📁 Compliance │  **Última mod.**: 2026-02-01           │
│  ├── Riesgos   │  ─────────────────────────             │
│  ├── Anexo VII │                                        │
│  └── SBOM      │  # Contenido del Documento             │
│                │                                        │
│  📁 User Guide │  Este manual describe el               │
│  ├── Operador  │  procedimiento de operación...         │
│  └── Manten.   │                                        │
│                │  ## Sección 1: Arranque                 │
│  📁 Eléctrico  │  1. Verificar alimentación             │
│  └── Esquemas  │  2. Pulsar botón START                 │
│                │  3. Esperar indicador verde             │
│  📁 Interno ⚠️ │                                        │
│                │                                        │
├────────────────┤  ─────────────────────────             │
│ 📊 Estado CRA  │  [📥 Word] [📥 PDF] [✏️ Editar]       │
│ ████████░░ 65% │  [🕐 Historial] [✅ Aprobar]          │
│ 4/7 docs ready │                                        │
└────────────────┴────────────────────────────────────────┘
```

### 8.3 Componentes React

```
src/
├── views/
│   └── DocumentsView.js              ← Vista principal (con withPermission)
│
├── components/documents/
│   ├── DocumentTree.js                ← Árbol navegación izquierda
│   ├── DocumentViewer.js              ← Renderizado MD + metadata
│   ├── DocumentEditor.js              ← Editor MD (WYSIWYG)
│   ├── DocumentHistory.js             ← Historial de versiones
│   ├── DocumentExportMenu.js          ← Botones exportar Word/PDF
│   ├── DocumentImportDialog.js        ← Diálogo importar Word/PDF
│   ├── DocumentMetadataPanel.js       ← Panel lateral con metadata
│   ├── DocumentSearchBar.js           ← Búsqueda full-text
│   ├── DocumentCraStatus.js           ← Widget estado CRA
│   ├── DocumentCreateDialog.js        ← Crear nuevo documento
│   └── DocumentStatusBadge.js         ← Badge: draft/review/approved
│
├── services/
│   └── documentService.js             ← API client para /api/documents
│
└── styles/
    └── DocumentsView.css              ← Estilos específicos
```

---

## 9. Versionado y Git

### Estrategia de Versionado

Cada documento tiene **dos niveles de versión**:

1. **Versión semántica del documento** (campo `Version`): `1.0`, `1.1`, `2.0`
   - Controlada manualmente por el autor
   - Cambios menores: `1.0 → 1.1`
   - Cambios mayores: `1.1 → 2.0`

2. **Historial Git** (automático): Cada save = commit
   - Commit message autogenerado: `docs({category}): update {title} v{version}`
   - Hash del commit guardado en `DocumentHistory`
   - Diff disponible entre cualquier par de versiones

### Integración con GitController Existente

```
DocumentService.UpdateDocumentAsync()
    │
    ├── 1. Guardar .md en disco
    ├── 2. Actualizar metadata en DB
    ├── 3. Calcular SHA256 nuevo
    ├── 4. Crear entrada en DocumentHistory
    ├── 5. Git add + commit (via IGitOperationsService)
    └── 6. Log en AuditService
```

### Git Hooks para Documentos

El `DocumentService` utiliza el `IGitOperationsService` existente:

```csharp
// Auto-commit cada cambio
await _gitService.CommitAsync("backend", 
    $"docs({doc.Category}): {action} '{doc.Title}' v{doc.Version}",
    username);
```

---

## 10. Conversión de Formatos

### MD → PDF (QuestPDF)

```csharp
// Pipeline: MD → Markdig → HTML AST → QuestPDF Document
var html = Markdig.Markdown.ToHtml(markdownContent);
// Luego mapear elementos HTML a componentes QuestPDF
```

Incluye:
- Cabecera con logo Aquafrisch + nombre proyecto
- Pie de página con fecha, versión, número de página
- Tabla de contenidos automática
- Estilos corporativos consistentes

### MD → DOCX (OpenXml)

```csharp
// Pipeline: MD → Markdig AST → OpenXml Paragraphs
var document = Markdig.Markdown.Parse(markdownContent);
// Mapear cada bloque (heading, paragraph, list, table, code) a OpenXml
```

Incluye:
- Template corporativo con estilos predefinidos
- Cabecera/pie corporativo
- Tabla de contenidos actualizable en Word
- Imágenes embebidas

### DOCX → MD (Import)

```csharp
// Pipeline: OpenXml → Parse paragraphs → Generate MD
using var doc = WordprocessingDocument.Open(stream, false);
var body = doc.MainDocumentPart.Document.Body;
// Recorrer cada paragraph y mapear estilo → MD syntax
```

Preserva:
- Headings (H1-H6) por estilo de Word
- Listas (bullet, numbered)
- Tablas
- Negrita, cursiva, código
- Imágenes (extraídas a `_attachments/`)

### PDF → Attachment

Los PDFs (como esquemas eléctricos) **no se convierten a MD** — se almacenan como adjuntos binarios con un documento MD "wrapper" que contiene:

```markdown
# Esquema Eléctrico de Potencia v2.1

- **Archivo**: [electrical-schema-v2.pdf](./_attachments/electrical-schema-v2.pdf)
- **Fecha**: 2026-01-15
- **Versión**: 2.1
- **Descripción**: Esquema completo del cuadro de potencia principal

> Este documento es un adjunto PDF. Descárgalo para visualizarlo.
```

---

## 11. Integración CRA

### Dashboard CRA en la Vista Documental

Widget permanente en el panel izquierdo que muestra:

```
📊 Cumplimiento CRA Documental
━━━━━━━━━━━━━━━━━━━━━━━━━━━━

✅ SBOM (Art. 13.5)              Completo
✅ Gestión Vulnerabilidades       Completo  
✅ Logs de Auditoría             Completo
⚠️ Evaluación Riesgos (Art.13.2) Borrador  → Mar 2026
🔴 Doc. Técnica (Anexo VII)      Pendiente → Jun 2026
🔴 Manual Seguridad (Anexo II)   Pendiente → Jun 2026
🔴 Declaración UE (Art. 28)      Pendiente → Sept 2026

Progreso: ████████░░░░░░░ 43%
```

### Auto-tracking de Documentos CRA

Documentos marcados con `CraRelevant = true` y `CraArticle` se rastrean automáticamente. Cuando cambian de status (`Draft → Approved`), el progreso CRA se recalcula.

---

## 12. Escalabilidad Empresa

### Diseño para Fase 2 (futuro)

El sistema se diseña con estas abstracciones para facilitar la escalación:

| Concepto Fase 1 | Escalación Fase 2 |
|------------------|-------------------|
| `ProjectId` scope | `OrganizationId` + `DepartmentId` + `ProjectId` |
| Roles SCADA | Roles empresa (Director, Ingeniero, Calidad, etc.) |
| SQLite local | PostgreSQL centralizado |
| Git local | GitLab/GitHub empresa |
| Exportar PDF | Generación automática con templates |
| Manual categorías | Categorías por departamento + herencia |
| Sin workflows | Workflows de aprobación multi-nivel |
| Sin firma digital | Firma digital cualificada (eIDAS) |
| Sin IA | IA para generación, análisis, traducción |

### Interfaces Reutilizables

```csharp
// Interfaz genérica que se reutilizará en Fase 2
public interface IDocumentRepository
{
    Task<IEnumerable<DocumentInfo>> QueryAsync(DocumentFilter filter);
    Task<DocumentDetail> GetByIdAsync(string id);
    Task<string> SaveAsync(DocumentInfo doc, string content);
    Task DeleteAsync(string id);
}

// Fase 1: Implementación SQLite + FileSystem
public class ProjectDocumentRepository : IDocumentRepository { ... }

// Fase 2: Implementación PostgreSQL + S3/MinIO
// public class EnterpriseDocumentRepository : IDocumentRepository { ... }
```

---

## 13. Plan de Implementación

### Sprint 1: Infraestructura Base (1-2 semanas)

| # | Tarea | Prioridad |
|---|-------|-----------|
| 1.1 | Añadir `DocsPath` a `ProjectPaths`, `IProjectContextService`, `IRequestProjectContext` | 🔴 |
| 1.2 | Crear carpeta `docs/` en template de proyecto + proyecto activo | 🔴 |
| 1.3 | Crear modelos C#: `DocumentModels.cs` (Document, DocumentHistory, enums) | 🔴 |
| 1.4 | Crear migración DB: tablas `Documents` + `DocumentHistory` + FTS5 | 🔴 |
| 1.5 | Instalar NuGet: `Markdig` | 🔴 |
| 1.6 | Añadir `DocumentsView` al sistema de permisos (defaults por rol) | 🔴 |

### Sprint 2: Servicio + API Core (1-2 semanas)

| # | Tarea | Prioridad |
|---|-------|-----------|
| 2.1 | Crear `IDocumentService` + `DocumentService.cs` (CRUD, search, tree) | 🔴 |
| 2.2 | Crear `DocumentsController.cs` (endpoints CRUD + tree + search) | 🔴 |
| 2.3 | Implementar sincronización disco → DB (`SyncMetadataFromDiskAsync`) | 🔴 |
| 2.4 | Integrar con `IAuditLogService` | 🟡 |
| 2.5 | Implementar `MD → HTML` con Markdig | 🔴 |

### Sprint 3: Frontend Vista Básica (1-2 semanas)

| # | Tarea | Prioridad |
|---|-------|-----------|
| 3.1 | Crear `DocumentsView.js` + ruta en App.js | 🔴 |
| 3.2 | Añadir item `documentos` al `EpicSideMenu.js` | 🔴 |
| 3.3 | Instalar npm: `react-markdown`, `remark-gfm` | 🔴 |
| 3.4 | Crear `DocumentTree.js` (navegación por categorías) | 🔴 |
| 3.5 | Crear `DocumentViewer.js` (renderizado MD) | 🔴 |
| 3.6 | Crear `DocumentMetadataPanel.js` | 🟡 |
| 3.7 | Crear `documentService.js` (API client) | 🔴 |
| 3.8 | Crear `DocumentsView.css` | 🔴 |
| 3.9 | Añadir i18n keys (ES/EN) | 🟡 |

### Sprint 4: Editor + Versionado (1-2 semanas)

| # | Tarea | Prioridad |
|---|-------|-----------|
| 4.1 | Instalar npm: `@uiw/react-md-editor` | 🔴 |
| 4.2 | Crear `DocumentEditor.js` (editor WYSIWYG) | 🔴 |
| 4.3 | Crear `DocumentHistory.js` (historial) | 🟡 |
| 4.4 | Integrar Git auto-commit en DocumentService | 🟡 |
| 4.5 | Crear `DocumentCreateDialog.js` | 🔴 |
| 4.6 | Crear `DocumentStatusBadge.js` + workflow approval | 🟡 |

### Sprint 5: Conversión + Import/Export (2 semanas)

| # | Tarea | Prioridad |
|---|-------|-----------|
| 5.1 | Instalar NuGet: `QuestPDF`, `DocumentFormat.OpenXml` | 🔴 |
| 5.2 | Crear `IDocumentConversionService` + implementación | 🔴 |
| 5.3 | MD → PDF con template corporativo | 🔴 |
| 5.4 | MD → DOCX con estilos corporativos | 🔴 |
| 5.5 | DOCX → MD (import) | 🟡 |
| 5.6 | PDF attachment upload + registro | 🟡 |
| 5.7 | Frontend: `DocumentExportMenu.js` | 🔴 |
| 5.8 | Frontend: `DocumentImportDialog.js` | 🟡 |

### Sprint 6: CRA + Búsqueda + Polish (1-2 semanas)

| # | Tarea | Prioridad |
|---|-------|-----------|
| 6.1 | Widget `DocumentCraStatus.js` | 🔴 |
| 6.2 | `DocumentSearchBar.js` con FTS5 | 🟡 |
| 6.3 | Migrar documentos CRA existentes al DMS | 🔴 |
| 6.4 | Incluir `docs/` en BackupService | 🔴 |
| 6.5 | Testing end-to-end | 🔴 |
| 6.6 | Documentación del propio DMS | 🟡 |

### Tiempo Estimado Total: 8-12 semanas

```
Sprint 1 ████ Infraestructura
Sprint 2 ████ Backend API
Sprint 3 ████ Frontend Base
Sprint 4 ████ Editor + Versiones
Sprint 5 ██████ Conversión Formatos
Sprint 6 ████ CRA + Polish

──────────────────────────────────────►
Sem 1-2   Sem 3-4   Sem 5-6   Sem 7-10
```

---

## Dependencias NuGet a Instalar

```xml
<!-- En SW.PC.API.Backend.csproj -->
<PackageReference Include="Markdig" Version="0.37.0" />
<PackageReference Include="QuestPDF" Version="2024.12.0" />
<PackageReference Include="DocumentFormat.OpenXml" Version="3.2.0" />
```

## Dependencias npm a Instalar

```json
// En my-3d-app/package.json
"react-markdown": "^9.0.0",
"remark-gfm": "^4.0.0",
"rehype-highlight": "^7.0.0",
"@uiw/react-md-editor": "^4.0.0"
```

---

> **Siguiente paso**: Pásame el fichero `.md` con la estructura documental de la empresa que mencionaste. Lo integraré en la taxonomía de categorías (Sección 4) y adaptaré el diseño para que sea compatible con la Fase 2 empresarial.
