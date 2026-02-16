# PGD — Plan de Gestión Documental

> **Código**: PGD-2026-001  
> **Versión**: 4.0  
> **Fecha**: 2026-02-15  
> **Estado**: Propuesta para aprobación por Dirección  
> **Autor**: Departamento de Software  
> **Ubicación en el árbol**: 📊 01 CALIDAD → 01.2 Procesos del SGC  
> **Clasificación**: 🔵 Interno  

---

## 1. Objetivo de Este Documento

Este documento define **cómo Aquafrisch va a organizar TODA su documentación** — desde catálogos comerciales hasta programas PLC, pasando por planos eléctricos, contratos y manuales.

**Este documento NO es software.** Es el PLAN que dirección debe aprobar para:
1. Saber qué documentos necesitamos
2. Saber dónde va cada uno
3. Cumplir las normativas que nos exigen (ISO 9001, ISO 27001, IEC 62443, EU CRA)
4. Estandarizar el trabajo de ingeniería

---

## 2. Contexto: Dos Softwares Distintos

```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│  🖥️ AQUAFRISCH SUPERVISOR (ya existe)                       │
│                                                             │
│  → Software SCADA/HMI que controla las máquinas             │
│  → Tiene un módulo DMS integrado (gestión de documentos)    │
│  → Se instala en cada PC Industrial, una por máquina        │
│  → Documentos del scope "Máquina" van aquí                  │
│                                                             │
│  👉 ACCIÓN: Adaptar sus categorías al árbol de este plan    │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  📂 NUEVO SOFTWARE DMS (por desarrollar)                     │
│                                                             │
│  → Software de gestión documental de EMPRESA                │
│  → Centralizado (no en cada máquina)                        │
│  → Documentos del scope "Master" van aquí                   │
│  → Accesible desde oficina                                  │
│  → Gestiona TODO el árbol (00-10)                           │
│                                                             │
│  👉 ACCIÓN: Desarrollar basándose en este plan              │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### ¿Cómo se relacionan?

```
                    📂 NUEVO DMS (Empresa)
                    Gestiona TODO el árbol
                    ┌──────────────────────┐
                    │ 00 Público           │
                    │ 01 Calidad           │
                    │ 02 Seguridad         │  ← Solo en el DMS central
                    │ 03 Software          │
                    │ 04 Manuales          │
                    │ 05 Plantillas        │
                    ├──────────────────────┤
                    │ 06 Proyecto ───────┐ │
                    │ 07 Ingeniería ──┐  │ │
                    │ 08 TwinCAT ──┐  │  │ │  ← También en cada máquina
                    │ 09 Config ┐  │  │  │ │
                    │ 10 Operar │  │  │  │ │
                    └───────────┼──┼──┼──┼─┘
                                │  │  │  │
                    ┌───────────▼──▼──▼──▼─┐
                    │ 🖥️ AQUAFRISCH SUPERV. │
                    │ (PC de cada máquina)  │
                    │                      │
                    │ Tiene copia de los    │
                    │ docs de SU máquina    │
                    │ + manuales generales  │
                    └──────────────────────┘
