# DMS — Propuesta de Estructura Documental Multi-Normativa

> **Fecha**: 2026-02-13 (actualizado 2026-02-15)  
> **Estado**: Propuesta (sin cambios de código)  
> **Autor**: Equipo Aquafrisch Supervisor  
> **Aplica a**: AQSdocs_master (Software) + AQSdocs_project (Proyecto)

---

## 0. Resumen Ejecutivo — La Idea en 5 Segundos

**7 cajones. Cada uno responde una pregunta.**

```
🏭 AQUAFRISCH — Gestión Documental
│
├── 📊 01 CALIDAD            ← ¿Hacemos las cosas bien?
│   └── Política · Procesos · Auditorías · No Conformidades · Dirección · Mejora
│
├── 🔒 02 SEGURIDAD          ← ¿Estamos protegidos?
│   └── Políticas · Riesgos · Controles · CRA Europa · Industrial · Vulnerabilidades
│
├── 📦 03 PRODUCTO            ← ¿Qué hemos construido?
│   └── Arquitectura · Desarrollo Seguro · Especificaciones · SBOM · Testing
│
├── ⚡ 04 INGENIERÍA          ← ¿Cómo está diseñado?
│   └── Eléctricos · Control · Red
│
├── 🔩 05 OPERACIONES         ← ¿Cómo se mantiene?
│   └── Preventivo · Correctivo · Emergencia · Registros
│
├── 📖 06 MANUALES            ← ¿Cómo se usa?
│   └── Manual · Instalación · Formación · FAQ
│
└── 📋 07 PROYECTO            ← ¿Qué le entregamos al cliente?
    └── Contrato · Plan · Entregables · Aceptación · Post-Venta
```

### ¿Por qué estas 7 carpetas?

Porque cada una responde a las normativas que nos exigen:

```
NORMATIVA              CARPETA PRINCIPAL        TAMBIÉN APLICA A
─────────────────────  ─────────────────────    ──────────────────────
ISO 9001 (Calidad)     📊 01 CALIDAD    ======► 03 Producto, 05 Operaciones, 07 Proyecto
ISO 27001 (Seguridad)  🔒 02 SEGURIDAD  ======► 04 Ingeniería
IEC 62443 (Industrial) 🔒 02 SEGURIDAD  ======► 03 Producto, 04 Ingeniería
EU CRA (Ley Europea)   🔒 02 SEGURIDAD  ======► 03 Producto, 06 Manuales, 07 Proyecto
```

> **Ventaja**: Cuando venga un auditor de ISO 9001, va directamente a carpeta 01. Cuando venga uno de ciberseguridad (ISO 27001 / IEC 62443 / CRA), va a carpeta 02. Todo tiene su sitio.

---

## 1. Estado Actual del DMS

### Categorías actuales (7, planas)

| ID | Categoría | Icono | Color | Carpeta | Alcance |
|----|-----------|-------|-------|---------|---------|
| 0 | Compliance CRA | 📋 | `#ef4444` | `compliance` | Normativo |
| 1 | CRA Genérico (SW) | 🇪🇺 | `#3b82f6` | `cra-generic` | Normativo |
| 2 | Manuales de Usuario | 📖 | `#10b981` | `user-guides` | Producto |
| 3 | Documentación Técnica | 🔧 | `#f59e0b` | `technical` | Producto |
| 4 | Esquemas Eléctricos | ⚡ | `#8b5cf6` | `electrical` | Ingeniería |
| 5 | Mantenimiento | 🔩 | `#06b6d4` | `maintenance` | Operaciones |
| 7 | Otros | 📄 | `#9ca3af` | *(vacío)* | General |

Todas son `IsSystem=true`, `DefaultClassificationId=0`, `DefaultMinimumRole='Visualizador'`.

### Funcionalidades ya implementadas

