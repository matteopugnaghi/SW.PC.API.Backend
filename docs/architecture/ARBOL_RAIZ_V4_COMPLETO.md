# 🌳 ÁRBOL RAÍZ DOCUMENTAL v4.1 — COMPLETO CON UBICACIONES

> **Código**: ARB-2026-001  
> **Versión**: 4.1  
> **Fecha**: 2026-02-16  
> **Estado**: Para revisión con Dirección  
> **Autor**: Departamento de Software  
> **Referencia**: PGD-2026-001 (Plan de Gestión Documental)  
> **Cambio v4.1**: Añadida columna de ubicación (DMS Empresa / Supervisor Master / Supervisor Project)  

---

## Contexto Aquafrisch

> **Aquafrisch** es un fabricante de maquinaria para talleres de ferrocarriles.  
> Fabricamos distintos **modelos de máquina** (lavadoras de bogies, tornos de ruedas, etc.).  
> Cada máquina lleva un **PC Industrial** con nuestro software **Aquafrisch Supervisor** (SCADA/HMI).  
>  
> El **software Supervisor es SIEMPRE EL MISMO** para todas las máquinas.  
> Lo que cambia entre máquinas es: la **configuración Excel**, los **modelos 3D** y el **programa PLC (TwinCAT)**.  
>  
> Por eso necesitamos **tres ubicaciones** para la documentación:

---

## Leyenda de Estados

| Icono | Significado |
|-------|-------------|
| ✅ | **EXISTE** — Ya lo tenemos escrito |
| 🔴 | **CREAR AUDITORÍA** — Necesario para auditoría IEC 62443 abril 2026 |
| 🟡 | **CREAR FUTURO** — Importante pero no urgente |
| ⬜ | **PER-MACHINE** — Se crea por cada instalación/proyecto |
| ⚠️ | **RESTRINGIDO** — Credenciales, no publicar nunca |

## Leyenda de Ubicaciones

| Icono | Sistema | Descripción |
|-------|---------|-------------|
| 🏢 | **DMS Empresa** | Documentación **interna** de la empresa. NUNCA va al PC del cliente. Solo accesible desde la red de Aquafrisch. Políticas, auditorías, calidad, ingeniería, contratos, SBOM... |
| 🖥️M | **Supervisor Master** | Documentación para el **cliente/ingeniero de campo**, **siempre igual** para todos los proyectos y modelos de máquina. Se despliega con cada instalación del Supervisor. Manual de usuario, ficha técnica, release notes, guías de referencia... |
| 🖥️P | **Supervisor Project** | Documentación **específica de ESTA instalación/máquina**. Diferente para cada proyecto. Configuración Excel, modelos 3D, plan de mantenimiento, puesta en marcha, libro de máquina... |

---

## Visión General del Árbol

```
🏭 AQUAFRISCH — GESTIÓN DOCUMENTAL (Maquinaria talleres ferroviarios)
│                                                            UBICACIÓN
│━━━ 📦 MASTER — Se escribe UNA VEZ, sirve para TODAS las máquinas ━━━
│
├── 🌐 00 PÚBLICO              5 subcat    7 docs  (4✅  2🟡  1🔴)   🏢 + 🖥️M
├── 📋 01 CALIDAD              5 subcat    8 docs  (2✅  6🟡)        🏢
├── 🔒 02 SEGURIDAD            5 subcat   22 docs (11✅  4🟡  7🔴)   🏢
├── 💻 03 SOFTWARE             5 subcat   20 docs (17✅  1🟡  3🔴)   🏢          ← LA MÁS LLENA
├── 📖 04 MANUALES             4 subcat   10 docs  (6✅  4🟡)        🖥️M
├── 📐 05 PLANTILLAS           4 subcat   10 docs  (1✅  9🟡)        🏢
│
│━━━ 🔧 PER-MACHINE — Se repite para CADA instalación (orden cronológico) ━━━
│
├── 🏗️ 06 PROYECTO  ① Vender    5 subcat   10 docs  (1✅  7⬜  2🔴)  🏢
├── ⚡ 07 INGENIERÍA ② Diseñar   8 subcat   14 docs  (0✅ 14⬜)       🏢
├── 🔧 08 TWINCAT   ③ Programar 5 subcat   10 docs  (0✅ 10⬜)       🏢
├── ⚙️ 09 CONFIG SW  ④ Config    3 subcat    9 docs  (4✅  5⬜)       🖥️M + 🖥️P
├── 🛠️ 10 OPERACIONES ⑤ Mantener 5 subcat   15 docs  (0✅ 15⬜)      🖥️P
│
├── ⛔ INTERNO                                       3 docs  (3⚠️)    🏢
│
│━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
│ TOTALES: 11 categorías │ 54 subcategorías │ ~128 posiciones
│          43 documentos EXISTENTES ✅
│          14 documentos AUDITORÍA 🔴 (abril 2026)
│          ~27 documentos FUTURO 🟡
│          ~44 posiciones PER-MACHINE ⬜
│
│ POR UBICACIÓN:
│   🏢  DMS Empresa .............. ~100 docs (políticas, ingeniería, audit, contratos)
│   🖥️M Supervisor Master ........  ~18 docs (manuales, fichas, guías — igual en toda máquina)
│   🖥️P Supervisor Project ........  ~23 docs (config, 3D, mantenimiento — distinto cada máquina)
│━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## Tres Ubicaciones, Un Solo Árbol

> El árbol documental es ÚNICO, pero cada documento tiene una **ubicación física** diferente según quién lo necesita y si varía entre máquinas.

```
┌─────────────────────────────────────────────────────────────────┐
│  🏢 DMS EMPRESA                                                │
│  (Software nuevo, Fase 2: mayo-dic 2026)                       │
│                                                                 │
│  → Software centralizado en la RED INTERNA de Aquafrisch        │
│  → NUNCA va al PC del cliente                                   │
│  → Todo el árbol (00-10) accesible desde aquí                   │
│  → Departamentos: Dirección, Calidad, Ingeniería, Comercial,   │
│    Software, Servicio Técnico                                   │
│  → ~100 documentos: políticas, auditorías, ingeniería,          │
│    contratos, SBOM, planos, esquemas...                         │
│  → OBJETIVO: ISO 9001 + ISO 27001 + orden empresa              │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│  🖥️M AQUAFRISCH SUPERVISOR — MASTER                             │
│  (Ya existe al 80%, completar Fase 1)                           │
│                                                                 │
│  → Módulo DMS dentro del SCADA en el PC Industrial              │
│  → Documentación IGUAL para TODAS las máquinas/modelos          │
│  → Se despliega con cada instalación del Supervisor             │
│  → ~18 documentos: manual de usuario, ficha técnica,            │
│    release notes, vulnerability report, guías de referencia,    │
│    troubleshooting, manuales de instalación                     │
│  → OBJETIVO: Pasar auditoría IEC 62443 abril 2026              │
│                                                                 │
├─────────────────────────────────────────────────────────────────┤
│  🖥️P AQUAFRISCH SUPERVISOR — PROJECT                            │
│  (Se crea para cada instalación/máquina)                        │
│                                                                 │
│  → Carpeta Projects/{projectId}/ en el Supervisor               │
│  → Documentación ESPECÍFICA de ESTA máquina                     │
│  → Diferente para cada proyecto: lavadora de bogies RhB ≠       │
│    torno de ruedas FFS ≠ lavadora exterior SBB                  │
│  → ~23 documentos: ProjectConfig.xlsm, modelos 3D (.glb),      │
│    plan mantenimiento, commissioning, libro de máquina,         │
│    repuestos, project.db                                        │
│  → OBJETIVO: Documentación operativa en planta                  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