```

---

## 3. El Árbol Documental — Visión para Dirección

### La idea en 30 segundos

**11 carpetas. Divididas en "lo que es siempre igual" y "lo que cambia por máquina".**

```
🏭 AQUAFRISCH — Gestión Documental
│
│━━━ 📦 MASTER — Se escribe UNA VEZ, sirve para TODAS las máquinas ━━━
│
├── 🌐 00 PÚBLICO             ← Lo que puede ver cualquiera
├── 📊 01 CALIDAD              ← ¿Hacemos las cosas bien?
├── 🔒 02 SEGURIDAD            ← ¿Estamos protegidos?
├── 💻 03 SOFTWARE             ← ¿Cómo funciona nuestro software?
├── 📖 04 MANUALES             ← ¿Cómo se usa el producto?
├── 🏗️ 05 PLANTILLAS           ← La "receta" para cada proyecto
│
│━━━ 🔧 POR MÁQUINA — Se repite para CADA instalación (orden cronológico) ━━━
│
├── 📋 06 PROYECTO      ① Vender      ← Oferta, contrato, plan
├── ⚡ 07 INGENIERÍA     ② Diseñar     ← Eléctricos, mecánicos, P&ID, layout
├── 🔧 08 TWINCAT       ③ Programar   ← Código PLC, I/O, EtherCAT
├── ⚙️ 09 CONFIG SW      ④ Configurar  ← Excel, 3D, variables del SCADA
└── 🔩 10 OPERACIONES   ⑤ Mantener    ← Preventivo, correctivo, repuestos
```

### ¿Por qué estas carpetas?

Porque cada una responde a las **normativas que nos exigen**:

```
NORMATIVA              CARPETA PRINCIPAL        TAMBIÉN APLICA A
─────────────────────  ─────────────────────    ──────────────────────
ISO 9001 (Calidad)     📊 01 CALIDAD    ======► 05, 06, 07, 10
ISO 27001 (Seguridad)  🔒 02 SEGURIDAD  ======► 07, 08
IEC 62443 (Industrial) 🔒 02 SEGURIDAD  ======► 03, 07, 08
EU CRA (Ley Europea)   🔒 02 SEGURIDAD  ======► 00, 03, 04, 10
```

> **Para el auditor**: "¿Dónde está la política de calidad?" → Carpeta 01.  
> "¿Dónde está el SBOM?" → Carpeta 03.  
> "¿Los planos de la máquina de Madrid?" → Carpeta 07, proyecto Piscina Madrid.  
> Todo tiene su sitio.

---

## 4. Detalle Completo del Árbol

### 📦 MASTER — Documentación que se escribe UNA VEZ

---

### 🌐 00 PÚBLICO
> **Clasificación**: 🟢 Público — Cualquiera puede verlo  
> **Responsable**: Dirección / Comercial  
> **¿Qué es?**: Lo que enseñamos al mundo exterior

| Sub | Subcarpeta | Contenido | Normativa |
|-----|-----------|-----------|-----------|
| 00.1 | Presentación de Empresa | Quiénes somos, qué hacemos, portfolio | — |
| 00.2 | Catálogo de Productos | Fichas comerciales, fotos, características | — |
| 00.3 | Certificaciones y Sellos | Certificado CE, ISOs, declaraciones públicas | CRA Anexo V |
| 00.4 | Condiciones Generales | Condiciones de venta, garantía, RGPD | — |
| 00.5 | Política de Vulnerabilidades | Cómo reportar fallos de seguridad (obligatorio publicar) | **CRA Art. 14** |

---

### 📊 01 CALIDAD
> **Clasificación**: 🔵 Interno  
> **Responsable**: Responsable de Calidad / Dirección  
> **¿Qué es?**: El sistema de gestión de calidad de la empresa

| Sub | Subcarpeta | Contenido | Normativa |
|-----|-----------|-----------|-----------|
| 01.1 | Política de Calidad | Política firmada por dirección, objetivos anuales | ISO 9001 §5.2 |
| 01.2 | Procesos del SGC | Mapa de procesos, procedimientos, fichas de proceso. **👈 ESTE DOCUMENTO (PGD) VA AQUÍ** | ISO 9001 §4.4, §7.5 |
| 01.3 | Auditorías Internas | Programa anual, informes, hallazgos, seguimiento | ISO 9001 §9.2 |
| 01.4 | No Conformidades | Registro NC, acciones correctivas, análisis causas | ISO 9001 §10.2 |
| 01.5 | Revisión por Dirección | Actas anuales, KPIs, decisiones | ISO 9001 §9.3 |
| 01.6 | Mejora Continua | Planes de mejora, indicadores, acciones preventivas | ISO 9001 §10.3 |
| 01.7 | Proveedores y Compras | Proveedores aprobados, evaluaciones, criterios | ISO 9001 §8.4 |

---

### 🔒 02 SEGURIDAD Y CUMPLIMIENTO
> **Clasificación**: 🟠 Confidencial / 🔴 Restringido  
> **Responsable**: Responsable de Seguridad / IT  
> **¿Qué es?**: Todo lo de ciberseguridad y cumplimiento normativo

| Sub | Subcarpeta | Contenido | Normativa |
|-----|-----------|-----------|-----------|
| 02.1 | Políticas de Seguridad | Política de seguridad, uso aceptable, contraseñas, desarrollo seguro | ISO 27001 A.5 |
| 02.2 | Gestión de Riesgos | Metodología, inventario activos, evaluación riesgos, plan tratamiento | ISO 27001 §6.1, IEC 62443 3-2 |
| 02.3 | SoA y Controles | Statement of Applicability (114 controles), evidencias, matriz acceso | ISO 27001 A.8, A.9 |
| 02.4 | CRA — Conformidad EU | Expediente técnico, declaración conformidad, evaluación, roadmap | CRA Anexo I, V, VII |
| 02.5 | Seguridad Industrial OT | CSMS, zonas y conductos, Security Levels, requisitos componentes | IEC 62443 2-1, 3-2, 3-3, 4-2 |
| 02.6 | Vulnerabilidades y PSIRT | Proceso gestión vulnerabilidades, registro, pen-test, notificaciones ENISA | CRA Art. 14, ISO 27001 A.8.8 |
| 02.7 | Continuidad y Recuperación | Plan continuidad negocio, recuperación desastres, backup/restore | ISO 27001 A.5.29-30 |

---

### 💻 03 SOFTWARE
> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Departamento de Software  
> **¿Qué es?**: Documentación del código (Frontend + Backend) — siempre igual para todas las máquinas

| Sub | Subcarpeta | Contenido | Normativa |
|-----|-----------|-----------|-----------|
| 03.1 | Arquitectura del Sistema | Diagramas (Backend, Frontend, SignalR, PLC), stack, flujos, red | IEC 62443 4-1, CRA Anexo VII |
| 03.2 | Desarrollo Seguro (SDL) | Proceso desarrollo, Git flow, reviews, SAST/DAST, checklist seguridad | IEC 62443 4-1 |
| 03.3 | Especificaciones Funcionales | Qué hace cada módulo, APIs (/swagger), requisitos funcionales | ISO 9001 §8.3 |
| 03.4 | SBOM | Lista dependencias (npm + NuGet), licencias, versiones, hashes | **CRA Anexo I §2** |
| 03.5 | Testing y Validación | Plan testing, casos prueba, resultados, criterios aceptación | IEC 62443 4-1, ISO 9001 §8.6 |
| 03.6 | Release Notes | Histórico versiones, cambios, vulnerabilidades corregidas | CRA Art. 14 |

---

### 📖 04 MANUALES GENERALES
> **Clasificación**: 🟢 Público / 🔵 Interno  
> **Responsable**: Departamento de Software / Ingeniería  
> **¿Qué es?**: Documentación de usuario del producto en general

| Sub | Subcarpeta | Contenido | Normativa |
|-----|-----------|-----------|-----------|
| 04.1 | Manual de Usuario | Cómo usar el SCADA/HMI, pantallas, alarmas, info seguridad | **CRA Anexo II §1-6** |
| 04.2 | Guía de Instalación | Requisitos HW, procedimiento instalación, config segura, firewall | CRA Anexo II §7 |
| 04.3 | Formación y Capacitación | Material formación, registro formados, evaluación competencias | ISO 27001 A.6.3, ISO 9001 §7.2 |
| 04.4 | FAQ y Troubleshooting | Problemas comunes, códigos error, guía diagnóstico | — |

---

### 🏗️ 05 PLANTILLAS (La "receta" para proyectos nuevos)
> **Clasificación**: 🔵 Interno  
> **Responsable**: Ingeniería / Dirección Técnica  
> **¿Qué es?**: Estándares y plantillas que definen CÓMO debe hacerse cada proyecto.  
> **Concepto clave**: Esta carpeta es la **estandarización**. No son planos reales — son las reglas y formatos que todos deben seguir.

| Sub | Subcarpeta | Contenido | Para qué sirve |
|-----|-----------|-----------|-----------------|
| 05.1 | Checklist de Proyecto Nuevo | Lista de TODOS los documentos que debe tener cada máquina | Que no se olvide nada |
| 05.2 | Plantilla Esquemas Eléctricos | Formato, carátula, simbología, numeración cables | Que todos los eléctricos se hagan igual |
| 05.3 | Plantilla Planos Mecánicos | Formato, vistas obligatorias, tolerancias, materiales | Que todos los mecánicos se hagan igual |
| 05.4 | Plantilla P&ID | Simbología ISA 5.1, nomenclatura instrumentos, ejemplo tipo | Que todos los P&ID se hagan igual |
| 05.5 | Plantilla Layout | Escalas, capas CAD, distancias mínimas, accesos | Que todos los layouts se hagan igual |
| 05.6 | Especificaciones Técnicas Tipo | Formato de spec, qué datos debe incluir siempre | Que todas las specs tengan la misma info |
| 05.7 | Componentes Homologados | Bombas/variadores/sensores aprobados, proveedores preferentes | Que se usen componentes probados |
| 05.8 | Criterios de Diseño y Normativa | CE, Baja Tensión, EMC, Legionella, materiales contacto agua | Que se cumplan las normas desde el diseño |

> **La analogía para dirección**: "La carpeta 05 es la RECETA. Las carpetas 07-08 son los PLATOS que servimos a cada cliente. Sin receta, cada cocinero hace lo que quiere."

---

### 🔧 POR MÁQUINA — Se repite para CADA instalación

> Cada máquina/proyecto tiene su propia copia de las carpetas 06 a 10.  
> El orden de las carpetas sigue el **flujo cronológico real** del proyecto.

---

### 📋 06 PROYECTO — ① Vender
> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Project Manager / Comercial  
> **¿Cuándo?**: PRIMERO — antes de empezar a diseñar

| Sub | Subcarpeta | Contenido | Normativa |
|-----|-----------|-----------|-----------|
| 06.1 | Oferta y Contrato | Oferta comercial, contrato firmado, requisitos cliente, alcance | ISO 9001 §8.2 |
| 06.2 | Plan de Proyecto | Fases, hitos, fechas, responsables, Gantt/cronograma | ISO 9001 §8.1 |
| 06.3 | Entregables al Cliente | Dossier de máquina, decl. conformidad CE, decl. CRA, manual | CRA Anexo V, ISO 9001 §8.6 |
| 06.4 | Puesta en Marcha y Aceptación | Protocolo FAT, protocolo SAT, acta recepción, punch list | ISO 9001 §8.6 |
| 06.5 | Soporte Post-Venta | Incidencias cliente, actualizaciones entregadas, visitas, fin soporte | CRA Art. 14, ISO 9001 §8.5.5 |

---

### ⚡ 07 INGENIERÍA — ② Diseñar
> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Ingeniería  
> **¿Cuándo?**: Después de firmar contrato — diseño real de ESTA máquina

| Sub | Subcarpeta | Contenido | Normativa |
|-----|-----------|-----------|-----------|
| 07.1 | Esquemas Eléctricos | Cuadro completo, alimentación, protecciones, maniobra, lista cables | — |
| 07.2 | Planos Mecánicos | Conjuntos, subconjuntos, despieces, piezas especiales, fabricación | — |
| 07.3 | P&ID | Piping & Instrumentation real, tags, diámetros | — |
| 07.4 | Layout | Distribución real en sala, acometidas, recorrido tuberías | — |
| 07.5 | Esquemas Neumáticos/Hidráulicos | Si la máquina los tiene | — |
| 07.6 | BOM (Bill of Materials) | Lista materiales completa, referencias, cantidades, proveedores | ISO 9001 §8.5 |
| 07.7 | Datasheets Componentes | Fichas técnicas de lo instalado, curvas bombas, specs variadores | — |
| 07.8 | Planos As-Built | Planos tal como QUEDÓ, cambios vs diseño original | ISO 9001 §7.5 |

---

### 🔧 08 TWINCAT / PLC — ③ Programar
> **Clasificación**: 🔴 Restringido  
> **Responsable**: Programador PLC  
> **¿Cuándo?**: En paralelo con ingeniería — programa de ESTA máquina

| Sub | Subcarpeta | Contenido | Normativa |
|-----|-----------|-----------|-----------|
| 08.1 | Proyecto TwinCAT | Código fuente PLC, versión TwinCAT, backup completo | IEC 62443 4-1 |
| 08.2 | Lista de I/O | Entradas/salidas digitales y analógicas, mapeo borna ↔ variable | — |
| 08.3 | Configuración EtherCAT | Topología red industrial, esclavos, direcciones, firmware | IEC 62443 3-3 |
| 08.4 | Parámetros y Recetas | Setpoints, recetas guardadas, calibraciones | — |
| 08.5 | Mapa de Variables | Binding PLC ↔ HMI/SCADA, unidades, rangos, alarmas | — |

---

### ⚙️ 09 CONFIGURACIÓN SUPERVISOR — ④ Configurar
> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Departamento de Software  
> **¿Cuándo?**: Después del PLC — configurar el SCADA para ESTA máquina

| Sub | Subcarpeta | Contenido | Normativa |
|-----|-----------|-----------|-----------|
| 09.1 | ProjectConfig.xlsm | Excel de configuración (variables, pantallas, modelos 3D) | — |
| 09.2 | Modelos 3D | Archivos .glb de ESTA máquina, config cámaras, luces | — |
| 09.3 | Base de Datos Proyecto | project.db (usuarios, sesiones, audit trail) | — |

---

### 🔩 10 OPERACIONES — ⑤ Mantener
> **Clasificación**: 🔵 Interno  
> **Responsable**: Servicio Técnico / Cliente  
> **¿Cuándo?**: Después de la puesta en marcha — vida útil de la máquina

| Sub | Subcarpeta | Contenido | Normativa |
|-----|-----------|-----------|-----------|
| 10.1 | Plan Mantenimiento Preventivo | Calendario tareas, frecuencias, checklist por tarea | ISO 9001 §8.5.1 |
| 10.2 | Registros Mantenimiento Correctivo | Averías, acciones, tiempo parada | ISO 9001 §10.2 |
| 10.3 | Procedimientos de Emergencia | Parada emergencia, anti-legionella, contactos emergencia | IEC 62443 2-1 |
| 10.4 | Registros de Operación | Logs producción, incidencias, parámetros funcionamiento | ISO 9001 §7.5 |
| 10.5 | Repuestos | Lista recambios, stock mínimo, referencias, proveedores | — |

---

## 5. Clasificación de Seguridad (ISO 27001 A.8.2)

Cada documento tiene un nivel de confidencialidad. Ya implementado en Aquafrisch Supervisor.

| Nivel | Icono | Quién puede ver | Ejemplo |
|-------|-------|-----------------|---------|
| Público | 🟢 | Cualquiera (clientes, web, proveedores) | Catálogo, certificado CE |
| Interno | 🔵 | Solo personal Aquafrisch | Procedimientos, mantenimiento |
| Confidencial | 🟠 | Solo personal autorizado | Planos, contratos, código |
| Restringido | 🔴 | Solo dirección / seguridad | Vulnerabilidades, PLC, pen-test |

---

## 6. Cómo Funciona por Proyecto (Ejemplo Real)

```
Llega un cliente nuevo: "Hotel Playa de Aro"