- **Campos normativos por documento**: CRA (`CraRelevant`, `CraArticle`, `CraDeadline`), ISO 27001 (`Iso27001Relevant`, `Iso27001Article`), IEC 62443 (`Iec62443Relevant`, `Iec62443Article`)
- **Clasificación ISO 27001 A.8.2**: 4 niveles (Público, Interno, Confidencial, Restringido)
- **Matriz de acceso**: Roles × Categorías con permisos CanRead/CanWrite
- **Subcategorías**: Soporte `ParentId` en `DocumentCategoryConfig`
- **Scopes**: Software (master) y Project (por proyecto)
- **Estados**: Draft → Review → Approved → Archived / Obsolete
- **Panel de Auditoría**: KPIs + compliance CRA/ISO 27001/IEC 62443
- **Control de versiones**: `DocumentHistories` con historial completo

### Lo que falta

- **ISO 9001** no está cubierta (sin campos en el modelo)
- Las categorías CRA son redundantes (0 y 1 solapan)
- No hay jerarquía real (todo plano)
- No hay trazabilidad cruzada entre normas
- No hay auto-tags normativos por categoría

---

## 2. Marco Normativo — Qué Exige Cada Estándar

### ISO 9001:2015 — Sistema de Gestión de Calidad

| Cláusula | Título | Documentación Requerida |
|----------|--------|-------------------------|
| §4.3 | Alcance del SGC | Documento de alcance |
| §4.4 | SGC y sus procesos | Mapa de procesos, procedimientos |
| §5.2 | Política de calidad | Política documentada y comunicada |
| §6.1 | Riesgos y oportunidades | Registro de riesgos |
| §7.2 | Competencia | Registros de formación |
| §7.5 | Información documentada | Procedimiento de control documental |
| §8.2 | Requisitos productos/servicios | Contratos, especificaciones cliente |
| §8.3 | Diseño y desarrollo | Especificaciones, revisiones de diseño |
| §8.4 | Proveedores externos | Evaluación de proveedores |
| §8.5 | Producción | Procedimientos operativos, mantenimiento |
| §8.6 | Liberación | Criterios de aceptación, registros |
| §9.1 | Seguimiento y medición | KPIs, análisis de datos |
| §9.2 | Auditoría interna | Programa, informes, hallazgos |
| §9.3 | Revisión por dirección | Actas, decisiones |
| §10.2 | No conformidad | Registros NC, acciones correctivas |
| §10.3 | Mejora continua | Planes de mejora |

### ISO 27001:2022 — Seguridad de la Información

| Control | Título | Relevancia DMS |
|---------|--------|----------------|
| A.5.1-A.5.37 | Políticas de seguridad | Documentar y comunicar políticas |
| A.6.3 | Concienciación y formación | Registros de capacitación |
| A.8.2 | Clasificación de la información | Niveles: Público, Interno, Confidencial, Restringido |
| A.8.3 | Restricción de acceso | Matriz de acceso por roles |
| A.8.8 | Gestión de vulnerabilidades técnicas | Registro y seguimiento |
| A.8.20 | Seguridad en redes | Documentación de configuración de red |
| A.9 | Control de acceso | Procedimientos de acceso |
| §6.1 | Gestión de riesgos | Evaluación de riesgos de seguridad |

### IEC 62443 — Seguridad en Sistemas de Automatización Industrial

| Parte | Título | Documentación Requerida |
|-------|--------|-------------------------|
| 2-1 | CSMS (Cyber Security Management System) | Políticas, procedimientos, plan de emergencia |
| 3-2 | Evaluación de riesgos de seguridad | Análisis de riesgos, zonas y conductos |
| 3-3 | Requisitos de seguridad del sistema (SL/SR) | Niveles de seguridad, requisitos por zona |
| 4-1 | Desarrollo seguro de producto (SDL) | Prácticas 1-8, lifecycle, testing |
| 4-2 | Requisitos de seguridad de componentes | Especificaciones técnicas de componentes |

### EU CRA 2024/2847 — Cyber Resilience Act

| Artículo/Anexo | Título | Documentación Requerida |
|----------------|--------|-------------------------|
| Anexo I | Requisitos esenciales de ciberseguridad | Especificaciones de seguridad del producto |
| Anexo I §2 | Gestión de vulnerabilidades | SBOM, proceso de actualización |
| Anexo II | Información e instrucciones al usuario | Manual de usuario con info de seguridad |
| Anexo V | Declaración de conformidad UE | Declaración formal de conformidad |
| Anexo VII | Documentación técnica | Expediente técnico completo |
| Art. 10-13 | Obligaciones del fabricante | Evaluación de conformidad |
| Art. 14 | Obligaciones de notificación de vulnerabilidades | Proceso PSIRT, notificación a ENISA |