EL ÁRBOL ES EL MISMO. Solo cambia DÓNDE se guarda cada documento.
Algunos documentos pueden estar en 2 ubicaciones (ej: un doc 🏢 que
también se copia al Supervisor 🖥️M para referencia del técnico).
```

---

## 🖥️ Vista Explorador DMS — Árbol Completo con Ubicaciones

> **Ubicación**: 🏢 = DMS Empresa | 🖥️M = Supervisor Master | 🖥️P = Supervisor Project

```
 🏭 AQUAFRISCH — GESTIÓN DOCUMENTAL (Maquinaria talleres ferroviarios)
 │                                                          Estado  Ubicación
 │━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 │  📦 MASTER — UNA COPIA, TODAS LAS MÁQUINAS (todos los modelos)
 │━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 │
 ├── 🌐 00 PÚBLICO
 │   ├── 📁 00.1 Certificaciones
 │   │   ├── 📄 Certificado ISO 9001                              🟡    🏢
 │   │   ├── 📄 Certificado ISO 27001                             🟡    🏢
 │   │   └── 📄 Declaración IEC 62443                             🟡    🏢
 │   ├── 📁 00.2 Catálogo Producto
 │   │   ├── 📄 Aquafrisch_Supervisor_Core_2026.pptx              ✅    🏢
 │   │   ├── 📄 email_comercial.html                              ✅    🏢
 │   │   └── 📄 Screenshots funcionales (10 capturas)             ✅    🏢
 │   ├── 📁 00.3 Ficha Técnica
 │   │   └── 📄 Ficha técnica producto (datasheet)                🟡    🖥️M
 │   ├── 📁 00.4 Declaración Conformidad
 │   │   └── 📄 Declaración conformidad EU CRA                    🟡    🖥️M
 │   └── 📁 00.5 Política Vulnerabilidades
 │       ├── 📄 VULNERABILITY_REPORT.md                           ✅    🖥️M
 │       └── 📄 DOC-13: Proceso Gestión Vulnerabilidades          🔴    🖥️M
 │
 ├── 📋 01 CALIDAD
 │   ├── 📁 01.1 SGC — Sistema Gestión Calidad
 │   │   ├── 📄 Manual SGC                                        🟡    🏢
 │   │   └── 📄 Política de Calidad (firmada dirección)           🟡    🏢
 │   ├── 📁 01.2 Gestión Documental
 │   │   ├── 📄 PGD_PLAN_GESTION_DOCUMENTAL.md                    ✅    🏢
 │   │   └── 📄 DMS_ESTRUCTURA_MULTINORMATIVA.md                   ✅    🏢
 │   ├── 📁 01.3 Objetivos Calidad
 │   │   └── 📄 Objetivos calidad anuales 2026                    🟡    🏢
 │   ├── 📁 01.4 No Conformidades
 │   │   ├── 📄 Registro de no conformidades                      🟡    🏢
 │   │   └── 📄 Procedimiento acciones correctivas                🟡    🏢
 │   └── 📁 01.5 Mejora Continua
 │       └── 📄 Plan mejora continua                              🟡    🏢
 │
 ├── 🔒 02 SEGURIDAD
 │   ├── 📁 02.1 Políticas Ciberseguridad
 │   │   ├── 📄 SECURITY.md                                       ✅    🏢
 │   │   ├── 📄 resumen-ciberseguridad.md                         ✅    🏢
 │   │   ├── 📄 ROLES_PERMISSIONS.md                              ✅    🏢
 │   │   ├── 📄 ROLES_PERMISSIONS_QUICKSTART.md                   ✅    🏢
 │   │   ├── 📄 DOC-01: Política de Ciberseguridad                🔴    🏢
 │   │   ├── 📄 DOC-03: Organigrama + RACI Ciberseguridad         🔴    🏢
 │   │   ├── 📄 DOC-05: Política Protección Física y TI           🔴    🏢
 │   │   ├── 📄 DOC-06: Política Gestión de Cuentas TI            🔴    🏢
 │   │   └── 📄 DOC-07: Política Seguridad OT (TwinCAT/PLC)      🔴    🏢
 │   ├── 📁 02.2 CRA EU — Cumplimiento Europeo
 │   │   ├── 📄 ROADMAP_CUMPLIMIENTO_CRA.md                       ✅    🏢
 │   │   ├── 📄 GESTION_USUARIOS_EU_CRA.md                        ✅    🏢
 │   │   ├── 📄 SISTEMA_LOGS_CRA.md                               ✅    🏢
 │   │   └── 📄 DOC-04: Plan de Gestión de Incidentes             🔴    🏢
 │   ├── 📁 02.3 Integridad
 │   │   ├── 📄 SOFTWARE_INTEGRITY.md                             ✅    🏢
 │   │   ├── 📄 integrity-state.json                              ✅    🏢
 │   │   └── 📄 deploy-version.json                               ✅    🖥️M
 │   ├── 📁 02.4 Auditorías y Evaluaciones
 │   │   ├── 📄 rhb-it-standards-gap-analysis.md                  ✅    🏢
 │   │   ├── 📄 DOC-02: Estrategia Ciberseguridad + KPIs          🔴    🏢
 │   │   └── 📄 DOC-12: Procedimiento Evaluación Terceros         🔴    🏢
 │   └── 📁 02.5 Gestión Riesgos
 │       ├── 📄 Análisis de riesgos ciber                         🟡    🏢
 │       └── 📄 Plan tratamiento de riesgos                       🟡    🏢
 │
 ├── 💻 03 SOFTWARE
 │   ├── 📁 03.1 Arquitectura del Sistema
 │   │   ├── 📄 ARQUITECTURA_DESPLIEGUE.md                        ✅    🏢
 │   │   ├── 📄 ARQUITECTURA_LOGS.md                              ✅    🏢
 │   │   ├── 📄 DATA_MANAGEMENT.md                                ✅    🏢
 │   │   ├── 📄 DOCUMENT_MANAGEMENT_SYSTEM.md                     ✅    🏢
 │   │   ├── 📄 MODELOS_3D_IMPLEMENTATION.md                      ✅    🏢
 │   │   ├── 📄 MULTI_PROJECT_SYSTEM.md                           ✅    🏢
 │   │   └── 📄 SYSTEM_CONFIG_IMPLEMENTATION.md                   ✅    🏢
 │   ├── 📁 03.2 SDL — Desarrollo Seguro
 │   │   ├── 📄 GUIA_DESARROLLO.md                                ✅    🏢
 │   │   ├── 📄 BACKEND_API_EXAMPLE.md                            ✅    🏢
 │   │   ├── 📄 INTEGRACION_BACKEND.md                            ✅    🏢
 │   │   ├── 📄 INTEGRACION_FRONTEND_PUMPS.md                     ✅    🏢
 │   │   ├── 📄 IMPLEMENTACION_PUMP_ELEMENTS.md                   ✅    🏢
 │   │   └── 📄 DOC-08: SDL — Proceso Desarrollo Seguro           🔴    🏢
 │   ├── 📁 03.3 Guías de Codificación Segura
 │   │   └── 📄 DOC-09: Secure Coding Guidelines                  🔴    🏢
 │   ├── 📁 03.4 SBOM y Terceros
 │   │   ├── 📄 INDICE_TERCEROS.md                                ✅    🏢
 │   │   ├── 📄 README_BECKHOFF.md                                ✅    🏢
 │   │   ├── 📄 Nuestra_Configuracion_Beckhoff.md                 ✅    🏢
 │   │   └── 📄 DOC-10: SBOM Formal                               🔴    🏢
 │   └── 📁 03.5 Testing y Changelog
 │       ├── 📄 ESTADO_INTEGRACION.md                             ✅    🏢
 │       ├── 📄 RESUMEN_TRABAJO_NOCTURNO.md                       ✅    🏢
 │       └── 📄 Plan de testing formal                            🟡    🏢
 │
 ├── 📖 04 MANUALES
 │   ├── 📁 04.1 Manual de Usuario
 │   │   ├── 📄 MANUAL_USUARIO_RECUPERACION.md                    ✅    🖥️M
 │   │   └── 📄 Manual usuario completo                           🟡    🖥️M
 │   ├── 📁 04.2 Manual de Instalación
 │   │   ├── 📄 INSTALACION_PRODUCCION.md                         ✅    🖥️M
 │   │   ├── 📄 COMO_USAR_NUEVA_VERSION.md                        ✅    🖥️M
 │   │   ├── 📄 README_KIOSK.md                                   ✅    🖥️M
 │   │   └── 📄 SERVIDOR_EMPRESA.md                               ✅    🏢
 │   ├── 📁 04.3 Formación
 │   │   ├── 📄 Material formación operadores                     🟡    🖥️M
 │   │   └── 📄 Guía rápida de arranque                           🟡    🖥️M
 │   └── 📁 04.4 FAQ / Troubleshooting
 │       ├── 📄 TROUBLESHOOTING_ANIMACION_PLC.md                  ✅    🖥️M
 │       └── 📄 FAQ general del producto                          🟡    🖥️M
 │
 └── 📐 05 PLANTILLAS
     ├── 📁 05.1 Checklist Nuevo Proyecto
     │   ├── 📄 Checklist arranque proyecto nuevo                 🟡    🏢
     │   └── 📄 Checklist entrega final                           🟡    🏢
     ├── 📁 05.2 Formatos Estándar
     │   ├── 📄 Template informe ingeniería                       🟡    🏢
     │   ├── 📄 Template acta reunión                             🟡    🏢
     │   └── 📄 Template informe test                             🟡    🏢
     ├── 📁 05.3 Componentes Homologados
     │   ├── 📄 VOCABULARIO_MAQUINA.xlsx                          ✅    🏢
     │   ├── 📄 Lista componentes aprobados                       🟡    🏢
     │   └── 📄 Proveedores homologados                           🟡    🏢
     └── 📁 05.4 Criterios de Aceptación
         ├── 📄 Criterios aceptación SW (SAT/FAT)                 🟡    🏢
         └── 📄 Criterios aceptación HW                           🟡    🏢
 │
 │━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 │  🔧 PER-MACHINE — UNA COPIA POR INSTALACIÓN
 │  Orden cronológico: Vender → Diseñar → Programar → Config → Mantener
 │  (cada modelo de máquina × cada cliente = un proyecto independiente)
 │━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 │
 ├── 🏗️ 06 PROYECTO ① Vender
 │   ├── 📁 06.1 Oferta Comercial
 │   │   ├── 📄 Oferta técnica-económica                          ⬜    🏢
 │   │   └── 📄 Presupuesto detallado                             ⬜    🏢
 │   ├── 📁 06.2 Especificaciones Cliente
 │   │   ├── 📄 RhB_IT_Standards_v9.0.4_Analisis.md              ✅    🏢
 │   │   ├── 📄 DOC-11: Requisitos Ciberseguridad Producto        🔴    🏢
 │   │   └── 📄 Especificaciones técnicas del cliente             ⬜    🏢
 │   ├── 📁 06.3 Contrato
 │   │   ├── 📄 Contrato firmado                                  ⬜    🏢
 │   │   └── 📄 Condiciones de garantía                           ⬜    🏢
 │   ├── 📁 06.4 Plan de Proyecto
 │   │   ├── 📄 Cronograma proyecto (Gantt)                       ⬜    🏢
 │   │   └── 📄 DOC-14: Roles Ciberseguridad por Proyecto         🔴    🏢
 │   └── 📁 06.5 Actas y Comunicaciones
 │       ├── 📄 Actas reunión con cliente                         ⬜    🏢
 │       └── 📄 Correspondencia técnica relevante                 ⬜    🏢
 │
 ├── ⚡ 07 INGENIERÍA ② Diseñar
 │   ├── 📁 07.1 Esquemas Eléctricos
 │   │   ├── 📄 Esquemas de potencia                              ⬜    🏢
 │   │   ├── 📄 Esquemas de control/maniobra                      ⬜    🏢
 │   │   └── 📄 Lista de cables                                   ⬜    🏢
 │   ├── 📁 07.2 P&ID (Piping & Instrumentation)
 │   │   ├── 📄 Diagrama tuberías e instrumentación               ⬜    🏢
 │   │   └── 📄 Lista de instrumentos                             ⬜    🏢
 │   ├── 📁 07.3 Layout / Implantación
 │   │   ├── 📄 Layout planta 2D                                  ⬜    🏢
 │   │   └── 📄 Layout 3D (si aplica)                             ⬜    🏢
 │   ├── 📁 07.4 Planos Mecánicos
 │   │   ├── 📄 Planos de conjunto                                ⬜    🏢
 │   │   └── 📄 Planos de detalle / despiece                      ⬜    🏢
 │   ├── 📁 07.5 Esquemas Neumáticos/Hidráulicos
 │   │   └── 📄 Esquemas neumáticos (si aplica)                   ⬜    🏢
 │   ├── 📁 07.6 BOM — Lista de Materiales
 │   │   ├── 📄 BOM materiales completa                           ⬜    🏢
 │   │   └── 📄 BOM componentes eléctricos                        ⬜    🏢
 │   ├── 📁 07.7 Datasheets Componentes
 │   │   └── 📄 Fichas técnicas equipos instalados                ⬜    🏢
 │   └── 📁 07.8 Planos As-Built
 │       └── 📄 Planos "como quedó" (cambios vs diseño)           ⬜    🏢
 │
 ├── 🔧 08 TWINCAT / PLC ③ Programar
 │   ├── 📁 08.1 Proyecto TwinCAT
 │   │   ├── 📄 Archivo proyecto .tsproj (backup)                 ⬜    🏢
 │   │   └── 📄 Versión TwinCAT y runtime                         ⬜    🏢
 │   ├── 📁 08.2 Configuración I/O
 │   │   ├── 📄 Mapa de I/O (entradas/salidas)                    ⬜    🏢
 │   │   └── 📄 Lista señales con direcciones                     ⬜    🏢
 │   ├── 📁 08.3 EtherCAT
 │   │   ├── 📄 Topología red EtherCAT                            ⬜    🏢
 │   │   └── 📄 Configuración esclavos + firmware                 ⬜    🏢
 │   ├── 📁 08.4 Recetas PLC
 │   │   ├── 📄 Definición de recetas                             ⬜    🏢
 │   │   └── 📄 Parámetros de proceso                             ⬜    🏢
 │   └── 📁 08.5 Documentación PLC
 │       ├── 📄 Descripción funcional del PLC                     ⬜    🏢
 │       └── 📄 Lista de Function Blocks                          ⬜    🏢
 │
 ├── ⚙️ 09 CONFIG SW ④ Configurar
 │   ├── 📁 09.1 Excel Config
 │   │   ├── 📄 ESTRUCTURA_EXCEL_15_COLUMNAS_REFERENCIA.md        ✅    🖥️M
 │   │   ├── 📄 MAPEO_COLUMNAS_EXCEL.md                           ✅    🖥️M
 │   │   ├── 📄 SYSTEM_CONFIG_SHEET.md                            ✅    🖥️M
 │   │   ├── 📄 3D_Elements_Info_Setting.md                       ✅    🖥️M
 │   │   └── 📄 ProjectConfig.xlsm (de esta máquina)              ⬜    🖥️P
 │   ├── 📁 09.2 Modelos 3D
 │   │   ├── 📄 Archivos .glb de esta máquina                     ⬜    🖥️P
 │   │   └── 📄 Guía configuración modelos 3D                     🟡    🖥️M
 │   └── 📁 09.3 Base de Datos Proyecto
 │       ├── 📄 project.db de esta máquina                        ⬜    🖥️P
 │       └── 📄 Esquema/documentación de la DB                    🟡    🖥️M
 │
 ├── 🛠️ 10 OPERACIONES ⑤ Mantener
 │   ├── 📁 10.1 Mantenimiento Preventivo
 │   │   ├── 📄 Plan mantenimiento preventivo                     ⬜    🖥️P
 │   │   ├── 📄 Checklist inspección periódica                    ⬜    🖥️P
 │   │   └── 📄 Calendario de mantenimiento                       ⬜    🖥️P
 │   ├── 📁 10.2 Mantenimiento Correctivo
 │   │   ├── 📄 Registro de averías                               ⬜    🖥️P
 │   │   ├── 📄 Informes de reparación                            ⬜    🖥️P
 │   │   └── 📄 Análisis causa raíz                               ⬜    🖥️P
 │   ├── 📁 10.3 Repuestos
 │   │   ├── 📄 Lista repuestos recomendados                      ⬜    🖥️P
 │   │   └── 📄 Stock mínimo                                      ⬜    🖥️P
 │   ├── 📁 10.4 Histórico Máquina
 │   │   ├── 📄 Libro de máquina                                  ⬜    🖥️P
 │   │   ├── 📄 Registro de modificaciones                        ⬜    🖥️P
 │   │   └── 📄 Histórico de alarmas                              ⬜    🖥️P
 │   └── 📁 10.5 Puesta en Marcha
 │       ├── 📄 Protocolo puesta en marcha                        ⬜    🖥️P
 │       ├── 📄 Checklist commissioning                           ⬜    🖥️P
 │       └── 📄 Acta de recepción firmada                         ⬜    🖥️P
 │
 └── ⛔ INTERNO — Fuera del árbol (NO publicar)
     ├── 📄 CLIENTE_CREDENCIALES_INICIALES.md                     ⚠️    🏢
     ├── 📄 INTERNAL_AQUAFRISCH_CREDENTIALS.md                    ⚠️    🏢
     └── 📄 DOCUMENTACION_INTERNA_AQUAFRISCH.md                   ⚠️    🏢