PASO 1 — Project Manager abre carpeta 05, coge el CHECKLIST (05.1)
         Sabe exactamente qué documentos hay que crear

PASO 2 — Se crea el proyecto "Hotel Playa de Aro" en el sistema
         Se generan automáticamente las carpetas 06-10 vacías

PASO 3 — Comercial rellena 📋 06 PROYECTO
         Oferta, contrato, plan, requisitos del cliente

PASO 4 — Ingeniería diseña ⚡ 07 INGENIERÍA
         Usa las PLANTILLAS de 05 como base
         Esquemas eléctricos con formato estándar Aquafrisch
         P&ID con simbología estándar
         Layout según las reglas de 05.5

PASO 5 — Programador hace 🔧 08 TWINCAT
         Programa PLC, configura EtherCAT, mapea variables

PASO 6 — Software configura ⚙️ 09 CONFIG
         ProjectConfig.xlsm, modelos 3D, base de datos

PASO 7 — Se entrega la máquina
         → 06.3 Entregables: dossier completo al cliente
         → 06.4 Aceptación: protocolo FAT/SAT, firma

PASO 8 — Empieza la vida útil 🔩 10 OPERACIONES
         Mantenimiento preventivo, correctivo, repuestos

PASO 9 — Project Manager revisa CHECKLIST (05.1)
         ¿Falta algún documento? → Completar antes de cerrar proyecto
         ✅ TODO OK → Proyecto COMPLETO