---

## 3. Propuesta: Nueva Jerarquía de Categorías

Se pasa de 7 categorías planas a **7 dominios principales + ~30 subcategorías**, usando el soporte de `ParentId` ya existente.

### 📊 01 — Sistema de Gestión (SGC)

> **Normativa principal**: ISO 9001:2015  
> **Scope**: Master  
> **Color**: `#3b82f6` (azul)  
> **Carpeta base**: `sgc`

| Sub-ID | Subcategoría | Carpeta | Normativa | Clasificación | Rol Mínimo |
|--------|--------------|---------|-----------|---------------|------------|
| 01.1 | Política de Calidad | `sgc/politica-calidad` | ISO 9001 §5.2 | Interno | Auditor |
| 01.2 | Procesos del SGC | `sgc/procesos` | ISO 9001 §4.4, §7.5 | Interno | Auditor |
| 01.3 | Auditorías Internas | `sgc/auditorias` | ISO 9001 §9.2 | Confidencial | Auditor |
| 01.4 | No Conformidades | `sgc/no-conformidades` | ISO 9001 §10.2 | Confidencial | Administrador |
| 01.5 | Revisión por Dirección | `sgc/revision-direccion` | ISO 9001 §9.3 | Confidencial | Administrador |
| 01.6 | Mejora Continua | `sgc/mejora-continua` | ISO 9001 §10.3 | Interno | Auditor |

### 🔒 02 — Seguridad & Cumplimiento

> **Normativa principal**: ISO 27001, IEC 62443, EU CRA  
> **Scope**: Master  
> **Color**: `#f59e0b` (ámbar)  
> **Carpeta base**: `seguridad`

| Sub-ID | Subcategoría | Carpeta | Normativa | Clasificación | Rol Mínimo |
|--------|--------------|---------|-----------|---------------|------------|
| 02.1 | Políticas de Seguridad | `seguridad/politicas` | ISO 27001 A.5.1-A.5.37 | Interno | Auditor |
| 02.2 | Gestión de Riesgos | `seguridad/riesgos` | ISO 27001 §6.1, IEC 62443 3-2 | Confidencial | Administrador |
| 02.3 | SoA & Controles | `seguridad/soa-controles` | ISO 27001 A.8, A.9 | Confidencial | Auditor |
| 02.4 | CRA — Conformidad EU | `seguridad/cra` | CRA Anexo I, V, VII, Art.10-13 | Interno | Auditor |
| 02.5 | Seguridad Industrial OT | `seguridad/iec62443` | IEC 62443 2-1 CSMS, 3-3 SR/SL | Confidencial | Administrador |
| 02.6 | Vulnerabilidades & PSIRT | `seguridad/vulnerabilidades` | CRA Art.14, ISO 27001 A.8.8 | Restringido | Administrador |

### 📦 03 — Producto & Software

> **Normativa principal**: CRA, IEC 62443, ISO 9001  
> **Scope**: Master  
> **Color**: `#10b981` (verde)  
> **Carpeta base**: `producto`

| Sub-ID | Subcategoría | Carpeta | Normativa | Clasificación | Rol Mínimo |
|--------|--------------|---------|-----------|---------------|------------|
| 03.1 | Arquitectura del Sistema | `producto/arquitectura` | IEC 62443 4-1, CRA Anexo VII | Confidencial | Mantenimiento |
| 03.2 | Desarrollo Seguro (SDL) | `producto/sdl` | IEC 62443 4-1 Prácticas 1-8 | Confidencial | Administrador |
| 03.3 | Especificaciones Funcionales | `producto/especificaciones` | ISO 9001 §8.3 | Interno | Mantenimiento |
| 03.4 | SBOM | `producto/sbom` | CRA Anexo I §2, Art.13 | Interno | Auditor |
| 03.5 | Testing & Validación | `producto/testing` | IEC 62443 4-1 SR-5, ISO 9001 §8.6 | Interno | Mantenimiento |