```

> **Leyenda**: ✅ Existe | 🔴 AUDIT = Crear para auditoría abril 2026 | 🟡 Crear futuro | ⬜ Per-machine | ⚠️ Restringido  
> **Ubicación**: 🏢 DMS Empresa | 🖥️M Supervisor Master (igual todas las máquinas) | 🖥️P Supervisor Project (específico esta máquina)

---

# DETALLE COMPLETO POR CATEGORÍA

---

## 🌐 00 PÚBLICO

> **Clasificación**: 🟢 Público  
> **Responsable**: Dirección / Comercial  
> **Normativa principal**: EU CRA Anexo II, Anexo V  
> **¿Qué es?**: Lo que puede ver cualquiera — clientes, auditores, web  

### 00.1 Certificaciones
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 1 | Certificado ISO 9001 | 🟡 PENDIENTE | 🏢 | — cuando se certifique |
| 2 | Certificado ISO 27001 | 🟡 PENDIENTE | 🏢 | — cuando se certifique |
| 3 | Declaración IEC 62443 | 🟡 PENDIENTE | 🏢 | — cuando se certifique |

### 00.2 Catálogo Producto
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 4 | Presentación Aquafrisch Supervisor | ✅ EXISTE | 🏢 | `presentacion/Aquafrisch_Supervisor_Core_2026.pptx` |
| 5 | Email comercial tipo | ✅ EXISTE | 🏢 | `presentacion/email_comercial.html` |
| 6 | Screenshots funcionales (10 capturas) | ✅ EXISTE | 🏢 | `presentacion/01_login.png` → `10_hardware_monitor.png` |

### 00.3 Ficha Técnica
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 7 | Ficha técnica producto (datasheet) | 🟡 CREAR | 🖥️M | — |

### 00.4 Declaración Conformidad
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 8 | Declaración conformidad EU CRA | 🟡 CREAR | 🖥️M | — |

### 00.5 Política Vulnerabilidades
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 9 | Informe vulnerabilidades conocidas | ✅ EXISTE | 🖥️M | `user-guides/VULNERABILITY_REPORT.md` |
| 10 | **DOC-13: Proceso Gestión Vulnerabilidades** | 🔴 AUDITORÍA | 🖥️M | **CREAR** — Cubre S7.1, S7.2 |

---

## 📋 01 CALIDAD

> **Clasificación**: 🔵 Interno  
> **Responsable**: Responsable Calidad / Dirección  
> **Normativa principal**: ISO 9001  
> **¿Qué es?**: El sistema de gestión de calidad de la empresa  

### 01.1 SGC — Sistema Gestión Calidad
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 11 | Manual SGC | 🟡 CREAR | 🏢 | — |
| 12 | Política de Calidad (firmada dirección) | 🟡 CREAR | 🏢 | — |

### 01.2 Gestión Documental
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 13 | Plan de Gestión Documental (PGD) | ✅ EXISTE | 🏢 | `architecture/PGD_PLAN_GESTION_DOCUMENTAL.md` |
| 14 | Estructura multinormativa (borrador) | ✅ EXISTE | 🏢 | `architecture/DMS_ESTRUCTURA_MULTINORMATIVA.md` |

### 01.3 Objetivos Calidad
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 15 | Objetivos calidad anuales 2026 | 🟡 CREAR | 🏢 | — |

### 01.4 No Conformidades
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 16 | Registro de no conformidades | 🟡 CREAR | 🏢 | — |
| 17 | Procedimiento acciones correctivas | 🟡 CREAR | 🏢 | — |

### 01.5 Mejora Continua
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 18 | Plan mejora continua | 🟡 CREAR | 🏢 | — |

---

## 🔒 02 SEGURIDAD

> **Clasificación**: 🟠 Confidencial / 🔴 Restringido  
> **Responsable**: Responsable Seguridad / IT  
> **Normativa principal**: ISO 27001, IEC 62443, EU CRA  
> **¿Qué es?**: Ciberseguridad, cumplimiento, protección de datos — **LA CATEGORÍA MÁS IMPORTANTE PARA LA AUDITORÍA**  

### 02.1 Políticas Ciberseguridad
| # | Documento | Estado | Archivo actual | Auditoría |
|---|-----------|--------|----------------|-----------|
| 19 | Security overview (existente) | ✅ EXISTE | `compliance/SECURITY.md` | — |
| 20 | Resumen ciberseguridad | ✅ EXISTE | `compliance/resumen-ciberseguridad.md` | — |
| 21 | Roles y permisos del sistema | ✅ EXISTE | `development/ROLES_PERMISSIONS.md` | — |
| 22 | Quickstart roles | ✅ EXISTE | `development/ROLES_PERMISSIONS_QUICKSTART.md` | — |
| 23 | **DOC-01: Política de Ciberseguridad** | 🔴 AUDITORÍA | **CREAR** ~8 págs | S1.1 |
| 24 | **DOC-03: Organigrama + RACI Ciberseguridad** | 🔴 AUDITORÍA | **CREAR** ~4 págs | S1.4, S1.5 |
| 25 | **DOC-05: Política Protección Física y TI** | 🔴 AUDITORÍA | **CREAR** ~4 págs | S2.1, S2.2, S2.5 |
| 26 | **DOC-06: Política Gestión de Cuentas TI** | 🔴 AUDITORÍA | **CREAR** ~3 págs | S2.3 |
| 27 | **DOC-07: Política Seguridad OT (TwinCAT/PLC)** | 🔴 AUDITORÍA | **CREAR** ~5 págs | S3.1, S3.2, S3.3 |

### 02.2 CRA EU — Cumplimiento Europeo
| # | Documento | Estado | Ubicación | Archivo actual | Auditoría |
|---|-----------|--------|-----------|----------------|----------|
| 28 | Roadmap cumplimiento CRA | ✅ EXISTE | 🏢 | `compliance/ROADMAP_CUMPLIMIENTO_CRA.md` | — |
| 29 | Gestión usuarios CRA | ✅ EXISTE | 🏢 | `compliance/GESTION_USUARIOS_EU_CRA.md` | — |
| 30 | Sistema de logs CRA | ✅ EXISTE | 🏢 | `compliance/SISTEMA_LOGS_CRA.md` | — |
| 31 | **DOC-04: Plan de Gestión de Incidentes** | 🔴 AUDITORÍA | 🏢 | **CREAR** ~6 págs | S2.4 |

### 02.3 Integridad
| # | Documento | Estado | Ubicación | Archivo actual | Auditoría |
|---|-----------|--------|-----------|----------------|----------|
| 32 | Software Integrity (firma, checksums) | ✅ EXISTE | 🏢 | `architecture/SOFTWARE_INTEGRITY.md` | S5.2 ✅ |
| 33 | Estado integridad actual | ✅ EXISTE | 🏢 | `integrity-state.json` | S5.2 ✅ |
| 34 | Versión deploy producción | ✅ EXISTE | 🖥️M | `deploy-version.json` (generado en deploy) | S5.2 ✅ |

### 02.4 Auditorías y Evaluaciones
| # | Documento | Estado | Ubicación | Archivo actual | Auditoría |
|---|-----------|--------|-----------|----------------|----------|
| 35 | Gap analysis RhB IT Standards | ✅ EXISTE | 🏢 | `compliance/rhb-it-standards-gap-analysis.md` | — |
| 36 | **DOC-02: Estrategia Ciberseguridad + KPIs** | 🔴 AUDITORÍA | 🏢 | **CREAR** ~5 págs | S1.2, S1.3 |
| 37 | **DOC-12: Procedimiento Evaluación Terceros** | 🔴 AUDITORÍA | 🏢 | **CREAR** ~4 págs | S6.3 |

### 02.5 Gestión Riesgos
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 38 | Análisis de riesgos ciber | 🟡 CREAR | 🏢 | — |
| 39 | Plan tratamiento de riesgos | 🟡 CREAR | 🏢 | — |

---

## 💻 03 SOFTWARE

> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Departamento Software  
> **Normativa principal**: IEC 62443 4-1, EU CRA Anexo VII  
> **¿Qué es?**: Documentación del código (Frontend + Backend) — siempre igual para todas las máquinas. **LA CATEGORÍA MÁS LLENA**  

### 03.1 Arquitectura del Sistema
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 40 | Arquitectura y despliegue | ✅ EXISTE | 🏢 | `architecture/ARQUITECTURA_DESPLIEGUE.md` |
| 41 | Arquitectura de logs | ✅ EXISTE | 🏢 | `architecture/ARQUITECTURA_LOGS.md` |
| 42 | Gestión de datos (backup, restore) | ✅ EXISTE | 🏢 | `architecture/DATA_MANAGEMENT.md` |
| 43 | Sistema de gestión documental | ✅ EXISTE | 🏢 | `architecture/DOCUMENT_MANAGEMENT_SYSTEM.md` |
| 44 | Implementación modelos 3D | ✅ EXISTE | 🏢 | `architecture/MODELOS_3D_IMPLEMENTATION.md` |
| 45 | Sistema multi-proyecto | ✅ EXISTE | 🏢 | `architecture/MULTI_PROJECT_SYSTEM.md` |
| 46 | Implementación System Config | ✅ EXISTE | 🏢 | `configuration/SYSTEM_CONFIG_IMPLEMENTATION.md` |

### 03.2 SDL — Desarrollo Seguro (Secure Development Lifecycle)
| # | Documento | Estado | Ubicación | Archivo actual | Auditoría |
|---|-----------|--------|-----------|----------------|----------|
| 47 | Guía de desarrollo | ✅ EXISTE | 🏢 | `development/GUIA_DESARROLLO.md` | — |
| 48 | Ejemplo API Backend | ✅ EXISTE | 🏢 | `development/BACKEND_API_EXAMPLE.md` | — |
| 49 | Integración Backend | ✅ EXISTE | 🏢 | `development/INTEGRACION_BACKEND.md` | — |
| 50 | Integración Frontend bombas | ✅ EXISTE | 🏢 | `development/INTEGRACION_FRONTEND_PUMPS.md` | — |
| 51 | Implementación pump elements | ✅ EXISTE | 🏢 | `development/IMPLEMENTACION_PUMP_ELEMENTS.md` | — |
| 52 | **DOC-08: SDL — Proceso Desarrollo Seguro** | 🔴 AUDITORÍA | 🏢 | **CREAR** ~8 págs | S4.1, S4.2, S4.4 |

### 03.3 Guías de Codificación Segura
| # | Documento | Estado | Ubicación | Archivo actual | Auditoría |
|---|-----------|--------|-----------|----------------|----------|
| 53 | **DOC-09: Secure Coding Guidelines** | 🔴 AUDITORÍA | 🏢 | **CREAR** ~6 págs | S4.3 |

### 03.4 SBOM y Terceros
| # | Documento | Estado | Ubicación | Archivo actual | Auditoría |
|---|-----------|--------|-----------|----------------|----------|
| 54 | Índice de terceros | ✅ EXISTE | 🏢 | `compliance/terceros/INDICE_TERCEROS.md` | — |
| 55 | README Beckhoff | ✅ EXISTE | 🏢 | `compliance/terceros/beckhoff/README_BECKHOFF.md` | — |
| 56 | Config Beckhoff propia | ✅ EXISTE | 🏢 | `compliance/terceros/beckhoff/Nuestra_Configuracion_Beckhoff.md` | — |
| 57 | **DOC-10: SBOM Formal** | 🔴 AUDITORÍA | 🏢 | **CREAR** ~3 págs (semi-auto: package.json + .csproj) | S5.1 |

### 03.5 Testing y Changelog
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 58 | Estado integración | ✅ EXISTE | 🏢 | `changelog/ESTADO_INTEGRACION.md` |
| 59 | Resumen trabajo nocturno | ✅ EXISTE | 🏢 | `changelog/RESUMEN_TRABAJO_NOCTURNO.md` |
| 60 | Plan de testing formal | 🟡 CREAR | 🏢 | — |

---

## 📖 04 MANUALES

> **Clasificación**: 🟢 Público / 🔵 Interno  
> **Responsable**: Departamento Software / Ingeniería  
> **Normativa principal**: EU CRA Anexo II  
> **¿Qué es?**: Documentación de usuario del producto en general  

### 04.1 Manual de Usuario
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 61 | Manual recuperación usuario | ✅ EXISTE | 🖥️M | `user-guides/MANUAL_USUARIO_RECUPERACION.md` |
| 62 | Manual usuario completo | 🟡 CREAR | 🖥️M | — (ampliar con info CRA Anexo II) |

### 04.2 Manual de Instalación
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 63 | Instalación producción completa | ✅ EXISTE | 🖥️M | `deployment/INSTALACION_PRODUCCION.md` |
| 64 | Cómo usar nueva versión | ✅ EXISTE | 🖥️M | `deployment/COMO_USAR_NUEVA_VERSION.md` |
| 65 | Configuración modo Kiosk | ✅ EXISTE | 🖥️M | `deployment/README_KIOSK.md` |
| 66 | Deploy servidor empresa | ✅ EXISTE | 🏢 | `deployment/SERVIDOR_EMPRESA.md` |

### 04.3 Formación
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 67 | Material formación operadores | 🟡 CREAR | 🖥️M | — |
| 68 | Guía rápida de arranque | 🟡 CREAR | 🖥️M | — |

### 04.4 FAQ / Troubleshooting
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 69 | Troubleshooting animación PLC | ✅ EXISTE | 🖥️M | `development/TROUBLESHOOTING_ANIMACION_PLC.md` |
| 70 | FAQ general del producto | 🟡 CREAR | 🖥️M | — |

---

## 📐 05 PLANTILLAS

> **Clasificación**: 🔵 Interno  
> **Responsable**: Ingeniería / Dirección Técnica  
> **¿Qué es?**: **La "receta"** — estándares y plantillas para estandarizar el trabajo. No son documentos de un proyecto real, sino las REGLAS que todos deben seguir.  

### 05.1 Checklist Nuevo Proyecto
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 71 | Checklist arranque proyecto nuevo | 🟡 CREAR | 🏢 | — |
| 72 | Checklist entrega final | 🟡 CREAR | 🏢 | — |

### 05.2 Formatos Estándar
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 73 | Template informe ingeniería | 🟡 CREAR | 🏢 | — |
| 74 | Template acta reunión | 🟡 CREAR | 🏢 | — |
| 75 | Template informe test | 🟡 CREAR | 🏢 | — |

### 05.3 Componentes Homologados
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 76 | Vocabulario máquina (glosario) | ✅ EXISTE | 🏢 | `internal/VOCABULARIO_MAQUINA.xlsx` |
| 77 | Lista componentes aprobados | 🟡 CREAR | 🏢 | — |
| 78 | Proveedores homologados | 🟡 CREAR | 🏢 | — |

### 05.4 Criterios de Aceptación
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 79 | Criterios aceptación SW (SAT/FAT) | 🟡 CREAR | 🏢 | — |
| 80 | Criterios aceptación HW | 🟡 CREAR | 🏢 | — |

---

# 🔧 PER-MACHINE — Se repite para CADA instalación

> Cada máquina/proyecto tiene su propia copia de las carpetas 06 a 10.  
> El orden sigue el **flujo cronológico real**: Vender → Diseñar → Programar → Configurar → Mantener  

---

## 🏗️ 06 PROYECTO — ① Vender

> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Project Manager / Comercial  
> **¿Cuándo?**: PRIMERO — antes de empezar a diseñar  

### 06.1 Oferta Comercial
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 81 | Oferta técnica-económica | ⬜ per-machine | 🏢 | — |
| 82 | Presupuesto detallado | ⬜ per-machine | 🏢 | — |

### 06.2 Especificaciones Cliente
| # | Documento | Estado | Ubicación | Archivo actual | Auditoría |
|---|-----------|--------|-----------|----------------|----------|
| 83 | Análisis estándares IT del cliente | ✅ EXISTE | 🏢 | `especificaciones_clientes/RhB_IT_Standards_v9.0.4_Analisis.md` | — |
| 84 | **DOC-11: Requisitos Ciberseguridad Producto** | 🔴 AUDITORÍA | 🏢 | **CREAR** ~5 págs | S6.1, S6.2 |
| 85 | Especificaciones técnicas del cliente | ⬜ per-machine | 🏢 | — |

### 06.3 Contrato
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 86 | Contrato firmado | ⬜ per-machine | 🏢 | — |
| 87 | Condiciones de garantía | ⬜ per-machine | 🏢 | — |

### 06.4 Plan de Proyecto
| # | Documento | Estado | Ubicación | Archivo actual | Auditoría |
|---|-----------|--------|-----------|----------------|----------|
| 88 | Cronograma proyecto (Gantt) | ⬜ per-machine | 🏢 | — | — |
| 89 | **DOC-14: Roles Ciberseguridad por Proyecto** | 🔴 AUDITORÍA | 🏢 | **CREAR** ~3 págs (plantilla reutilizable) | S8.1, S8.2 |

### 06.5 Actas y Comunicaciones
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 90 | Actas reunión con cliente | ⬜ per-machine | 🏢 | — |
| 91 | Correspondencia técnica relevante | ⬜ per-machine | 🏢 | — |

---

## ⚡ 07 INGENIERÍA — ② Diseñar

> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Ingeniería  
> **¿Cuándo?**: Después de firmar contrato — diseño real de ESTA máquina  

### 07.1 Esquemas Eléctricos
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 92 | Esquemas de potencia | ⬜ per-machine | 🏢 |
| 93 | Esquemas de control/maniobra | ⬜ per-machine | 🏢 |
| 94 | Lista de cables | ⬜ per-machine | 🏢 |

### 07.2 P&ID (Piping & Instrumentation)
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 95 | Diagrama tuberías e instrumentación | ⬜ per-machine | 🏢 |
| 96 | Lista de instrumentos | ⬜ per-machine | 🏢 |

### 07.3 Layout / Implantación
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 97 | Layout planta 2D | ⬜ per-machine | 🏢 |
| 98 | Layout 3D (si aplica) | ⬜ per-machine | 🏢 |

### 07.4 Planos Mecánicos
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 99 | Planos de conjunto | ⬜ per-machine | 🏢 |
| 100 | Planos de detalle / despiece | ⬜ per-machine | 🏢 |

### 07.5 Esquemas Neumáticos/Hidráulicos
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 101 | Esquemas neumáticos (si aplica) | ⬜ per-machine | 🏢 |

### 07.6 BOM — Lista de Materiales
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 102 | BOM materiales completa | ⬜ per-machine | 🏢 |
| 103 | BOM componentes eléctricos | ⬜ per-machine | 🏢 |

### 07.7 Datasheets Componentes
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 104 | Fichas técnicas equipos instalados | ⬜ per-machine | 🏢 |

### 07.8 Planos As-Built
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 105 | Planos "como quedó" (cambios vs diseño) | ⬜ per-machine | 🏢 |

---

## 🔧 08 TWINCAT / PLC — ③ Programar

> **Clasificación**: 🔴 Restringido  
> **Responsable**: Programador PLC  
> **¿Cuándo?**: En paralelo con ingeniería — programa PLC de ESTA máquina  

### 08.1 Proyecto TwinCAT
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 106 | Archivo proyecto .tsproj (backup completo) | ⬜ per-machine | 🏢 |
| 107 | Versión TwinCAT y runtime utilizados | ⬜ per-machine | 🏢 |

### 08.2 Configuración I/O
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 108 | Mapa de I/O (entradas/salidas) | ⬜ per-machine | 🏢 |
| 109 | Lista de señales con direcciones | ⬜ per-machine | 🏢 |

### 08.3 EtherCAT
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 110 | Topología red EtherCAT | ⬜ per-machine | 🏢 |
| 111 | Configuración esclavos + firmware | ⬜ per-machine | 🏢 |

### 08.4 Recetas PLC
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 112 | Definición de recetas | ⬜ per-machine | 🏢 |
| 113 | Parámetros de proceso | ⬜ per-machine | 🏢 |

### 08.5 Documentación PLC
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 114 | Descripción funcional del PLC | ⬜ per-machine | 🏢 |
| 115 | Lista de Function Blocks | ⬜ per-machine | 🏢 |

---

## ⚙️ 09 CONFIG SW — ④ Configurar

> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Departamento Software  
> **¿Cuándo?**: Después del PLC — configurar el SCADA para ESTA máquina  

### 09.1 Excel Config
| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 116 | Referencia estructura Excel 15 columnas | ✅ EXISTE | 🖥️M | `configuration/ESTRUCTURA_EXCEL_15_COLUMNAS_REFERENCIA.md` |
| 117 | Mapeo columnas Excel | ✅ EXISTE | 🖥️M | `configuration/MAPEO_COLUMNAS_EXCEL.md` |
| 118 | System Config sheet | ✅ EXISTE | 🖥️M | `configuration/SYSTEM_CONFIG_SHEET.md` |
| 119 | Configuración elementos 3D | ✅ EXISTE | 🖥️M | `excel configuration/3D_Elements_Info_Setting.md` |
| 120 | ProjectConfig.xlsm de esta máquina | ⬜ per-machine | 🖥️P | — |

### 09.2 Modelos 3D
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 121 | Archivos .glb de esta máquina | ⬜ per-machine | 🖥️P |
| 122 | Guía configuración modelos 3D | 🟡 CREAR | 🖥️M |

### 09.3 Base de Datos Proyecto
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 123 | project.db de esta máquina | ⬜ per-machine | 🖥️P |
| 124 | Esquema/documentación de la DB | 🟡 CREAR | 🖥️M |

---

## 🛠️ 10 OPERACIONES — ⑤ Mantener

> **Clasificación**: 🔵 Interno  
> **Responsable**: Servicio Técnico / Cliente  
> **¿Cuándo?**: Después de la puesta en marcha — durante toda la vida útil  

### 10.1 Mantenimiento Preventivo
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 125 | Plan mantenimiento preventivo | ⬜ per-machine | 🖥️P |
| 126 | Checklist inspección periódica | ⬜ per-machine | 🖥️P |
| 127 | Calendario de mantenimiento | ⬜ per-machine | 🖥️P |

### 10.2 Mantenimiento Correctivo
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 128 | Registro de averías | ⬜ per-machine | 🖥️P |
| 129 | Informes de reparación | ⬜ per-machine | 🖥️P |
| 130 | Análisis causa raíz | ⬜ per-machine | 🖥️P |

### 10.3 Repuestos
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 131 | Lista repuestos recomendados | ⬜ per-machine | 🖥️P |
| 132 | Stock mínimo | ⬜ per-machine | 🖥️P |

### 10.4 Histórico Máquina
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 133 | Libro de máquina | ⬜ per-machine | 🖥️P |
| 134 | Registro de modificaciones | ⬜ per-machine | 🖥️P |
| 135 | Histórico de alarmas | ⬜ per-machine | 🖥️P |

### 10.5 Puesta en Marcha
| # | Documento | Estado | Ubicación |
|---|-----------|--------|----------|
| 136 | Protocolo puesta en marcha | ⬜ per-machine | 🖥️P |
| 137 | Checklist commissioning | ⬜ per-machine | 🖥️P |
| 138 | Acta de recepción firmada | ⬜ per-machine | 🖥️P |

---

## ⛔ INTERNO — Fuera del Árbol (No publicar)

> **Clasificación**: 🔴 RESTRINGIDO MÁXIMO  
> **Nota**: Estos documentos contienen credenciales y datos sensibles. NO van en ningún DMS público. Se mantienen en repositorio privado con acceso restringido.  

| # | Documento | Estado | Ubicación | Archivo actual |
|---|-----------|--------|-----------|----------------|
| 139 | Credenciales iniciales clientes | ⚠️ RESTRINGIDO | 🏢 | `internal/CLIENTE_CREDENCIALES_INICIALES.md` |
| 140 | Credenciales internas Aquafrisch | ⚠️ RESTRINGIDO | 🏢 | `internal/INTERNAL_AQUAFRISCH_CREDENTIALS.md` |
| 141 | Documentación interna varios | ⚠️ RESTRINGIDO | 🏢 | `internal/DOCUMENTACION_INTERNA_AQUAFRISCH.md` |

---

# 🔴 RESUMEN DOCUMENTOS AUDITORÍA IEC 62443 — ABRIL 2026

> Estos son los **14 documentos que hay que CREAR** para pasar la auditoría.  
> Cada uno tiene su posición exacta en el árbol.

| Prioridad | ID | Nombre documento | Posición árbol | Ubicación | Páginas | Cubre checklist |
|-----------|-----|-----------------|----------------|-----------|---------|-----------------|
| 🔴 1 | DOC-01 | Política de Ciberseguridad | **02.1** Políticas | 🏢 | ~8 | S1.1 |
| 🔴 2 | DOC-03 | Organigrama + RACI Ciberseguridad | **02.1** Políticas | 🏢 | ~4 | S1.4, S1.5 |
| 🔴 3 | DOC-08 | SDL — Proceso Desarrollo Seguro | **03.2** SDL | 🏢 | ~8 | S4.1, S4.2, S4.4 |
| 🟠 4 | DOC-04 | Plan de Gestión de Incidentes | **02.2** CRA EU | 🏢 | ~6 | S2.4 |
| 🟠 5 | DOC-07 | Política Seguridad OT (TwinCAT/PLC) | **02.1** Políticas | 🏢 | ~5 | S3.1, S3.2, S3.3 |
| 🟠 6 | DOC-09 | Secure Coding Guidelines | **03.3** Coding | 🏢 | ~6 | S4.3 |
| 🟡 7 | DOC-13 | Proceso Gestión Vulnerabilidades | **00.5** Vulnerab. | 🖥️M | ~5 | S7.1, S7.2 |
| 🟡 8 | DOC-10 | SBOM Formal | **03.4** SBOM | 🏢 | ~3 | S5.1 |
| 🟡 9 | DOC-11 | Requisitos Ciberseguridad Producto | **06.2** Espec. | 🏢 | ~5 | S6.1, S6.2 |
| 🟡 10 | DOC-02 | Estrategia Ciberseguridad + KPIs | **02.4** Auditorías | 🏢 | ~5 | S1.2, S1.3 |
| ⚪ 11 | DOC-05 | Política Protección Física y TI | **02.1** Políticas | 🏢 | ~4 | S2.1, S2.2, S2.5 |
| ⚪ 12 | DOC-06 | Política Gestión Cuentas TI | **02.1** Políticas | 🏢 | ~3 | S2.3 |
| ⚪ 13 | DOC-12 | Procedimiento Evaluación Terceros | **02.4** Auditorías | 🏢 | ~4 | S6.3 |
| ⚪ 14 | DOC-14 | Roles Ciberseguridad por Proyecto | **06.4** Plan Proy. | 🏢 | ~3 | S8.1, S8.2 |
| | | **TOTAL estimado** | | | **~69 págs** | **26 puntos audit.** |

> **Nota**: El punto S5.2 (Integridad Software) ya está cubierto ✅ con `SOFTWARE_INTEGRITY.md` + `integrity-state.json` + `deploy-version.json`.  
> **Nota ubicación**: 13 de los 14 docs de auditoría van en 🏢 DMS Empresa (son políticas internas). Solo DOC-13 (Gestión Vulnerabilidades) va en 🖥️M Supervisor Master porque el cliente necesita saber cómo reportar vulnerabilidades.

---

# 📅 ROADMAP

```
FEB-MAR 2026 ──────── ABR 2026 ──────── MAY-JUL 2026 ──────── SEP-DIC 2026
     │                    │                    │                      │
     │ 🔵 FASE 1          │ 🔴 AUDITORÍA      │ 🟠 FASE 2a           │ 🟠 FASE 2b
     │                    │                    │                      │
     │ • Completar        │ • 14 docs listos   │ • DMS Empresa v1     │ • DMS Empresa v2
     │   Aquafrisch DMS   │ • Software cumple  │ • Login + Roles      │ • Workflows
     │ • Escribir 14 docs │ • Pasar auditoría  │ • Categorías 00-10   │ • Aprobación docs
     │   de auditoría     │   IEC 62443        │ • Upload/Download    │ • Todos los deptos
     │                    │                    │ • Búsqueda           │ • Integ. Supervisor
```

---

## Aprobación

| Rol | Nombre | Firma | Fecha |
|-----|--------|-------|-------|
| Director General | | | |
| Director Técnico | | | |
| Responsable IT / Software | | | |
| Responsable Ingeniería | | | |

---

> **Este documento se examina con Dirección ANTES de:**  
> 1. Empezar a escribir los 14 documentos de auditoría  
> 2. Modificar las categorías del Aquafrisch Supervisor DMS  
> 3. Planificar el desarrollo del DMS Empresa  