```

---

## 7. Resumen: ¿Qué Hay que Hacer?

### Para Aquafrisch Supervisor (software existente)

| Acción | Descripción | Esfuerzo |
|--------|-------------|----------|
| Cambiar categorías del DMS | Reemplazar las 7 categorías actuales por las 11 del árbol (00-10) con subcategorías | 2-3 días |
| Añadir campo ISO 9001 | Nuevo campo `Iso9001Relevant` + `Iso9001Article` en el modelo de documento | 1 día |
| Actualizar Panel de Auditoría | Añadir 4ª tarjeta ISO 9001, KPI cobertura multi-normativa | 1 día |
| Seed de subcategorías | Crear las ~63 subcategorías con sus defaults (clasificación, rol, auto-tags) | 2 días |

### Para el Nuevo Software DMS (por desarrollar)

| Acción | Descripción | Prioridad |
|--------|-------------|-----------|
| Definir alcance | Qué hace el DMS central vs qué hace Aquafrisch Supervisor | Alta |
| Sincronización | Cómo se sincronizan docs entre DMS central y cada máquina | Alta |
| Gestión multi-proyecto | Ver todos los proyectos desde una sola pantalla | Alta |
| Checklist automático | Cotejar docs de cada proyecto contra plantilla 05 | Media |
| Workflow de aprobación | Draft → Review → Approved con notificaciones | Media |
| Portal público | Servir docs de carpeta 00 a clientes/web | Baja |

### Documentos que HAY QUE ESCRIBIR (independiente del software)

#### 🔴 Fase 1 — Urgente (3-6 meses)

| Documento | Carpeta | Esfuerzo | Quién |
|-----------|---------|----------|-------|
| Política de Seguridad | 02.1 | 1-2 páginas, firma dirección | Dirección + IT |
| Evaluación de Riesgos | 02.2 | Tabla de riesgos, 5-10 páginas | IT + Ingeniería |
| SBOM formal | 03.4 | Exportar de package.json + .csproj | Software |
| Manual Usuario con info CRA | 04.1 | Ampliar el existente | Software |
| Declaración de Conformidad | 00.3 + 02.4 | 1 página, firma dirección | Dirección |
| Política divulgación vulnerabilidades | 00.5 | 1 página, publicar en producto | IT |

#### 🟡 Fase 2 — Importante (6-12 meses)

| Documento | Carpeta | Esfuerzo | Quién |
|-----------|---------|----------|-------|
| Política de Calidad | 01.1 | 1 página, firma dirección | Dirección |
| Proceso Desarrollo Seguro | 03.2 | Documentar lo que ya hacemos (Git, reviews) | Software |
| Arquitectura del Sistema | 03.1 | Diagramas, ya existe parcialmente en docs/ | Software |
| Checklist Proyecto Nuevo | 05.1 | La plantilla maestra | Ingeniería + PM |
| Plantilla eléctricos | 05.2 | Estandarizar formato actual | Ingeniería |

#### 🟢 Fase 3 — Estratégico (12-24 meses)

| Documento | Carpeta | Esfuerzo | Quién |
|-----------|---------|----------|-------|
| SoA completo (114 controles) | 02.3 | Trabajo largo, se puede hacer gradual | IT + Dirección |
| Programa Auditoría Interna | 01.3 | Calendario + checklist | Calidad |
| Todas las plantillas (05.3-05.8) | 05 | Estandarizar ingeniería | Ingeniería |
| Plan Mejora Continua | 01.6 | KPIs, objetivos anuales | Dirección |

---

## 8. Tabla Resumen para Dirección

| Nº | Carpeta | Qué hay dentro | ¿Cuántas copias? | Responsable | Clasificación |
|---|---------|----------------|-------------------|-------------|---------------|
| 00 | 🌐 Público | Catálogo, certificados, política vulnerabilidades | 1 | Comercial | 🟢 Público |
| 01 | 📊 Calidad | Auditorías, procesos, no conformidades, mejora | 1 | Calidad | 🔵 Interno |
| 02 | 🔒 Seguridad | Riesgos, CRA, SoA, vulnerabilidades | 1 | IT / Seguridad | 🟠🔴 Confid. |
| 03 | 💻 Software | Arquitectura, SBOM, testing, releases | 1 | Software | 🟠 Confid. |
| 04 | 📖 Manuales | Manual usuario, instalación, formación | 1 | Software | 🟢 Público |
| 05 | 🏗️ Plantillas | Checklist, formatos estándar, componentes homologados | 1 | Ingeniería | 🔵 Interno |
| 06 | 📋 Proyecto | Oferta, contrato, plan, entregables, post-venta | **1 por máquina** | Project Manager | 🟠 Confid. |
| 07 | ⚡ Ingeniería | Eléctricos, mecánicos, P&ID, layout, BOM, as-built | **1 por máquina** | Ingeniería | 🟠 Confid. |
| 08 | 🔧 TwinCAT | Código PLC, I/O, EtherCAT, recetas, variables | **1 por máquina** | Programador PLC | 🔴 Restringido |
| 09 | ⚙️ Config SW | Excel config, modelos 3D, base datos | **1 por máquina** | Software | 🟠 Confid. |
| 10 | 🔩 Operaciones | Mantenimiento, emergencia, repuestos, logs | **1 por máquina** | Serv. Técnico | 🔵 Interno |

---

## 9. Aprobación

| Rol | Nombre | Firma | Fecha |
|-----|--------|-------|-------|
| Director General | | | |
| Director Técnico | | | |
| Responsable IT / Software | | | |
| Responsable Ingeniería | | | |

---

> **Próximos pasos tras aprobación**:
> 1. Adaptar categorías de Aquafrisch Supervisor al árbol aprobado
> 2. Empezar con los documentos de Fase 1 (urgentes)
> 3. Crear el checklist de proyecto nuevo (05.1)
> 4. Licitar/planificar el desarrollo del nuevo DMS central