### ⚡ 04 — Ingeniería

> **Normativa principal**: IEC 62443  
> **Scope**: Ambos (Master + Project)  
> **Color**: `#8b5cf6` (violeta)  
> **Carpeta base**: `ingenieria`

| Sub-ID | Subcategoría | Carpeta | Normativa | Clasificación | Rol Mínimo |
|--------|--------------|---------|-----------|---------------|------------|
| 04.1 | Esquemas Eléctricos | `ingenieria/electricos` | — | Confidencial | Mantenimiento |
| 04.2 | Diagramas de Control | `ingenieria/control` | IEC 62443 3-3 | Confidencial | Mantenimiento |
| 04.3 | Configuración de Red | `ingenieria/red` | IEC 62443 3-3 SR 5.1, ISO 27001 A.8.20 | Restringido | Administrador |

### 🔩 05 — Operaciones

> **Normativa principal**: ISO 9001, IEC 62443  
> **Scope**: Project  
> **Color**: `#06b6d4` (cyan)  
> **Carpeta base**: `operaciones`

| Sub-ID | Subcategoría | Carpeta | Normativa | Clasificación | Rol Mínimo |
|--------|--------------|---------|-----------|---------------|------------|
| 05.1 | Mantenimiento Preventivo | `operaciones/preventivo` | ISO 9001 §8.5.1 | Interno | Mantenimiento |
| 05.2 | Mantenimiento Correctivo | `operaciones/correctivo` | ISO 9001 §10.2 | Interno | Mantenimiento |
| 05.3 | Procedimientos de Emergencia | `operaciones/emergencia` | IEC 62443 2-1 §4.3.4.5 | Interno | Operador |
| 05.4 | Registros de Operación | `operaciones/registros` | ISO 9001 §7.5 | Interno | Operador |

### 📖 06 — Documentación de Usuario

> **Normativa principal**: CRA, ISO 9001  
> **Scope**: Ambos  
> **Color**: `#22c55e` (verde claro)  
> **Carpeta base**: `usuario`

| Sub-ID | Subcategoría | Carpeta | Normativa | Clasificación | Rol Mínimo |
|--------|--------------|---------|-----------|---------------|------------|
| 06.1 | Manual de Usuario | `usuario/manual` | CRA Anexo II §1-6 | Público | Visualizador |
| 06.2 | Guía de Instalación | `usuario/instalacion` | CRA Anexo II §7 | Público | Operador |
| 06.3 | Formación & Capacitación | `usuario/formacion` | ISO 27001 A.6.3, ISO 9001 §7.2 | Interno | Operador |
| 06.4 | FAQ & Troubleshooting | `usuario/faq` | — | Público | Visualizador |

### 📋 07 — Proyecto

> **Normativa principal**: ISO 9001, CRA  
> **Scope**: Project  
> **Color**: `#ef4444` (rojo)  
> **Carpeta base**: `proyecto`

| Sub-ID | Subcategoría | Carpeta | Normativa | Clasificación | Rol Mínimo |
|--------|--------------|---------|-----------|---------------|------------|
| 07.1 | Contrato & Requisitos | `proyecto/contrato` | ISO 9001 §8.2 | Confidencial | Administrador |
| 07.2 | Plan de Proyecto | `proyecto/plan` | ISO 9001 §8.1 | Interno | Mantenimiento |
| 07.3 | Entregables al Cliente | `proyecto/entregables` | CRA Anexo V, ISO 9001 §8.6 | Interno | Mantenimiento |
| 07.4 | Actas de Aceptación | `proyecto/aceptacion` | ISO 9001 §8.6 | Confidencial | Administrador |
| 07.5 | Soporte Post-Venta | `proyecto/postventa` | CRA Art.14, ISO 9001 §8.5.5 | Interno | Mantenimiento |

---

## 4. Matriz de Cobertura Cruzada: Categorías × Normativas

### Trazabilidad ISO 9001

| Cláusula ISO 9001 | Categoría(s) DMS |
|--------------------|------------------|
| §4.3 Alcance | 01.1 Política de Calidad |
| §4.4 Procesos | 01.2 Procesos del SGC |
| §5.2 Política | 01.1 Política de Calidad |
| §6.1 Riesgos | 02.2 Gestión de Riesgos |
| §7.2 Competencia | 06.3 Formación |
| §7.5 Info Documentada | 01.2 Procesos del SGC, 05.4 Registros |
| §8.1 Planificación | 07.2 Plan de Proyecto |
| §8.2 Requisitos | 07.1 Contrato & Requisitos |
| §8.3 Diseño | 03.3 Especificaciones Funcionales |
| §8.4 Proveedores | 03.4 SBOM (componentes terceros) |
| §8.5 Producción | 05.1 Mantenimiento Preventivo |
| §8.5.5 Post-entrega | 07.5 Soporte Post-Venta |
| §8.6 Liberación | 03.5 Testing, 07.3 Entregables, 07.4 Actas |
| §9.1 Seguimiento | Panel de Auditoría (KPIs automáticos) |
| §9.2 Auditoría interna | 01.3 Auditorías Internas |
| §9.3 Revisión dirección | 01.5 Revisión por Dirección |
| §10.2 No conformidad | 01.4 No Conformidades, 05.2 Mant. Correctivo |
| §10.3 Mejora continua | 01.6 Mejora Continua |

### Trazabilidad ISO 27001

| Control ISO 27001 | Categoría(s) DMS |
|--------------------|------------------|
| A.5 Políticas | 02.1 Políticas de Seguridad |
| A.6.3 Formación | 06.3 Formación & Capacitación |
| A.8.2 Clasificación | Sistema de clasificación (4 niveles, ya implementado) |
| A.8.3 Restricción acceso | Matriz de acceso (ya implementada) |
| A.8.8 Vulnerabilidades | 02.6 Vulnerabilidades & PSIRT |
| A.8.20 Seguridad redes | 04.3 Configuración de Red |
| A.9 Control acceso | 02.3 SoA & Controles |
| §6.1 Riesgos | 02.2 Gestión de Riesgos |

### Trazabilidad IEC 62443

| Parte IEC 62443 | Categoría(s) DMS |
|------------------|------------------|
| 2-1 CSMS | 02.5 Seguridad Industrial OT, 05.3 Emergencia |
| 3-2 Evaluación riesgos | 02.2 Gestión de Riesgos |
| 3-3 SL/SR | 02.5 Seguridad Industrial, 04.2 Control, 04.3 Red |
| 4-1 SDL | 03.1 Arquitectura, 03.2 SDL, 03.5 Testing |
| 4-2 Componentes | 03.1 Arquitectura |

### Trazabilidad EU CRA

| Artículo/Anexo CRA | Categoría(s) DMS |
|---------------------|------------------|
| Anexo I Requisitos | 02.4 CRA Conformidad, 03.4 SBOM |
| Anexo I §2 SBOM | 03.4 SBOM |
| Anexo II Usuario | 06.1 Manual Usuario, 06.2 Guía Instalación |
| Anexo V Conformidad | 02.4 CRA Conformidad, 07.3 Entregables |
| Anexo VII Doc Técnica | 02.4 CRA Conformidad, 03.1 Arquitectura |
| Art. 10-13 Fabricante | 02.4 CRA Conformidad |
| Art. 14 Vulnerabilidades | 02.6 Vulnerabilidades & PSIRT, 07.5 Post-Venta |

---

## 5. Ampliación del Modelo: Campo ISO 9001

Actualmente el documento tiene campos para CRA, ISO 27001 e IEC 62443, pero **falta ISO 9001**.

### Campos a añadir al modelo `Document`

| Campo | Tipo | Ejemplo | Propósito |
|-------|------|---------|-----------|
| `Iso9001Relevant` | `bool` | `true` | Marca el doc como relevante para ISO 9001 |
| `Iso9001Article` | `string?` | `"§8.3"`, `"§9.2"` | Cláusula ISO 9001 aplicable |

### Impacto

- `DocumentModels.cs`: Añadir 2 propiedades
- `DocumentService.cs`: Ampliar `GetStatsAsync()` con stats ISO 9001
- `DocumentStats`: Añadir `Iso9001RelevantTotal`, `Iso9001Approved`, `Iso9001Pending`, `Iso9001CompliancePercent`, `Iso9001ByArticle`
- Frontend: 4ª tarjeta normativa en Panel de Auditoría
- Migración SQLite: `ALTER TABLE Documents ADD COLUMN Iso9001Relevant INTEGER DEFAULT 0`

---

## 6. Auto-Tags Normativos por Subcategoría

Al crear un documento en una subcategoría, el sistema debería pre-rellenar los campos normativos automáticamente.

| Subcategoría | Auto-CRA | Auto-ISO27001 | Auto-IEC62443 | Auto-ISO9001 |
|--------------|----------|---------------|---------------|--------------|
| 01.1 Política Calidad | — | — | — | §5.2 |
| 01.2 Procesos SGC | — | — | — | §4.4 |
| 01.3 Auditorías | — | — | — | §9.2 |
| 01.4 No Conformidades | — | — | — | §10.2 |
| 01.5 Revisión Dirección | — | — | — | §9.3 |
| 01.6 Mejora Continua | — | — | — | §10.3 |
| 02.1 Políticas Seguridad | — | A.5 | — | — |
| 02.2 Gestión Riesgos | — | §6.1 | 3-2 | §6.1 |
| 02.3 SoA & Controles | — | A.8, A.9 | — | — |
| 02.4 CRA Conformidad | Anexo I | — | — | — |
| 02.5 Seguridad Industrial | — | — | 2-1 | — |
| 02.6 Vulnerabilidades | Art.14 | A.8.8 | — | — |
| 03.1 Arquitectura | Anexo VII | — | 4-1 | §8.3 |
| 03.2 SDL | — | — | 4-1 | — |
| 03.3 Especificaciones | — | — | — | §8.3 |
| 03.4 SBOM | Anexo I §2 | — | — | — |
| 03.5 Testing | — | — | 4-1 SR-5 | §8.6 |
| 04.1 Esquemas Eléctricos | — | — | — | — |
| 04.2 Diagramas Control | — | — | 3-3 | — |
| 04.3 Config Red | — | A.8.20 | 3-3 SR5.1 | — |
| 05.1 Mant. Preventivo | — | — | — | §8.5.1 |
| 05.2 Mant. Correctivo | — | — | — | §10.2 |
| 05.3 Emergencia | — | — | 2-1 §4.3 | — |
| 05.4 Registros | — | — | — | §7.5 |
| 06.1 Manual Usuario | Anexo II | — | — | — |
| 06.2 Guía Instalación | Anexo II §7 | — | — | — |
| 06.3 Formación | — | A.6.3 | — | §7.2 |
| 06.4 FAQ | — | — | — | — |
| 07.1 Contrato | — | — | — | §8.2 |
| 07.2 Plan Proyecto | — | — | — | §8.1 |
| 07.3 Entregables | Anexo V | — | — | §8.6 |
| 07.4 Actas Aceptación | — | — | — | §8.6 |
| 07.5 Post-Venta | Art.14 | — | — | §8.5.5 |

---

## 7. Flujo de Vida del Documento Multi-Normativo

```
[Crear] → Draft → Review → Approved → Archived
                     ↓                    ↑
                   Draft ← (Rechazar)     │
                                    Approved → Obsolete
```

### Fase Draft

1. **Redacción** del contenido
2. **Asignación de normativas**: Tags ISO 9001/CRA/ISO 27001/IEC 62443 (auto-tags según subcategoría)
3. **Clasificación** ISO 27001 A.8.2 (Público/Interno/Confidencial/Restringido)
4. **Rol mínimo** según Matriz de Acceso

### Fase Review

1. **Verificación técnica**: Contenido correcto
2. **Verificación normativa**: ¿Cumple los artículos asignados?
3. **Aprobación** por usuario con rol adecuado

### Fase Approved

1. Documento **vigente** y disponible para auditoría
2. **Hash de integridad** registrado
3. Visible en **Panel de Auditoría** con indicadores de compliance

### Transiciones

- `Approved → Review`: Si requiere actualización (genera nueva versión)
- `Approved → Archived`: Al publicar nueva versión (la anterior se archiva)
- `Approved → Obsolete`: Retirada definitiva

---

## 8. Migración: Categorías Actuales → Nuevas

| Categoría Actual | Migra a | Notas |
|------------------|---------|-------|
| 📋 Compliance CRA (ID 0) | **02.4 CRA — Conformidad EU** | Se integra en el dominio Seguridad |
| 🇪🇺 CRA Genérico (SW) (ID 1) | **03.1 Arquitectura** + **03.4 SBOM** | Se divide según contenido real |
| 📖 Manuales de Usuario (ID 2) | **06.1 Manual de Usuario** | Directo |
| 🔧 Documentación Técnica (ID 3) | **03.3 Especificaciones** o **03.1 Arquitectura** | Según contenido: funcional vs arquitectural |
| ⚡ Esquemas Eléctricos (ID 4) | **04.1 Esquemas Eléctricos** | Directo, se enriquece con subcategorías |
| 🔩 Mantenimiento (ID 5) | **05.1/05.2 Mantenimiento** | Se divide en preventivo y correctivo |
| 📄 Otros (ID 7) | **Reclasificar** cada documento | Auditar contenido y redistribuir |

### Estrategia de migración

1. Crear las nuevas categorías padre (7 dominios)
2. Crear las subcategorías con sus defaults normativos
3. Migrar documentos existentes: asignar nueva categoría según contenido
4. Marcar categorías antiguas como `IsSystem=false` o eliminar
5. Verificar que no queden docs en "Otros" sin reclasificar

---

## 9. Panel de Auditoría — Ampliación con ISO 9001

### Nueva tarjeta normativa

Se añade una **4ª tarjeta** al panel de compliance existente:

| Normativa | Indicadores |
|-----------|-------------|
| **EU CRA** | ✅ Ya implementado |
| **ISO 27001** | ✅ Ya implementado |
| **IEC 62443** | ✅ Ya implementado |
| **ISO 9001** (nuevo) | Docs relevantes, Aprobados, Pendientes, % Cobertura, Por cláusula |

### Nuevo KPI global

**"Cobertura Multi-Normativa"** = % de subcategorías con al menos 1 documento `Approved` por cada normativa que les aplica.

Ejemplo: Si la subcategoría 03.1 debe tener docs con tags CRA + IEC 62443 + ISO 9001, y tiene Approved para CRA e IEC pero no para ISO 9001, la cobertura de esa subcategoría es 66%.

---

## 10. Mapeo al Árbol Empresa

| Sección Empresa | Categoría DMS | Scope |
|-----------------|---------------|-------|
| /05_PRODUCTOS | 03 Producto & Software | AQSdocs_master |
| /05_PRODUCTOS/documentacion_tecnica | 03.1 Arquitectura + 03.3 Especificaciones | Master |
| /05_PRODUCTOS/seguridad_producto | 02.4 CRA + 02.6 Vulnerabilidades | Master |
| /06_PROYECTOS | 07 Proyecto | AQSdocs_project |
| /06_PROYECTOS/{cliente}/documentacion | 06.1-06.4 Doc. Usuario | Project |
| /08_ENTREGABLES | 07.3 Entregables al Cliente | Project |
| /09_POSTVENTA | 07.5 Soporte Post-Venta | Project |
| /11_SEGURIDAD_CUMPLIMIENTO | 02 Seguridad & Cumplimiento (entero) | Master |
| /11_SEGURIDAD_CUMPLIMIENTO/iso_9001 | 01 Sistema de Gestión | Master |

---

## 11. Plan de Implementación

| # | Cambio | Impacto | Prioridad | Complejidad |
|---|--------|---------|-----------|-------------|
| 1 | Reestructurar 7 categorías → 7 dominios + ~30 subcategorías | Backend seed + migración docs | **Alta** | Media |
| 2 | Añadir campos `Iso9001Relevant` + `Iso9001Article` | Migración DB, UI editor, stats | **Alta** | Baja |
| 3 | Auto-tags normativos por subcategoría | Backend: lógica al crear doc | **Media** | Media |
| 4 | 4ª tarjeta ISO 9001 en Panel de Auditoría | Frontend stats | **Media** | Baja |
| 5 | KPI "Cobertura Multi-Normativa" general | Backend stats endpoint | **Baja** | Media |
| 6 | Mapeo carpeta/folder por subcategoría | Ya soportado con `FolderName` | **Baja** | Baja |
| 7 | Documentar defaults en UI admin de categorías | Frontend categories panel | **Baja** | Baja |

### Lo que NO necesita cambios

- ✅ Clasificación ISO 27001 A.8.2 (4 niveles) — ya funciona
- ✅ Matriz de acceso por roles — ya funciona
- ✅ Flujo Draft → Review → Approved — ya funciona
- ✅ Control de versiones — ya funciona
- ✅ Scopes Software/Project — ya funciona
- ✅ Panel de Auditoría (CRA/ISO 27001/IEC 62443) — ya funciona, solo ampliar

---

## 12. Resumen Visual

```
         ┌─────────────────────────────────────────────┐
         │         AQUAFRISCH SUPERVISOR — DMS          │
         │           Estructura Multi-Normativa         │
         ├─────────────────────────────────────────────┤
         │                                             │
         │  📊 01 SGC (ISO 9001)                       │
         │    ├── 01.1 Política de Calidad             │
         │    ├── 01.2 Procesos                        │
         │    ├── 01.3 Auditorías                      │
         │    ├── 01.4 No Conformidades                │
         │    ├── 01.5 Revisión Dirección              │
         │    └── 01.6 Mejora Continua                 │
         │                                             │
         │  🔒 02 Seguridad (ISO 27001 + IEC + CRA)    │
         │    ├── 02.1 Políticas de Seguridad          │
         │    ├── 02.2 Gestión de Riesgos              │
         │    ├── 02.3 SoA & Controles                 │
         │    ├── 02.4 CRA — Conformidad EU            │
         │    ├── 02.5 Seguridad Industrial OT         │
         │    └── 02.6 Vulnerabilidades & PSIRT        │
         │                                             │
         │  📦 03 Producto & Software                   │
         │    ├── 03.1 Arquitectura del Sistema        │
         │    ├── 03.2 Desarrollo Seguro (SDL)         │
         │    ├── 03.3 Especificaciones Funcionales    │
         │    ├── 03.4 SBOM                            │
         │    └── 03.5 Testing & Validación            │
         │                                             │
         │  ⚡ 04 Ingeniería                            │
         │    ├── 04.1 Esquemas Eléctricos             │
         │    ├── 04.2 Diagramas de Control            │
         │    └── 04.3 Configuración de Red            │
         │                                             │
         │  🔩 05 Operaciones                           │
         │    ├── 05.1 Mant. Preventivo                │
         │    ├── 05.2 Mant. Correctivo                │
         │    ├── 05.3 Procedimientos Emergencia       │
         │    └── 05.4 Registros de Operación          │
         │                                             │
         │  📖 06 Documentación Usuario                 │
         │    ├── 06.1 Manual de Usuario               │
         │    ├── 06.2 Guía de Instalación             │
         │    ├── 06.3 Formación & Capacitación        │
         │    └── 06.4 FAQ & Troubleshooting           │
         │                                             │
         │  📋 07 Proyecto                              │
         │    ├── 07.1 Contrato & Requisitos           │
         │    ├── 07.2 Plan de Proyecto                │
         │    ├── 07.3 Entregables al Cliente          │
         │    ├── 07.4 Actas de Aceptación             │
         │    └── 07.5 Soporte Post-Venta              │
         │                                             │
         └─────────────────────────────────────────────┘
```

---

> **Próximo paso**: Cuando se decida implementar, comenzar por los puntos 1 (reestructurar categorías en seed) y 2 (campo ISO 9001 en modelo) — todo lo demás depende de ellos.
