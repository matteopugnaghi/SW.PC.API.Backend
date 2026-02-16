# 🌳 ÁRBOL RAÍZ DOCUMENTAL v5.1 — MULTINORMATIVA COMPLETA

> **Código**: ARB-2026-001  
> **Versión**: 5.1  
> **Fecha**: 2026-02-16  
> **Estado**: Para revisión con Dirección  
> **Autor**: Departamento de Software  
> **Referencia**: PGD-2026-001 (Plan de Gestión Documental)  
> **Cambio v5.1**: Numeración jerárquica `XX.Y-ZZ` (inmune a inserciones). Clarificación producto SW vs máquinas.  
> **Cambio v5.0**: Cobertura completa de las 4 normativas (ISO 9001, ISO 27001, IEC 62443, EU CRA).  
> **Sustituye**: ARBOL_RAIZ_V5_MULTINORMATIVA.md (v5.0)

---

## Contexto Aquafrisch

> **Aquafrisch** es un fabricante de maquinaria para talleres de ferrocarriles.  
> Fabricamos distintos **modelos de máquina** (lavadoras de bogies, tornos de ruedas, etc.).  
> Cada máquina lleva un **PC Industrial** con nuestro software **Aquafrisch Supervisor** (SCADA/HMI).  
>  
> El **software Supervisor es SIEMPRE EL MISMO** para todas las máquinas.  
> Lo que cambia entre máquinas es: la **configuración Excel**, los **modelos 3D** y el **programa PLC (TwinCAT)**.  
>  
> Por eso necesitamos **tres ubicaciones** para la documentación.  
>  
> **Producto digital (EU CRA)**: El sujeto de cumplimiento normativo es **Aquafrisch Supervisor** (el software), no las máquinas individuales. Las fichas técnicas, declaraciones de conformidad y manuales CRA se refieren al software.

---

## Marco Normativo de Referencia

Este árbol está diseñado para cubrir **simultáneamente** las 4 normativas que aplican a Aquafrisch:

| Código | Normativa | Alcance | Deadline |
|--------|-----------|---------|----------|
| **Q** | **ISO 9001:2015** | Sistema de Gestión de Calidad — procesos, control documental, mejora continua, satisfacción del cliente | Certificación futura |
| **S** | **ISO 27001:2022** | Sistema de Gestión de Seguridad de la Información — políticas, riesgos, controles Anexo A (93 controles en 4 temas) | Certificación futura |
| **I** | **IEC 62443** | Ciberseguridad industrial — desarrollo seguro de sistemas de automatización (IACS). Checklist proveedor S1-S8 (31 puntos) | **Auditoría abril 2026** |
| **C** | **EU CRA** | Cyber Resilience Act — requisitos esenciales de ciberseguridad para productos digitales en la UE. Anexos I, II, V, VII | Entrada en vigor 2027 |

### Cláusulas Obligatorias ISO 9001:2015

| Cláusula | Requisito | Qué significa para Aquafrisch |
|----------|-----------|-------------------------------|
| Q:4.1 | Contexto de la organización | Entender factores internos/externos que afectan al SGC |
| Q:4.2 | Partes interesadas | Identificar clientes, proveedores, empleados, reguladores |
| Q:4.3 | Alcance del SGC | Definir qué cubre el sistema de calidad |
| Q:4.4 | SGC y sus procesos | Mapa de procesos de la empresa |
| Q:5.1 | Liderazgo y compromiso | Dirección comprometida con calidad |
| Q:5.2 | Política de calidad | Política firmada por dirección |
| Q:5.3 | Roles y responsabilidades | Quién hace qué en calidad |
| Q:6.1 | Riesgos y oportunidades | Análisis de riesgos (compartido con 27001) |
| Q:6.2 | Objetivos de calidad | Objetivos medibles anuales |
| Q:7.1 | Recursos | Recursos necesarios para el SGC |
| Q:7.2 | Competencia | Formación y competencias del personal |
| Q:7.3 | Toma de conciencia | El personal entiende la política de calidad |
| Q:7.4 | Comunicación | Qué, cuándo, a quién, cómo comunicar |
| Q:7.5 | Información documentada | Control de documentos y registros = PGD |
| Q:8.1 | Planificación operacional | Planificar cómo se hacen los proyectos |
| Q:8.2 | Requisitos de productos/servicios | Captar y revisar requisitos del cliente |
| Q:8.3 | Diseño y desarrollo | Proceso de diseño (ingeniería + SW + PLC) |
| Q:8.4 | Proveedores externos | Control de compras y subcontratistas |
| Q:8.5 | Producción y servicio | Fabricación, instalación, puesta en marcha |
| Q:8.6 | Liberación de productos | Criterios para entregar al cliente |
| Q:8.7 | Salidas no conformes | Qué hacer cuando algo sale mal |
| Q:9.1 | Seguimiento y medición | KPIs, indicadores, satisfacción cliente |
| Q:9.2 | Auditoría interna | Programa de auditorías internas |
| Q:9.3 | Revisión por la dirección | Revisión periódica por dirección |
| Q:10.1 | Mejora | Determinar oportunidades de mejora |
| Q:10.2 | No conformidad y acción correctiva | Registro NC + acciones correctivas |
| Q:10.3 | Mejora continua | Plan de mejora continua |

### Cláusulas Obligatorias ISO 27001:2022

| Cláusula | Requisito | Qué significa para Aquafrisch |
|----------|-----------|-------------------------------|
| S:4.1-4.2 | Contexto y partes interesadas | Entender el entorno de seguridad |
| S:4.3 | Alcance del SGSI | Qué sistemas/datos cubre el SGSI |
| S:5.1-5.3 | Liderazgo, política, roles | Política de seguridad + organigrama |
| S:6.1 | Evaluación de riesgos | Metodología + registro de riesgos |
| S:6.1.3d | Declaración de Aplicabilidad (SoA) | Qué controles del Anexo A aplican y cuáles no |
| S:6.2 | Objetivos de seguridad | Objetivos medibles de seguridad |
| S:7.1-7.5 | Soporte | Recursos, competencia, documentación |
| S:8.1-8.3 | Operación | Ejecutar plan de riesgos |
| S:9.1 | Monitorización y medición | KPIs de seguridad |
| S:9.2 | Auditoría interna | Programa de auditorías SGSI |
| S:9.3 | Revisión por dirección | Revisión periódica por dirección |
| S:10.1-10.2 | Mejora | NC + mejora continua |

### Controles Anexo A — ISO 27001:2022 (93 controles, 4 temas)

| Tema | Controles | Controles clave para Aquafrisch |
|------|-----------|--------------------------------|
| A.5 Organizativos (37) | Políticas, roles, activos, acceso, proveedores, incidentes, continuidad, cumplimiento | A.5.1, A.5.2, A.5.9, A.5.12-13, A.5.15-18, A.5.19-21, A.5.24-26, A.5.29-30 |
| A.6 Personas (8) | Selección, empleo, concienciación, disciplina, terminación, teletrabajo | A.6.1-A.6.8 |
| A.7 Físicos (14) | Perímetro, oficinas, equipos, cableado, mantenimiento, retirada | A.7.1-A.7.4 |
| A.8 Tecnológicos (34) | Dispositivos, acceso, crypto, desarrollo seguro, red, logs, vulnerabilidades | A.8.1-5, A.8.9, A.8.15-16, A.8.20-22, A.8.24-28 |

### Checklist IEC 62443 — Proveedor (S1-S8, 31 puntos)

| Sección | Tema | Puntos |
|---------|------|--------|
| S1 | Gestión de ciberseguridad | S1.1 Política, S1.2 Estrategia, S1.3 KPIs, S1.4 Organización, S1.5 Responsable |
| S2 | Seguridad física y de personas | S2.1 Física, S2.2 Red, S2.3 Cuentas, S2.4 Incidentes, S2.5 Acceso |
| S3 | Seguridad OT | S3.1 Separación IT/OT, S3.2 Gestión activos OT, S3.3 Actualización OT |
| S4 | Desarrollo seguro | S4.1 SDL, S4.2 Requisitos seguridad, S4.3 Coding seguro, S4.4 Verificación |
| S5 | Gestión de software | S5.1 SBOM, S5.2 Integridad |
| S6 | Gestión de cadena de suministro | S6.1 Requisitos proveedor, S6.2 Evaluación, S6.3 Monitoreo |
| S7 | Gestión de vulnerabilidades | S7.1 Proceso, S7.2 Comunicación |
| S8 | Gestión de proyectos | S8.1 Roles seguridad, S8.2 Integración seguridad |

### Requisitos EU CRA (Cyber Resilience Act)

| Anexo | Contenido | Qué debe hacer Aquafrisch |
|-------|-----------|---------------------------|
| **Anexo I.1** | Requisitos esenciales ciberseguridad | (a) Sin vulnerabilidades conocidas (b) Config segura por defecto (c) Proteger datos en tránsito (d) Proteger contra acceso no autorizado (e) Minimizar superficie de ataque (f) Minimizar impacto incidentes (g) Registrar actividad/eventos (h) Mecanismo de actualización seguro |
| **Anexo I.2** | Gestión de vulnerabilidades | Identificar, documentar, remediar vulnerabilidades. Política de divulgación coordinada |
| **Anexo II** | Información al usuario | Manual de usuario con: nombre producto, fabricante, punto de contacto, propósito, instrucciones instalación, soporte, vulnerabilidades conocidas |
| **Anexo V** | Declaración de conformidad | Declaración formal EU CRA |
| **Anexo VII** | Documentación técnica | Descripción general, diseño, desarrollo, producción, evaluación de riesgos, pruebas aplicadas |

---

## Leyenda de Estados

| Icono | Significado |
|-------|-------------|
| ✅ | **EXISTE** — Ya lo tenemos escrito |
| 🔴 | **CREAR AUDITORÍA** — Necesario para auditoría IEC 62443 abril 2026 |
| 🟡 | **CREAR FUTURO** — Importante pero no urgente |
| 🟢 | **CREAR NORMATIVA** — Necesario para cumplimiento ISO 9001 / 27001 / CRA (no urgente) |
| ⬜ | **PER-MACHINE** — Se crea por cada instalación/proyecto |
| ⚠️ | **RESTRINGIDO** — Credenciales, no publicar nunca |

## Leyenda de Ubicaciones

| Icono | Sistema | Descripción |
|-------|---------|-------------|
| 🏢 | **DMS Empresa** | Documentación **interna**. NUNCA va al PC del cliente. Políticas, auditorías, calidad, ingeniería, contratos, SBOM... |
| 🖥️M | **Supervisor Master** | Documentación para el **cliente/ingeniero**, **siempre igual** para todos los proyectos. Manual, ficha técnica, guías... |
| 🖥️P | **Supervisor Project** | Documentación **específica de ESTA máquina**. Config Excel, modelos 3D, mantenimiento, libro de máquina... |

## Leyenda de Normativas (columna "Norma")

| Código | Normativa |
|--------|-----------|
| **Q:X.X** | ISO 9001:2015 cláusula X.X |
| **S:X.X** | ISO 27001:2022 cláusula X.X |
| **S:A.X.X** | ISO 27001:2022 Anexo A control X.X |
| **I:SX.X** | IEC 62443 checklist punto SX.X |
| **C:I.X** | EU CRA Anexo I.X |
| **C:II** | EU CRA Anexo II (info al usuario) |
| **C:V** | EU CRA Anexo V (declaración conformidad) |
| **C:VII** | EU CRA Anexo VII (doc técnica) |

## Sistema de Numeración Jerárquica

> **Formato: `XX.Y-ZZ`** donde:
> - `XX` = categoría (00-10)
> - `Y` = subcategoría (1-8)
> - `ZZ` = número de documento dentro de esa subcategoría (01-99)
>
> **Ejemplo**: `02.1-05` = Categoría 02 (Seguridad), Subcategoría 1 (Políticas), Documento 05
>
> **Ventaja**: Añadir un documento en la subcategoría 01.1 (ej: `01.1-05`) **NO afecta** a ningún documento de 01.2, 02.x, 03.x, etc. Cada subcategoría tiene su propio espacio de numeración independiente.

---

## Visión General del Árbol

```
🏭 AQUAFRISCH — GESTIÓN DOCUMENTAL (Maquinaria talleres ferroviarios)
│                                                              Ubic.  Normativa principal
│━━━ 📦 MASTER — Se escribe UNA VEZ, sirve para TODAS las máquinas ━━━
│
├── 🌐 00 PÚBLICO              5 subcat   14+ docs              🏢+🖥️M  C:V, C:II, I:S7
├── 📋 01 CALIDAD              8 subcat   29 docs               🏢      Q:4-10 (TODO)
├── 🔒 02 SEGURIDAD            7 subcat   32 docs               🏢      S:4-10, I:S1-S3, C:I
├── 💻 03 SOFTWARE             6 subcat   23 docs               🏢      I:S4-S5, C:VII, Q:8.3
├── 📖 04 MANUALES             4 subcat   10 docs               🖥️M     C:II, Q:7.2-7.3
├── 📐 05 PLANTILLAS           4 subcat   10 docs               🏢      Q:8.1, Q:8.4
│
│━━━ 🔧 PER-MACHINE — Se repite para CADA instalación ━━━
│
├── 🏗️ 06 PROYECTO  ① Vender    5 subcat   12 docs              🏢      Q:8.2, I:S6, I:S8
├── ⚡ 07 INGENIERÍA ② Diseñar   8 subcat   14 docs              🏢      Q:8.3
├── 🔧 08 TWINCAT   ③ Programar 5 subcat   10 docs              🏢      I:S3, Q:8.3
├── ⚙️ 09 CONFIG SW  ④ Config    3 subcat    9 docs              🖥️M+🖥️P Q:8.3, C:I.1(b)
├── 🛠️ 10 OPERACIONES ⑤ Mantener 5 subcat   15 docs             🖥️P     Q:8.5-8.6
│
├── ⛔ INTERNO                                3 docs              🏢      —
│
│━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
│ TOTALES: 11 categorías │ 60 subcategorías │ ~172 posiciones
│
│ POR ESTADO:
│   ✅ Existentes ........... 49
│   🔴 Auditoría IEC 62443 .. 14  (abril 2026)
│   🟢 Normativa ISO/CRA .... 22
│   🟡 Futuro ............... 28
│   ⬜ Per-machine ........... 46
│   ⚠️ Restringido ........... 3
│
│ POR UBICACIÓN:
│   🏢  DMS Empresa .............. ~121 docs
│   🖥️M Supervisor Master ........  ~18 docs
│   🖥️P Supervisor Project ........  ~23 docs
│
│ POR NORMATIVA:
│   Q ISO 9001 .................. ~60 docs contribuyen
│   S ISO 27001 ................. ~46 docs contribuyen
│   I IEC 62443 ................. ~25 docs contribuyen
│   C EU CRA .................... ~20 docs contribuyen
│━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

---

## Tres Ubicaciones, Un Solo Árbol

> El árbol documental es ÚNICO, pero cada documento tiene una **ubicación física** diferente según quién lo necesita y si varía entre máquinas.

```
┌─────────────────────────────────────────────────────────────────┐
│  🏢 DMS EMPRESA                                                │
│  (Software nuevo, Fase 2: mayo-dic 2026)                       │
│  → Red INTERNA de Aquafrisch, NUNCA al PC del cliente           │
│  → ~115 docs: políticas, calidad, ingeniería, contratos...     │
│  → OBJETIVO: ISO 9001 + ISO 27001 + orden empresa              │
├─────────────────────────────────────────────────────────────────┤
│  🖥️M AQUAFRISCH SUPERVISOR — MASTER                             │
│  (Ya existe al 80%, completar Fase 1)                           │
│  → IGUAL para TODAS las máquinas/modelos                        │
│  → ~18 docs: manual, ficha técnica, guías, vulnerability...    │
│  → OBJETIVO: Pasar auditoría IEC 62443 abril 2026              │
├─────────────────────────────────────────────────────────────────┤
│  🖥️P AQUAFRISCH SUPERVISOR — PROJECT                            │
│  (Se crea para cada instalación/máquina)                        │
│  → ESPECÍFICA de ESTA máquina                                   │
│  → ~23 docs: config, 3D, mantenimiento, libro de máquina...    │
│  → OBJETIVO: Documentación operativa en planta                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🗂️ Vista Explorador — Árbol Completo (v5.1 — Numeración Jerárquica)

```
🏭 AQUAFRISCH — ÁRBOL DOCUMENTAL v5.1 MULTINORMATIVA
│
│  Numeración: XX.Y-ZZ = Categoría.Subcategoría-Documento
│  Añadir docs en una subcategoría NO afecta a ninguna otra
│
│━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
│  📦 MASTER — Se escribe UNA VEZ, sirve para TODAS las máquinas
│━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
│
├── 🌐 00 PÚBLICO                                 🏢+🖥️M  C:V, C:II, I:S7
│   ├── 00.1 Certificaciones
│   │   ├── 00.1-01 🟡 Certificado ISO 9001                    Q:toda
│   │   ├── 00.1-02 🟡 Certificado ISO 27001                   S:toda
│   │   ├── 00.1-03 🟡 Declaración IEC 62443                   I:toda
│   │   └── 00.1-04 🟡 Certificado EU CRA                      C:toda
│   ├── 00.2 Presentación Comercial
│   │   ├── 00.2-01 ✅ Presentación Aquafrisch Supervisor       —
│   │   ├── 00.2-02 ✅ Email comercial tipo                     —
│   │   └── 00.2-03 ✅ Screenshots funcionales (10 capturas)    —
│   ├── 00.3 Fichas Técnicas (Datasheets)
│   │   ├── 00.3-01 🟡 Datasheet Aquafrisch Supervisor (SW)     C:II · Q:8.2
│   │   ├── 00.3-02 🟡 Datasheet Lavadora de Bogies             Q:8.2
│   │   ├── 00.3-03 🟡 Datasheet Torno de Ruedas                Q:8.2
│   │   └── 00.3-XX 🟡 Datasheet [modelo de máquina N]          Q:8.2
│   ├── 00.4 Declaración Conformidad
│   │   └── 00.4-01 🟢 Declaración conformidad EU CRA          C:V
│   └── 00.5 Política Vulnerabilidades
│       ├── 00.5-01 ✅ Informe vulnerabilidades conocidas       C:I.2 · C:II
│       └── 00.5-02 🔴 DOC-13: Gestión Vulnerabilidades        I:S7.1-2 · C:I.2 · S:A.8.8
│
├── 📋 01 CALIDAD                                  🏢  Q:4-10
│   ├── 01.1 SGC — Sistema Gestión Calidad
│   │   ├── 01.1-01 🟢 Manual del SGC                          Q:4.4, Q:5.3, Q:7.1
│   │   ├── 01.1-02 🟢 Política de Calidad                     Q:5.2
│   │   ├── 01.1-03 🟢 Alcance del SGC                         Q:4.3
│   │   └── 01.1-04 🟢 Contexto org. + partes interesadas      Q:4.1, Q:4.2
│   ├── 01.2 Gestión Documental
│   │   ├── 01.2-01 ✅ PGD (Plan Gestión Documental)            Q:7.5 · S:7.5
│   │   └── 01.2-02 ✅ Estructura multinormativa (borrador)     Q:7.5
│   ├── 01.3 Objetivos y Medición
│   │   ├── 01.3-01 🟢 Objetivos calidad anuales 2026          Q:6.2
│   │   ├── 01.3-02 🟢 Indicadores y medición (KPIs)           Q:9.1.1
│   │   └── 01.3-03 🟢 Satisfacción del cliente                Q:9.1.2
│   ├── 01.4 No Conformidades
│   │   ├── 01.4-01 🟢 Registro de no conformidades            Q:10.2 · S:10.1
│   │   └── 01.4-02 🟢 Procedimiento acciones correctivas      Q:10.2 · S:10.1
│   ├── 01.5 Mejora Continua
│   │   └── 01.5-01 🟢 Plan mejora continua                    Q:10.3 · S:10.2
│   ├── 01.6 Auditoría Interna y Revisión Dirección
│   │   ├── 01.6-01 🟢 Programa de auditoría interna           Q:9.2 · S:9.2
│   │   ├── 01.6-02 🟢 Procedimiento de auditoría interna      Q:9.2 · S:9.2
│   │   └── 01.6-03 🟢 Actas revisión por dirección            Q:9.3 · S:9.3
│   ├── 01.7 Riesgos y Oportunidades
│   │   └── 01.7-01 🟢 Análisis riesgos y oportunidades        Q:6.1
│   └── 01.8 Procedimientos Generales del SGC (PGs existentes)
│       ├── 01.8-01 ✅ MSG 00 Manual del Sistema de Gestión     Q:4.4, Q:5.1-3
│       ├── 01.8-02 ✅ PG 01 Control Información Documentada   Q:7.5
│       ├── 01.8-03 ✅ PG 02 Gestión de NC y Reclamaciones     Q:8.7, Q:10.2
│       ├── 01.8-04 ✅ PG 04 Gestión de Recursos              Q:7.1
│       ├── 01.8-05 ✅ PG 05 Control Producto y Servicio       Q:8.1-8.7
│       ├── 01.8-06 ✅ PG 06 Gestión de Compras               Q:8.4
│       ├── 01.8-07 ✅ PG 07 Preparación y Rpta Emergencias    Q:6.1 · S:A.5.29-30
│       ├── 01.8-08 ✅ PG 08 Ventas de Producto y Servicio     Q:8.2, Q:9.1.2
│       ├── 01.8-09 ✅ PG 09 Servicio Postventa                Q:8.5.5, Q:9.1.2
│       ├── 01.8-10 ✅ PG 10 Gestión Riesgos y Oportunidades   Q:6.1, Q:4.1
│       ├── 01.8-11 ✅ PG 11 Comunicación, Participación, Consulta Q:7.4
│       ├── 01.8-12 ✅ PG 12 Control Operacional (MA+SST)       Q:8.1
│       └── 01.8-13 ✅ PG 13 Seguridad de la Información ⚠️R    S:5.2, S:A.5-A.8
│
├── 🔒 02 SEGURIDAD                                🏢  S:4-10, I:S1-S3, C:I
│   ├── 02.1 Políticas de Seguridad de la Información
│   │   ├── 02.1-01 ✅ Security overview                        S:A.5.1
│   │   ├── 02.1-02 ✅ Resumen ciberseguridad                   S:A.5.1
│   │   ├── 02.1-03 ✅ Roles y permisos del sistema             S:A.5.2 · I:S1.4
│   │   ├── 02.1-04 ✅ Quickstart roles                         S:A.5.2
│   │   ├── 02.1-05 🔴 DOC-01: Política Ciberseguridad          S:5.2, A.5.1 · I:S1.1
│   │   ├── 02.1-06 🔴 DOC-03: Organigrama + RACI Ciber         S:5.3, A.5.2 · I:S1.4-5
│   │   ├── 02.1-07 🔴 DOC-05: Protección Física y TI           S:A.7.1-4 · I:S2.1-2, S2.5
│   │   ├── 02.1-08 🔴 DOC-06: Gestión Cuentas TI               S:A.5.15-18, A.8.2-5 · I:S2.3
│   │   ├── 02.1-09 🔴 DOC-07: Seguridad OT (TwinCAT/PLC)      S:A.8.9 · I:S3.1-3
│   │   ├── 02.1-10 🟢 Alcance del SGSI                         S:4.3
│   │   ├── 02.1-11 🟢 Declaración Aplicabilidad (SoA)          S:6.1.3d
│   │   ├── 02.1-12 🟢 Objetivos seguridad información          S:6.2
│   │   ├── 02.1-13 🟢 Política clasificación información       S:A.5.12-13
│   │   ├── 02.1-14 🟢 Política de criptografía                 S:A.8.24 · C:I.1(c)
│   │   └── 02.1-15 🟢 Política seguridad de red                S:A.8.20-22 · I:S3.1
│   ├── 02.2 CRA EU — Cumplimiento Europeo
│   │   ├── 02.2-01 ✅ Roadmap cumplimiento CRA                 C:I.1, C:I.2
│   │   ├── 02.2-02 ✅ Gestión usuarios CRA                     C:I.1(d) · S:A.5.15
│   │   ├── 02.2-03 ✅ Sistema de logs CRA                      C:I.1(g) · S:A.8.15
│   │   └── 02.2-04 🔴 DOC-04: Plan Gestión Incidentes          I:S2.4 · S:A.5.24-26 · C:I.1(f)
│   ├── 02.3 Integridad
│   │   ├── 02.3-01 ✅ Software Integrity (firma, checksums)    I:S5.2 · C:I.1(h) · S:A.8.25
│   │   ├── 02.3-02 ✅ Estado integridad actual                  I:S5.2
│   │   └── 02.3-03 ✅ Versión deploy producción                 I:S5.2 · C:I.1(h)
│   ├── 02.4 Auditorías y Evaluaciones
│   │   ├── 02.4-01 🔴 DOC-02: Estrategia Ciberseg. + KPIs     I:S1.2-3 · S:9.1
│   │   └── 02.4-02 🔴 DOC-12: Evaluación Terceros              I:S6.3 · S:A.5.19-21 · Q:8.4
│   ├── 02.5 Gestión de Riesgos
│   │   ├── 02.5-01 🟢 Metodología evaluación de riesgos        S:6.1.2 · Q:6.1
│   │   ├── 02.5-02 🟢 Registro riesgos + Plan tratamiento      S:6.1.3, S:8.3 · Q:6.1
│   │   └── 02.5-03 🟢 Inventario activos de información        S:A.5.9-11
│   ├── 02.6 Continuidad de Negocio
│   │   ├── 02.6-01 🟢 Plan Continuidad Negocio (BCP)           S:A.5.29-30
│   │   └── 02.6-02 🟢 Plan Recuperación Desastres (DRP)        S:A.5.30 · Q:6.1
│   └── 02.7 Seguridad del Personal
│       ├── 02.7-01 🟢 Procedimiento seguridad RRHH             S:A.6.1-6
│       ├── 02.7-02 🟢 Plan concienciación seguridad            S:A.6.3 · Q:7.3 · I:S1.1
│       └── 02.7-03 🟢 Acuerdos confidencialidad (NDA)          S:A.6.6 · S:A.5.14
│
├── 💻 03 SOFTWARE                                  🏢  I:S4-S5, C:VII, Q:8.3
│   ├── 03.1 Arquitectura del Sistema
│   │   ├── 03.1-01 ✅ Arquitectura y despliegue                 Q:8.3 · C:VII · S:A.8.25
│   │   ├── 03.1-02 ✅ Arquitectura de logs                      C:I.1(g) · S:A.8.15
│   │   ├── 03.1-03 ✅ Gestión de datos (backup, restore)        S:A.8.13 · C:I.1(f)
│   │   ├── 03.1-04 ✅ Sistema gestión documental                Q:7.5
│   │   ├── 03.1-05 ✅ Implementación modelos 3D                 Q:8.3
│   │   ├── 03.1-06 ✅ Sistema multi-proyecto                    Q:8.3 · S:A.8.31
│   │   └── 03.1-07 ✅ System Config implementation              Q:8.3 · C:I.1(b)
│   ├── 03.2 SDL — Desarrollo Seguro
│   │   ├── 03.2-01 ✅ Guía de desarrollo                        Q:8.3 · S:A.8.25
│   │   ├── 03.2-02 ✅ Ejemplo API Backend                       Q:8.3
│   │   ├── 03.2-03 ✅ Integración Backend                       Q:8.3
│   │   ├── 03.2-04 ✅ Integración Frontend bombas               Q:8.3
│   │   ├── 03.2-05 ✅ Implementación pump elements              Q:8.3
│   │   └── 03.2-06 🔴 DOC-08: SDL — Desarrollo Seguro          I:S4.1-2, S4.4 · S:A.8.25-27 · C:I.1(a,e)
│   ├── 03.3 Guías Codificación Segura
│   │   └── 03.3-01 🔴 DOC-09: Secure Coding Guidelines         I:S4.3 · S:A.8.28 · C:I.1(a)
│   ├── 03.4 SBOM y Terceros
│   │   ├── 03.4-01 ✅ Índice de terceros                        I:S5.1 · S:A.5.19 · Q:8.4
│   │   ├── 03.4-02 ✅ README Beckhoff                           I:S5.1 · S:A.5.19
│   │   ├── 03.4-03 ✅ Config Beckhoff propia                    I:S5.1
│   │   └── 03.4-04 🔴 DOC-10: SBOM Formal                      I:S5.1 · C:VII · S:A.5.19
│   ├── 03.5 Testing y Changelog
│   │   ├── 03.5-01 ✅ Estado integración                        Q:8.6 · I:S4.4
│   │   ├── 03.5-02 ✅ Resumen trabajo nocturno                  Q:8.3
│   │   ├── 03.5-03 🟡 Plan de testing formal                   Q:8.6 · I:S4.4 · S:A.8.29
│   │   └── 03.5-04 🟢 Release Notes / Changelog formal         C:II · I:S5.2 · Q:8.6
│   └── 03.6 Documentación Técnica CRA
│       └── 03.6-01 🟢 Documentación técnica formal EU CRA      C:VII
│
├── 📖 04 MANUALES                                  🖥️M  C:II, Q:7.2-7.3
│   ├── 04.1 Manual de Usuario
│   │   ├── 04.1-01 ✅ Manual recuperación usuario               C:II · Q:8.5
│   │   └── 04.1-02 🟢 Manual usuario completo (CRA Anexo II)   C:II (obligatorio)
│   ├── 04.2 Manual de Instalación
│   │   ├── 04.2-01 ✅ Instalación producción completa           C:II · Q:8.5
│   │   ├── 04.2-02 ✅ Cómo usar nueva versión                   C:I.1(h) · C:II
│   │   ├── 04.2-03 ✅ Configuración modo Kiosk                  Q:8.5
│   │   └── 04.2-04 ✅ Deploy servidor empresa                   Q:8.5
│   ├── 04.3 Formación
│   │   ├── 04.3-01 🟡 Material formación operadores            Q:7.2-3 · S:A.6.3
│   │   └── 04.3-02 🟡 Guía rápida de arranque                  C:II · Q:7.2
│   └── 04.4 FAQ / Troubleshooting
│       ├── 04.4-01 ✅ Troubleshooting animación PLC             Q:8.7
│       └── 04.4-02 🟡 FAQ general del producto                  C:II
│
├── 📐 05 PLANTILLAS                                🏢  Q:8.1, Q:8.4
│   ├── 05.1 Checklist Nuevo Proyecto
│   │   ├── 05.1-01 🟡 Checklist arranque proyecto nuevo         Q:8.1
│   │   └── 05.1-02 🟡 Checklist entrega final                  Q:8.6
│   ├── 05.2 Formatos Estándar
│   │   ├── 05.2-01 🟡 Template informe ingeniería              Q:7.5
│   │   ├── 05.2-02 🟡 Template acta reunión                    Q:7.4
│   │   └── 05.2-03 🟡 Template informe test                    Q:8.6 · I:S4.4
│   ├── 05.3 Componentes Homologados
│   │   ├── 05.3-01 ✅ Vocabulario máquina (glosario)            Q:7.4
│   │   ├── 05.3-02 🟡 Lista componentes aprobados              Q:8.4 · S:A.5.19
│   │   └── 05.3-03 🟡 Proveedores homologados                  Q:8.4 · S:A.5.19 · I:S6.3
│   └── 05.4 Criterios de Aceptación
│       ├── 05.4-01 🟡 Criterios aceptación SW (SAT/FAT)        Q:8.6 · I:S4.4
│       └── 05.4-02 🟡 Criterios aceptación HW                  Q:8.6
│
│━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
│  🔧 PER-MACHINE — Se repite para CADA instalación
│━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
│
├── 🏗️ 06 PROYECTO — ① Vender                      🏢  Q:8.2, I:S6, I:S8
│   ├── 06.1 Oferta Comercial
│   │   ├── 06.1-01 ⬜ Oferta técnica-económica                  Q:8.2
│   │   └── 06.1-02 ⬜ Presupuesto detallado                     Q:8.2
│   ├── 06.2 Especificaciones Cliente
│   │   ├── 06.2-01 ✅ Análisis estándares IT del cliente         S:A.5.36 · I:S6.1
│   │   ├── 06.2-02 🔴 DOC-11: Requisitos Ciber Producto         I:S6.1-2 · S:A.5.19 · C:I.1
│   │   ├── 06.2-03 ⬜ Especificaciones técnicas del cliente      Q:8.2.2-3
│   │   └── 06.2-04 🟢 Revisión requisitos del contrato          Q:8.2.3
│   ├── 06.3 Contrato
│   │   ├── 06.3-01 ⬜ Contrato firmado                           Q:8.2.3
│   │   └── 06.3-02 ⬜ Condiciones de garantía                    Q:8.2.1 · C:II
│   ├── 06.4 Plan de Proyecto
│   │   ├── 06.4-01 ⬜ Cronograma proyecto (Gantt)                Q:8.1 · I:S8.2
│   │   └── 06.4-02 🔴 DOC-14: Roles Ciber por Proyecto          I:S8.1-2 · S:A.5.2
│   └── 06.5 Actas y Comunicaciones
│       ├── 06.5-01 ⬜ Actas reunión con cliente                  Q:7.4
│       └── 06.5-02 ⬜ Correspondencia técnica relevante          Q:7.4
│
├── ⚡ 07 INGENIERÍA — ② Diseñar                    🏢  Q:8.3
│   ├── 07.1 Esquemas Eléctricos
│   │   ├── 07.1-01 ⬜ Esquemas de potencia                       Q:8.3.5
│   │   ├── 07.1-02 ⬜ Esquemas de control/maniobra               Q:8.3.5
│   │   └── 07.1-03 ⬜ Lista de cables                             Q:8.3.5
│   ├── 07.2 P&ID (Piping & Instrumentation)
│   │   ├── 07.2-01 ⬜ Diagrama tuberías e instrumentación        Q:8.3.5
│   │   └── 07.2-02 ⬜ Lista de instrumentos                      Q:8.3.5
│   ├── 07.3 Layout / Implantación
│   │   ├── 07.3-01 ⬜ Layout planta 2D                            Q:8.3.5
│   │   └── 07.3-02 ⬜ Layout 3D (si aplica)                       Q:8.3.5
│   ├── 07.4 Planos Mecánicos
│   │   ├── 07.4-01 ⬜ Planos de conjunto                          Q:8.3.5
│   │   └── 07.4-02 ⬜ Planos de detalle / despiece                Q:8.3.5
│   ├── 07.5 Esquemas Neumáticos/Hidráulicos
│   │   └── 07.5-01 ⬜ Esquemas neumáticos (si aplica)             Q:8.3.5
│   ├── 07.6 BOM — Lista de Materiales
│   │   ├── 07.6-01 ⬜ BOM materiales completa                     Q:8.3.5 · Q:8.4
│   │   └── 07.6-02 ⬜ BOM componentes eléctricos                  Q:8.3.5 · Q:8.4
│   ├── 07.7 Datasheets Componentes
│   │   └── 07.7-01 ⬜ Fichas técnicas equipos instalados          Q:8.4
│   └── 07.8 Planos As-Built
│       └── 07.8-01 ⬜ Planos "como quedó" (cambios vs diseño)     Q:8.3.6 · Q:8.5.6
│
├── 🔧 08 TWINCAT / PLC — ③ Programar              🏢  I:S3, Q:8.3
│   ├── 08.1 Proyecto TwinCAT
│   │   ├── 08.1-01 ⬜ Archivo proyecto .tsproj (backup)           Q:8.3.5 · I:S3.2
│   │   └── 08.1-02 ⬜ Versión TwinCAT y runtime                   I:S3.3 · I:S5.1
│   ├── 08.2 Configuración I/O
│   │   ├── 08.2-01 ⬜ Mapa de I/O (entradas/salidas)              Q:8.3.5 · I:S3.2
│   │   └── 08.2-02 ⬜ Lista de señales con direcciones             Q:8.3.5
│   ├── 08.3 EtherCAT
│   │   ├── 08.3-01 ⬜ Topología red EtherCAT                      I:S3.1 · S:A.8.20
│   │   └── 08.3-02 ⬜ Config esclavos + firmware                   I:S3.2-3
│   ├── 08.4 Recetas PLC
│   │   ├── 08.4-01 ⬜ Definición de recetas                        Q:8.5.1
│   │   └── 08.4-02 ⬜ Parámetros de proceso                        Q:8.5.1
│   └── 08.5 Documentación PLC
│       ├── 08.5-01 ⬜ Descripción funcional del PLC                Q:8.3.5 · C:VII
│       └── 08.5-02 ⬜ Lista de Function Blocks                      Q:8.3.5
│
├── ⚙️ 09 CONFIG SW — ④ Configurar                  🖥️M+🖥️P  Q:8.3, C:I.1(b)
│   ├── 09.1 Excel Config
│   │   ├── 09.1-01 ✅ Referencia estructura Excel 15 cols         Q:8.3.5
│   │   ├── 09.1-02 ✅ Mapeo columnas Excel                        Q:8.3.5
│   │   ├── 09.1-03 ✅ System Config sheet                         Q:8.3.5 · C:I.1(b)
│   │   ├── 09.1-04 ✅ Configuración elementos 3D                  Q:8.3.5
│   │   └── 09.1-05 ⬜ ProjectConfig.xlsm de esta máquina          Q:8.3.5 · S:A.8.9
│   ├── 09.2 Modelos 3D
│   │   ├── 09.2-01 ⬜ Archivos .glb de esta máquina               Q:8.3.5
│   │   └── 09.2-02 🟡 Guía configuración modelos 3D              Q:8.3.5
│   └── 09.3 Base de Datos Proyecto
│       ├── 09.3-01 ⬜ project.db de esta máquina                   S:A.8.13
│       └── 09.3-02 🟡 Esquema/documentación de la DB              Q:8.3.5 · S:A.8.13
│
├── 🛠️ 10 OPERACIONES — ⑤ Mantener                  🖥️P  Q:8.5-8.6
│   ├── 10.1 Mantenimiento Preventivo
│   │   ├── 10.1-01 ⬜ Plan mantenimiento preventivo               Q:8.5.1 · S:A.7.13
│   │   ├── 10.1-02 ⬜ Checklist inspección periódica              Q:8.5.1
│   │   └── 10.1-03 ⬜ Calendario de mantenimiento                 Q:8.5.1
│   ├── 10.2 Mantenimiento Correctivo
│   │   ├── 10.2-01 ⬜ Registro de averías                         Q:8.7 · Q:10.2
│   │   ├── 10.2-02 ⬜ Informes de reparación                      Q:8.7
│   │   └── 10.2-03 ⬜ Análisis causa raíz                         Q:10.2
│   ├── 10.3 Repuestos
│   │   ├── 10.3-01 ⬜ Lista repuestos recomendados                Q:8.5.3 · C:II
│   │   └── 10.3-02 ⬜ Stock mínimo                                Q:8.5.3
│   ├── 10.4 Histórico Máquina
│   │   ├── 10.4-01 ⬜ Libro de máquina                            Q:8.5.2 · Q:7.5
│   │   ├── 10.4-02 ⬜ Registro de modificaciones                  Q:8.5.6 · S:A.8.32
│   │   └── 10.4-03 ⬜ Histórico de alarmas                        C:I.1(g) · S:A.8.15
│   └── 10.5 Puesta en Marcha
│       ├── 10.5-01 ⬜ Protocolo puesta en marcha                  Q:8.6 · I:S4.4
│       ├── 10.5-02 ⬜ Checklist commissioning                     Q:8.6
│       └── 10.5-03 ⬜ Acta de recepción firmada                   Q:8.6 · Q:8.2.3
│
└── ⛔ INTERNO — Fuera del Árbol (RESTRINGIDO)       🏢
    ├── INT-01 ⚠️ Credenciales iniciales clientes                 S:A.5.17
    ├── INT-02 ⚠️ Credenciales internas Aquafrisch                S:A.5.17
    └── INT-03 ⚠️ Documentación interna varios                    —

RESUMEN:
  ✅ Existentes ........... 49    🖥️M Supervisor Master .... ~18
  🔴 Auditoría IEC 62443 .. 14    🖥️P Supervisor Project ... ~23
  🟢 Normativa ISO/CRA .... 22    🏢  DMS Empresa ......... ~121
  🟡 Futuro ............... 28
  ⬜ Per-machine ........... 46    Q ISO 9001 contribuyen .. ~60
  ⚠️ Restringido ........... 3    S ISO 27001 contribuyen . ~46
  ─────────────────────────────    I IEC 62443 contribuyen . ~25
  TOTAL .................. ~172    C EU CRA contribuyen .... ~20
```

---

# DETALLE COMPLETO POR CATEGORÍA — CON CLÁUSULAS NORMATIVAS

> Cada documento muestra exactamente **qué cláusula** de cada normativa cubre.  
> Formato: `Q:5.2` = ISO 9001 cl.5.2 | `S:A.5.1` = ISO 27001 Anexo A ctrl 5.1 | `I:S1.1` = IEC 62443 punto S1.1 | `C:I.1(a)` = CRA Anexo I.1(a)  
> Numeración: `XX.Y-ZZ` = Categoría.Subcategoría-Documento (inmune a inserciones)

---

## 🌐 00 PÚBLICO

> **Clasificación**: 🟢 Público  
> **Responsable**: Dirección / Comercial  
> **¿Qué es?**: Lo que puede ver cualquiera — clientes, auditores, web  
> **Normativas**: EU CRA Anexo II y V (principal), ISO 9001 (certificaciones)

### 00.1 Certificaciones
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 00.1-01 | Certificado ISO 9001 | 🟡 | 🏢 | Q:toda | Cuando se certifique |
| 00.1-02 | Certificado ISO 27001 | 🟡 | 🏢 | S:toda | Cuando se certifique |
| 00.1-03 | Declaración IEC 62443 | 🟡 | 🏢 | I:toda | Cuando se certifique |
| 00.1-04 | Certificado EU CRA | 🟡 | 🏢 | C:toda | Cuando entre en vigor |

### 00.2 Presentación Comercial
> Presentación del producto software **Aquafrisch Supervisor** (el SW es siempre el mismo; las máquinas varían).

| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 00.2-01 | Presentación Aquafrisch Supervisor | ✅ | 🏢 | — | `presentacion/Aquafrisch_Supervisor_Core_2026.pptx` |
| 00.2-02 | Email comercial tipo | ✅ | 🏢 | — | `presentacion/email_comercial.html` |
| 00.2-03 | Screenshots funcionales (10 capturas) | ✅ | 🏢 | — | `presentacion/01_login.png` → `10_hardware_monitor.png` |

### 00.3 Fichas Técnicas (Datasheets)
> **Datasheets comerciales** — tanto del software como de cada modelo de máquina que fabricamos.  
> Cada tipología de máquina tiene su propio datasheet con: especificaciones técnicas, dimensiones, capacidades, prestaciones.  
> El datasheet del **software Supervisor** es independiente (requisito CRA Anexo II).  
> Añadir modelos nuevos: simplemente crear `00.3-04`, `00.3-05`, etc.

| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 00.3-01 | Datasheet Aquafrisch Supervisor (SW) | 🟡 | 🖥️M | **C:II** · Q:8.2 | Requisito CRA Anexo II: info del producto digital |
| 00.3-02 | Datasheet Lavadora de Bogies | 🟡 | 🏢 | Q:8.2 | Especificaciones, capacidades, dimensiones |
| 00.3-03 | Datasheet Torno de Ruedas | 🟡 | 🏢 | Q:8.2 | Especificaciones, capacidades, dimensiones |
| 00.3-XX | Datasheet [modelo de máquina N] | 🟡 | 🏢 | Q:8.2 | Un datasheet por cada tipología que fabriquéis |

### 00.4 Declaración Conformidad
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 00.4-01 | Declaración conformidad EU CRA | 🟢 | 🖥️M | **C:V** | Declaración formal según modelo Anexo V |

### 00.5 Política Vulnerabilidades
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 00.5-01 | Informe vulnerabilidades conocidas | ✅ | 🖥️M | C:I.2 · C:II | `user-guides/VULNERABILITY_REPORT.md` |
| 00.5-02 | **DOC-13: Proceso Gestión Vulnerabilidades** | 🔴 | 🖥️M | **I:S7.1, S7.2** · C:I.2 · S:A.8.8 | **CREAR** ~5 págs |

---

## 📋 01 CALIDAD

> **Clasificación**: 🔵 Interno  
> **Responsable**: Responsable Calidad / Dirección  
> **¿Qué es?**: El sistema de gestión de calidad — la columna vertebral de ISO 9001  
> **Normativas**: **ISO 9001 (principal)** — cláusulas 4 a 10 completas

### 01.1 SGC — Sistema Gestión Calidad
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 01.1-01 | Manual del SGC | 🟢 | 🏢 | **Q:4.4, Q:5.3, Q:7.1** | Mapa de procesos, roles, recursos |
| 01.1-02 | Política de Calidad (firmada dirección) | 🟢 | 🏢 | **Q:5.2** | Firmada por Director General |
| 01.1-03 | Alcance del SGC | 🟢 | 🏢 | **Q:4.3** | Qué procesos/productos/sitios cubre |
| 01.1-04 | Contexto de la organización + partes interesadas | 🟢 | 🏢 | **Q:4.1, Q:4.2** | Análisis DAFO, stakeholders |

### 01.2 Gestión Documental
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 01.2-01 | Plan de Gestión Documental (PGD) | ✅ | 🏢 | **Q:7.5** · S:7.5 | `architecture/PGD_PLAN_GESTION_DOCUMENTAL.md` |
| 01.2-02 | Estructura multinormativa (borrador) | ✅ | 🏢 | Q:7.5 | `architecture/DMS_ESTRUCTURA_MULTINORMATIVA.md` |

### 01.3 Objetivos y Medición
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 01.3-01 | Objetivos calidad anuales 2026 | 🟢 | 🏢 | **Q:6.2** | Objetivos SMART medibles |
| 01.3-02 | Indicadores y medición de procesos (KPIs) | 🟢 | 🏢 | **Q:9.1.1** | KPIs de cada proceso |
| 01.3-03 | Satisfacción del cliente | 🟢 | 🏢 | **Q:9.1.2** | Encuestas, reclamaciones, feedback |

### 01.4 No Conformidades
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 01.4-01 | Registro de no conformidades | 🟢 | 🏢 | **Q:10.2** · S:10.1 | Registro NC internas y externas |
| 01.4-02 | Procedimiento acciones correctivas | 🟢 | 🏢 | **Q:10.2** · S:10.1 | Análisis causa raíz + acciones |

### 01.5 Mejora Continua
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 01.5-01 | Plan mejora continua | 🟢 | 🏢 | **Q:10.3** · S:10.2 | Oportunidades de mejora |

### 01.6 Auditoría Interna y Revisión por Dirección
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 01.6-01 | Programa de auditoría interna | 🟢 | 🏢 | **Q:9.2** · **S:9.2** | Calendario, alcance, auditores, criterios |
| 01.6-02 | Procedimiento de auditoría interna | 🟢 | 🏢 | **Q:9.2** · **S:9.2** | Cómo se ejecuta la auditoría |
| 01.6-03 | Actas de revisión por dirección | 🟢 | 🏢 | **Q:9.3** · **S:9.3** | Entradas, salidas, decisiones, acciones |

### 01.7 Riesgos y Oportunidades
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 01.7-01 | Análisis de riesgos y oportunidades (calidad) | 🟢 | 🏢 | **Q:6.1** | Registro/output del análisis. Procedimiento: **01.8-10 PG 10** ✅. Complementa 02.5 (ciber) |

### 01.8 Procedimientos Generales del SGC
> **Procedimientos ya existentes** del Sistema Integrado de Gestión de Aquafrisch (desde 2006, rev. continua).  
> Son documentos MASTER que definen CÓMO se ejecutan los procesos. Cubren las cláusulas operativas de ISO 9001.  
> Incluyen sus propios formatos de registro (FR), instrucciones de trabajo (IT) e instrucciones técnicas (TE).

| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 01.8-01 | MSG 00 Manual del Sistema de Gestión | ✅ | 🏢 | **Q:4.4, Q:5.1-5.3** | Manual SIG (calidad + medio ambiente + SST) |
| 01.8-02 | PG 01 Control de Información Documentada | ✅ | 🏢 | **Q:7.5** | Procedimiento control de documentos y registros |
| 01.8-03 | PG 02 Gestión de No Conformidades y Reclamaciones | ✅ | 🏢 | **Q:8.7, Q:10.2** | NC internas, externas, reclamaciones cliente |
| 01.8-04 | PG 04 Gestión de Recursos | ✅ | 🏢 | **Q:7.1** | Infraestructura, calibración equipos, RRHH |
| 01.8-05 | **PG 05 Control del Producto y Servicio** | ✅ | 🏢 | **Q:8.1-8.7** | Rev.17 (oct 2024). Ciclo completo: oferta → diseño → fabricación → FAT → instalación → SAT → garantía. Incluye ~40 ITs + ~35 FRs |
| 01.8-06 | PG 06 Gestión de Compras | ✅ | 🏢 | **Q:8.4** | Evaluación proveedores, pedidos, recepción material |
| 01.8-07 | **PG 07 Preparación y Respuesta ante Emergencias** | ✅ | 🏢 | **Q:6.1** · S:A.5.29-30 | Rev.06 (nov 2024). Plan emergencias (incendios, accidentes, primeros auxilios, vertidos) + Plan de contingencia (equipo gestión crisis). Cubre parcialmente ISO 14001 y 45001. Incluye FR 0701-0705 |
| 01.8-08 | **PG 08 Ventas de Producto y Servicio** | ✅ | 🏢 | **Q:8.2** · Q:9.1.2, Q:10.3 | Rev.03 (nov 2024). Proceso completo de ventas: conocimiento producto → prospección mercado → contacto cliente (Odoo CRM) → elaboración ofertas (servicios, mantenimientos, repuestos, productos distribuidos, fabricación propia) → negociación (matriz conformidad, Battle Card, Incoterms) → aceptación y traspaso a Proyectos. Incluye capacidad carga trabajo (§6.7), encuesta satisfacción (§6.4.3), protocolo Odoo detallado. Registros: plan ventas, ofertas técnicas/económicas, battle cards |
| 01.8-09 | **PG 09 Servicio Postventa** | ✅ | 🏢 | **Q:8.5.5** · Q:9.1.2, Q:10.3 | Rev.02 (nov 2024). Gestión completa postventa: incidencias/reparaciones (nac. e internac.), formación, mantenimientos, repuestos. Gestión vía Odoo Helpdesk (tickets con prioridad/categoría/tipo soporte). Flujo: recepción notificación → ticket → diagnóstico → presupuesto → aceptación → trabajos → cierre. Control garantías (equipo en/sin garantía con presupuesto a coste 0). Encuesta satisfacción al cierre. §13 Mejora continua + §14-15 Calidad del servicio. Ref: PG 08 (ventas), PG 02 (NC) |
| 01.8-10 | **PG 10 Gestión de Riesgos y Oportunidades** | ✅ | 🏢 | **Q:6.1** · Q:4.1, Q:4.2 | Rev.03 (dic 2025). Procedimiento sistema integral riesgos/oportunidades conforme ISO 31000 + ISO 9001 + 14001 + 45001. Metodología: contexto (interno/externo) → identificación R&O → análisis (P×I=R) → clasificación severidad A/B/C/D → tratamiento (evitar, asumir, eliminar, reducir, compartir, aceptar). Matriz 5×5 probabilidad/impacto. Registros: DAFO, partes interesadas, análisis R&O. Revisión anual en Revisión por Dirección. Governa 01.7-01 |
| 01.8-11 | **PG 11 Comunicación, Participación y Consulta** | ✅ | 🏢 | **Q:7.4** | Rev.06 (dic 2024). Comunicación interna (ascendente/descendente/horizontal: reuniones, buzón sugerencias, tablón, email, WhatsApp, Odoo) + externa (comercial, web, RRSS, teléfono). §4.3 Plan comunicación contingencias (enlaza con Plan de Contingencias). §4.4 Participación y consulta SST (formularios, delegados prevención, EPIs). FR 1101-1114 (14 registros). FR 1113 Matriz comunicación distribución documentos. Ref: UNE EN 1090-1,2 + MSG 00 |
| 01.8-12 | **PG 12 Control Operacional** | ✅ | 🏢 | **Q:8.1** (MA+SST) | Rev.01 (nov 2024). Control operacional medio ambiente + seguridad y salud laboral (ISO 14001 + 45001). 5 áreas: residuos (RP/RII/RU, códigos LER, gestores autorizados), consumos (semestrales, indicadores), emisiones (focos, sustancias, depuración), vertidos (análisis externos, límites legales), riesgos SST (planificación preventiva, jerarquía controles: eliminación→sustitución→ingeniería→admin→EPIs). FR 1201 Inventario Control Operacional. Datos entrada Revisión Dirección |
| 01.8-13 | **PG 13 Procedimiento General de Seguridad de la Información** | ✅ | 🏢 | **S:5.2** · S:A.5-A.8 | ⚠️ Clasificación: **Restringido**. Rev.02 (nov 2024). Procedimiento corporativo COMPLETO de seguridad de la información (**42 capítulos**). Cubre TODOS los dominios ISO 27001 Anexo A: **A.5 Organizativos** (§20 clasificación info: confidencial/interna/pública, §6 inventarios con FR 1305 Listado Infraestructura TI, §24-25 contraseñas trimestrales y control acceso por perfiles, §37 proveedores con NDA, §28 cumplimiento legal extenso: RGPD, LSSI, PI, Código Penal), **A.6 Personas** (§33 RRHH: cláusulas contractuales, confidencialidad ingreso/baja, formación, sanciones según convenio Metal; §22 concienciación: 2 manuales PE1301 taller + PE1302 oficina, checklists anuales FR1301/FR1302), **A.7 Físicos** (§7 protección centro: telefonillo, doble puerta, alarma ESV, cámaras interiores/exteriores, bolardos, §9 tres zonas: pública/restringida/autorizada, servidor bajo llave+cámara), **A.8 Tecnológicos** (§15 antimalware AVG licencias bianuales, §10 actualizaciones sw trimestral, §26 copias seguridad con inventario completo, §32 gestión logs: acceso/sesiones/configuraciones/CPU/disco, §40 criptografía VPN+certificados, §41 wifi y redes externas, §16 aplicaciones permitidas). **§38 Protocolo completo respuesta a incidentes**: gabinete crisis (Dirección+IT+Jefe Electrónica), flujo detección→aislamiento red→recuperación backups→post-incidencia→comunicación clientes→AEPD 72h→denuncia Policía/GC. §23 Plan Continuidad Negocio (alcance, restauración por criticidad, pruebas). §34 Plan Director Seguridad (PDS: análisis riesgos, nivel madurez, certificación ISO 27001). §29 IoT, §30-31 móviles corporativos/BYOD, §39 teletrabajo seguro, §35 protección web (CMS, HTTPS, RGPD), §21 comercio electrónico, §18 borrado seguro UNE 15713, §19 RRSS. Responsables: Nuria Martínez (Dir. Corporativa), Simone Huber (SIG), Sergio Fernández (IT), Matteo Pugnaghi (Técnico). Ref: ISO 9001:2015, ISO 27001:2017, MSG 00 |

---

## 🔒 02 SEGURIDAD

> **Clasificación**: 🟠 Confidencial / 🔴 Restringido  
> **Responsable**: Responsable Seguridad / IT  
> **¿Qué es?**: Ciberseguridad, cumplimiento, protección de datos — **LA CATEGORÍA MÁS IMPORTANTE PARA AUDITORÍA**  
> **Normativas**: **ISO 27001 (principal)** + IEC 62443 S1-S3 + EU CRA Anexo I

### 02.1 Políticas de Seguridad de la Información
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 02.1-01 | Security overview (existente) | ✅ | 🏢 | S:A.5.1 | `compliance/SECURITY.md` |
| 02.1-02 | Resumen ciberseguridad | ✅ | 🏢 | S:A.5.1 | `compliance/resumen-ciberseguridad.md` |
| 02.1-03 | Roles y permisos del sistema | ✅ | 🏢 | S:A.5.2 · I:S1.4 | `development/ROLES_PERMISSIONS.md` |
| 02.1-04 | Quickstart roles | ✅ | 🏢 | S:A.5.2 | `development/ROLES_PERMISSIONS_QUICKSTART.md` |
| 02.1-05 | **DOC-01: Política de Ciberseguridad** | 🔴 | 🏢 | **S:5.2, S:A.5.1** · **I:S1.1** | **CREAR** ~8 págs. Política general firmada dirección |
| 02.1-06 | **DOC-03: Organigrama + RACI Ciberseguridad** | 🔴 | 🏢 | **S:5.3, S:A.5.2** · **I:S1.4, S1.5** | **CREAR** ~4 págs |
| 02.1-07 | **DOC-05: Política Protección Física y TI** | 🔴 | 🏢 | **S:A.7.1-7.4** · **I:S2.1, S2.2, S2.5** | **CREAR** ~4 págs |
| 02.1-08 | **DOC-06: Política Gestión de Cuentas TI** | 🔴 | 🏢 | **S:A.5.15-18, A.8.2-5** · **I:S2.3** | **CREAR** ~3 págs |
| 02.1-09 | **DOC-07: Política Seguridad OT (TwinCAT/PLC)** | 🔴 | 🏢 | **S:A.8.9** · **I:S3.1, S3.2, S3.3** | **CREAR** ~5 págs |
| 02.1-10 | Alcance del SGSI | 🟢 | 🏢 | **S:4.3** | Qué sistemas, datos, sitios cubre el SGSI |
| 02.1-11 | Declaración de Aplicabilidad (SoA) | 🟢 | 🏢 | **S:6.1.3d** | 93 controles Anexo A: aplica / no aplica / justificación |
| 02.1-12 | Objetivos de seguridad de la información | 🟢 | 🏢 | **S:6.2** | Objetivos medibles de seguridad |
| 02.1-13 | Política de clasificación de información | 🟢 | 🏢 | **S:A.5.12, A.5.13** | Niveles: público, interno, confidencial, restringido |
| 02.1-14 | Política de criptografía | 🟢 | 🏢 | **S:A.8.24** · C:I.1(c) | TLS, cifrado DB, hashing contraseñas, certificados |
| 02.1-15 | Política de seguridad de red | 🟢 | 🏢 | **S:A.8.20-22** · I:S3.1 | Segmentación, firewall, monitorización red |

### 02.2 CRA EU — Cumplimiento Europeo
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 02.2-01 | Roadmap cumplimiento CRA | ✅ | 🏢 | **C:I.1, C:I.2** | `compliance/ROADMAP_CUMPLIMIENTO_CRA.md` |
| 02.2-02 | Gestión usuarios CRA | ✅ | 🏢 | C:I.1(d) · S:A.5.15 | `compliance/GESTION_USUARIOS_EU_CRA.md` |
| 02.2-03 | Sistema de logs CRA | ✅ | 🏢 | **C:I.1(g)** · S:A.8.15 | `compliance/SISTEMA_LOGS_CRA.md` |
| 02.2-04 | **DOC-04: Plan de Gestión de Incidentes** | 🔴 | 🏢 | **I:S2.4** · **S:A.5.24-26** · C:I.1(f) | **CREAR** ~6 págs |

### 02.3 Integridad
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 02.3-01 | Software Integrity (firma, checksums) | ✅ | 🏢 | **I:S5.2** · C:I.1(h) · S:A.8.25 | `architecture/SOFTWARE_INTEGRITY.md` |
| 02.3-02 | Estado integridad actual | ✅ | 🏢 | I:S5.2 | `integrity-state.json` |
| 02.3-03 | Versión deploy producción | ✅ | 🖥️M | I:S5.2 · C:I.1(h) | `deploy-version.json` (generado en deploy) |

### 02.4 Auditorías y Evaluaciones
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 02.4-01 | **DOC-02: Estrategia Ciberseguridad + KPIs** | 🔴 | 🏢 | **I:S1.2, S1.3** · **S:9.1** | **CREAR** ~5 págs |
| 02.4-02 | **DOC-12: Procedimiento Evaluación Terceros** | 🔴 | 🏢 | **I:S6.3** · **S:A.5.19-21** · Q:8.4 | **CREAR** ~4 págs |

### 02.5 Gestión de Riesgos
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 02.5-01 | Metodología de evaluación de riesgos | 🟢 | 🏢 | **S:6.1.2** · Q:6.1 | Metodología (ISO 27005): activos, amenazas, vulnerabilidades |
| 02.5-02 | Registro de riesgos + Plan de tratamiento | 🟢 | 🏢 | **S:6.1.3, S:8.3** · Q:6.1 | Riesgos identificados + acciones de mitigación |
| 02.5-03 | Inventario de activos de información | 🟢 | 🏢 | **S:A.5.9, A.5.10, A.5.11** | HW, SW, datos, personas, servicios — propietario de cada activo |

### 02.6 Continuidad de Negocio
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 02.6-01 | Plan de Continuidad de Negocio (BCP) | 🟢 | 🏢 | **S:A.5.29, A.5.30** | Qué hacer si falla un sistema crítico. **Nota**: 01.8-07 PG 07 ya cubre emergencias físicas (incendios, accidentes, vertidos) y plan de contingencia básico con equipo de crisis. Falta enfoque específico en continuidad de sistemas TI/OT |
| 02.6-02 | Plan de Recuperación ante Desastres (DRP) | 🟢 | 🏢 | **S:A.5.30** · Q:6.1 | Backup, restore, tiempos de recuperación (RTO/RPO). Complementa PG 07 con enfoque digital |

### 02.7 Seguridad del Personal
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 02.7-01 | Procedimiento seguridad RRHH | 🟢 | 🏢 | **S:A.6.1-6.6** | Selección, NDAs, durante empleo, terminación |
| 02.7-02 | Plan de concienciación y formación en seguridad | 🟢 | 🏢 | **S:A.6.3** · Q:7.3 · I:S1.1 | Formación periódica en ciberseguridad para todo el personal |
| 02.7-03 | Acuerdos de confidencialidad (NDA tipo) | 🟢 | 🏢 | **S:A.6.6** · S:A.5.14 | Template NDA para empleados y terceros |

---

## 💻 03 SOFTWARE

> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Departamento Software  
> **¿Qué es?**: Documentación del código (Frontend + Backend) — siempre igual para todas las máquinas. **LA CATEGORÍA MÁS LLENA**  
> **Normativas**: **IEC 62443 S4-S5 (principal)** + EU CRA Anexo VII + ISO 9001:8.3

### 03.1 Arquitectura del Sistema
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 03.1-01 | Arquitectura y despliegue | ✅ | 🏢 | Q:8.3 · C:VII · S:A.8.25 | `architecture/ARQUITECTURA_DESPLIEGUE.md` |
| 03.1-02 | Arquitectura de logs | ✅ | 🏢 | C:I.1(g) · S:A.8.15 | `architecture/ARQUITECTURA_LOGS.md` |
| 03.1-03 | Gestión de datos (backup, restore) | ✅ | 🏢 | S:A.8.13 · C:I.1(f) | `architecture/DATA_MANAGEMENT.md` |
| 03.1-04 | Sistema de gestión documental | ✅ | 🏢 | Q:7.5 | `architecture/DOCUMENT_MANAGEMENT_SYSTEM.md` |
| 03.1-05 | Implementación modelos 3D | ✅ | 🏢 | Q:8.3 | `architecture/MODELOS_3D_IMPLEMENTATION.md` |
| 03.1-06 | Sistema multi-proyecto | ✅ | 🏢 | Q:8.3 · S:A.8.31 | `architecture/MULTI_PROJECT_SYSTEM.md` |
| 03.1-07 | Implementación System Config | ✅ | 🏢 | Q:8.3 · C:I.1(b) | `configuration/SYSTEM_CONFIG_IMPLEMENTATION.md` |

### 03.2 SDL — Desarrollo Seguro (Secure Development Lifecycle)
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 03.2-01 | Guía de desarrollo | ✅ | 🏢 | Q:8.3 · S:A.8.25 | `development/GUIA_DESARROLLO.md` |
| 03.2-02 | Ejemplo API Backend | ✅ | 🏢 | Q:8.3 | `development/BACKEND_API_EXAMPLE.md` |
| 03.2-03 | Integración Backend | ✅ | 🏢 | Q:8.3 | `development/INTEGRACION_BACKEND.md` |
| 03.2-04 | Integración Frontend bombas | ✅ | 🏢 | Q:8.3 | `development/INTEGRACION_FRONTEND_PUMPS.md` |
| 03.2-05 | Implementación pump elements | ✅ | 🏢 | Q:8.3 | `development/IMPLEMENTACION_PUMP_ELEMENTS.md` |
| 03.2-06 | **DOC-08: SDL — Proceso Desarrollo Seguro** | 🔴 | 🏢 | **I:S4.1, S4.2, S4.4** · **S:A.8.25-27** · C:I.1(a,e) | **CREAR** ~8 págs |

### 03.3 Guías de Codificación Segura
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 03.3-01 | **DOC-09: Secure Coding Guidelines** | 🔴 | 🏢 | **I:S4.3** · **S:A.8.28** · C:I.1(a) | **CREAR** ~6 págs |

### 03.4 SBOM y Terceros
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 03.4-01 | Índice de terceros | ✅ | 🏢 | I:S5.1 · S:A.5.19 · Q:8.4 | `compliance/terceros/INDICE_TERCEROS.md` |
| 03.4-02 | README Beckhoff | ✅ | 🏢 | I:S5.1 · S:A.5.19 | `compliance/terceros/beckhoff/README_BECKHOFF.md` |
| 03.4-03 | Config Beckhoff propia | ✅ | 🏢 | I:S5.1 | `compliance/terceros/beckhoff/Nuestra_Configuracion_Beckhoff.md` |
| 03.4-04 | **DOC-10: SBOM Formal** | 🔴 | 🏢 | **I:S5.1** · **C:VII** · S:A.5.19 | **CREAR** ~3 págs (semi-auto) |

### 03.5 Testing y Changelog
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 03.5-01 | Estado integración | ✅ | 🏢 | Q:8.6 · I:S4.4 | `changelog/ESTADO_INTEGRACION.md` |
| 03.5-02 | Resumen trabajo nocturno | ✅ | 🏢 | Q:8.3 | `changelog/RESUMEN_TRABAJO_NOCTURNO.md` |
| 03.5-03 | Plan de testing formal | 🟡 | 🏢 | **Q:8.6** · I:S4.4 · S:A.8.29 | Casos de test, criterios aceptación SW |
| 03.5-04 | Release Notes / Changelog formal | 🟢 | 🖥️M | **C:II** · I:S5.2 · Q:8.6 | Qué cambió en cada versión |

### 03.6 Documentación Técnica CRA
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 03.6-01 | Documentación técnica formal EU CRA | 🟢 | 🏢 | **C:VII** | Descripción general producto, diseño, desarrollo, evaluación riesgos, pruebas |

---

## 📖 04 MANUALES

> **Clasificación**: 🟢 Público / 🔵 Interno  
> **Responsable**: Departamento Software / Ingeniería  
> **¿Qué es?**: Documentación de usuario del producto software **Aquafrisch Supervisor**  
> **Normativas**: **EU CRA Anexo II (principal)** + ISO 9001:7.2-7.3

### 04.1 Manual de Usuario
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 04.1-01 | Manual recuperación usuario | ✅ | 🖥️M | **C:II** · Q:8.5 | `user-guides/MANUAL_USUARIO_RECUPERACION.md` |
| 04.1-02 | Manual usuario completo (CRA Anexo II) | 🟢 | 🖥️M | **C:II** (obligatorio) | Nombre, fabricante, contacto, instalación, uso, soporte, vulnerabilidades |

### 04.2 Manual de Instalación
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 04.2-01 | Instalación producción completa | ✅ | 🖥️M | C:II · Q:8.5 | `deployment/INSTALACION_PRODUCCION.md` |
| 04.2-02 | Cómo usar nueva versión | ✅ | 🖥️M | C:I.1(h) · C:II | `deployment/COMO_USAR_NUEVA_VERSION.md` |
| 04.2-03 | Configuración modo Kiosk | ✅ | 🖥️M | Q:8.5 | `deployment/README_KIOSK.md` |
| 04.2-04 | Deploy servidor empresa | ✅ | 🏢 | Q:8.5 | `deployment/SERVIDOR_EMPRESA.md` |

### 04.3 Formación
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 04.3-01 | Material formación operadores | 🟡 | 🖥️M | **Q:7.2, Q:7.3** · S:A.6.3 | Formación operadores de la máquina |
| 04.3-02 | Guía rápida de arranque | 🟡 | 🖥️M | C:II · Q:7.2 | Quick start guide |

### 04.4 FAQ / Troubleshooting
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 04.4-01 | Troubleshooting animación PLC | ✅ | 🖥️M | Q:8.7 | `development/TROUBLESHOOTING_ANIMACION_PLC.md` |
| 04.4-02 | FAQ general del producto | 🟡 | 🖥️M | C:II | FAQ operativo |

---

## 📐 05 PLANTILLAS

> **Clasificación**: 🔵 Interno  
> **Responsable**: Ingeniería / Dirección Técnica  
> **¿Qué es?**: Estándares y plantillas para estandarizar el trabajo  
> **Normativas**: ISO 9001:8.1, 8.4 (estandarización de procesos)

### 05.1 Checklist Nuevo Proyecto
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 05.1-01 | Checklist arranque proyecto nuevo | 🟡 | 🏢 | Q:8.1 | Lista para empezar proyecto |
| 05.1-02 | Checklist entrega final | 🟡 | 🏢 | Q:8.6 | Lista para entregar al cliente |

### 05.2 Formatos Estándar
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 05.2-01 | Template informe ingeniería | 🟡 | 🏢 | Q:7.5 | Formato estándar |
| 05.2-02 | Template acta reunión | 🟡 | 🏢 | Q:7.4 | Formato estándar |
| 05.2-03 | Template informe test | 🟡 | 🏢 | Q:8.6 · I:S4.4 | Formato estándar |

### 05.3 Componentes Homologados
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 05.3-01 | Vocabulario máquina (glosario) | ✅ | 🏢 | Q:7.4 | `internal/VOCABULARIO_MAQUINA.xlsx` |
| 05.3-02 | Lista componentes aprobados | 🟡 | 🏢 | **Q:8.4** · S:A.5.19 | Componentes homologados |
| 05.3-03 | Proveedores homologados | 🟡 | 🏢 | **Q:8.4** · **S:A.5.19** · I:S6.3 | Lista proveedores evaluados |

### 05.4 Criterios de Aceptación
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 05.4-01 | Criterios aceptación SW (SAT/FAT) | 🟡 | 🏢 | **Q:8.6** · I:S4.4 | Qué debe pasar para liberar SW |
| 05.4-02 | Criterios aceptación HW | 🟡 | 🏢 | **Q:8.6** | Qué debe pasar para liberar HW |

---

# 🔧 PER-MACHINE — Se repite para CADA instalación

> Cada modelo de máquina × cada cliente = un proyecto independiente.  
> El orden sigue el **flujo cronológico real**: Vender → Diseñar → Programar → Configurar → Mantener

---

## 🏗️ 06 PROYECTO — ① Vender

> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Project Manager / Comercial  
> **Normativas**: ISO 9001:8.2 (requisitos producto) + IEC 62443 S6, S8

### 06.1 Oferta Comercial
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 06.1-01 | Oferta técnica-económica | ⬜ | 🏢 | Q:8.2 | Per-machine |
| 06.1-02 | Presupuesto detallado | ⬜ | 🏢 | Q:8.2 | Per-machine |

### 06.2 Especificaciones Cliente
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 06.2-01 | Análisis estándares IT del cliente | ✅ | 🏢 | S:A.5.36 · I:S6.1 | `especificaciones_clientes/RhB_IT_Standards_v9.0.4_Analisis.md` |
| 06.2-02 | **DOC-11: Requisitos Ciberseguridad Producto** | 🔴 | 🏢 | **I:S6.1, S6.2** · S:A.5.19 · C:I.1 | **CREAR** ~5 págs |
| 06.2-03 | Especificaciones técnicas del cliente | ⬜ | 🏢 | **Q:8.2.2, Q:8.2.3** | Requisitos funcionales + revisión contrato |
| 06.2-04 | Revisión de requisitos del contrato | 🟢 | 🏢 | **Q:8.2.3** | Acta de revisión: ¿podemos cumplir todo? |

### 06.3 Contrato
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 06.3-01 | Contrato firmado | ⬜ | 🏢 | Q:8.2.3 | Per-machine |
| 06.3-02 | Condiciones de garantía | ⬜ | 🏢 | Q:8.2.1 · C:II | Per-machine |

### 06.4 Plan de Proyecto
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 06.4-01 | Cronograma proyecto (Gantt) | ⬜ | 🏢 | Q:8.1 · I:S8.2 | Per-machine |
| 06.4-02 | **DOC-14: Roles Ciberseguridad por Proyecto** | 🔴 | 🏢 | **I:S8.1, S8.2** · S:A.5.2 | **CREAR** ~3 págs (plantilla reutilizable) |

### 06.5 Actas y Comunicaciones
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 06.5-01 | Actas reunión con cliente | ⬜ | 🏢 | Q:7.4 | Per-machine |
| 06.5-02 | Correspondencia técnica relevante | ⬜ | 🏢 | Q:7.4 | Per-machine |

---

## ⚡ 07 INGENIERÍA — ② Diseñar

> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Ingeniería  
> **Normativas**: ISO 9001:8.3 (diseño y desarrollo)

### 07.1 Esquemas Eléctricos
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 07.1-01 | Esquemas de potencia | ⬜ | 🏢 | Q:8.3.5 |
| 07.1-02 | Esquemas de control/maniobra | ⬜ | 🏢 | Q:8.3.5 |
| 07.1-03 | Lista de cables | ⬜ | 🏢 | Q:8.3.5 |

### 07.2 P&ID (Piping & Instrumentation)
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 07.2-01 | Diagrama tuberías e instrumentación | ⬜ | 🏢 | Q:8.3.5 |
| 07.2-02 | Lista de instrumentos | ⬜ | 🏢 | Q:8.3.5 |

### 07.3 Layout / Implantación
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 07.3-01 | Layout planta 2D | ⬜ | 🏢 | Q:8.3.5 |
| 07.3-02 | Layout 3D (si aplica) | ⬜ | 🏢 | Q:8.3.5 |

### 07.4 Planos Mecánicos
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 07.4-01 | Planos de conjunto | ⬜ | 🏢 | Q:8.3.5 |
| 07.4-02 | Planos de detalle / despiece | ⬜ | 🏢 | Q:8.3.5 |

### 07.5 Esquemas Neumáticos/Hidráulicos
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 07.5-01 | Esquemas neumáticos (si aplica) | ⬜ | 🏢 | Q:8.3.5 |

### 07.6 BOM — Lista de Materiales
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 07.6-01 | BOM materiales completa | ⬜ | 🏢 | Q:8.3.5 · Q:8.4 |
| 07.6-02 | BOM componentes eléctricos | ⬜ | 🏢 | Q:8.3.5 · Q:8.4 |

### 07.7 Datasheets Componentes
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 07.7-01 | Fichas técnicas equipos instalados | ⬜ | 🏢 | Q:8.4 |

### 07.8 Planos As-Built
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 07.8-01 | Planos "como quedó" (cambios vs diseño) | ⬜ | 🏢 | Q:8.3.6 · Q:8.5.6 |

---

## 🔧 08 TWINCAT / PLC — ③ Programar

> **Clasificación**: 🔴 Restringido  
> **Responsable**: Programador PLC  
> **Normativas**: IEC 62443 S3 (seguridad OT) + ISO 9001:8.3

### 08.1 Proyecto TwinCAT
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 08.1-01 | Archivo proyecto .tsproj (backup completo) | ⬜ | 🏢 | Q:8.3.5 · I:S3.2 |
| 08.1-02 | Versión TwinCAT y runtime utilizados | ⬜ | 🏢 | I:S3.3 · I:S5.1 |

### 08.2 Configuración I/O
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 08.2-01 | Mapa de I/O (entradas/salidas) | ⬜ | 🏢 | Q:8.3.5 · I:S3.2 |
| 08.2-02 | Lista de señales con direcciones | ⬜ | 🏢 | Q:8.3.5 |

### 08.3 EtherCAT
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 08.3-01 | Topología red EtherCAT | ⬜ | 🏢 | I:S3.1 · S:A.8.20 |
| 08.3-02 | Configuración esclavos + firmware | ⬜ | 🏢 | I:S3.2, S3.3 |

### 08.4 Recetas PLC
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 08.4-01 | Definición de recetas | ⬜ | 🏢 | Q:8.5.1 |
| 08.4-02 | Parámetros de proceso | ⬜ | 🏢 | Q:8.5.1 |

### 08.5 Documentación PLC
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 08.5-01 | Descripción funcional del PLC | ⬜ | 🏢 | Q:8.3.5 · C:VII |
| 08.5-02 | Lista de Function Blocks | ⬜ | 🏢 | Q:8.3.5 |

---

## ⚙️ 09 CONFIG SW — ④ Configurar

> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Departamento Software  
> **Normativas**: ISO 9001:8.3 + EU CRA I.1(b) (config segura por defecto)

### 09.1 Excel Config
| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| 09.1-01 | Referencia estructura Excel 15 columnas | ✅ | 🖥️M | Q:8.3.5 | `configuration/ESTRUCTURA_EXCEL_15_COLUMNAS_REFERENCIA.md` |
| 09.1-02 | Mapeo columnas Excel | ✅ | 🖥️M | Q:8.3.5 | `configuration/MAPEO_COLUMNAS_EXCEL.md` |
| 09.1-03 | System Config sheet | ✅ | 🖥️M | Q:8.3.5 · C:I.1(b) | `configuration/SYSTEM_CONFIG_SHEET.md` |
| 09.1-04 | Configuración elementos 3D | ✅ | 🖥️M | Q:8.3.5 | `excel configuration/3D_Elements_Info_Setting.md` |
| 09.1-05 | ProjectConfig.xlsm de esta máquina | ⬜ | 🖥️P | Q:8.3.5 · S:A.8.9 | Per-machine |

### 09.2 Modelos 3D
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 09.2-01 | Archivos .glb de esta máquina | ⬜ | 🖥️P | Q:8.3.5 |
| 09.2-02 | Guía configuración modelos 3D | 🟡 | 🖥️M | Q:8.3.5 |

### 09.3 Base de Datos Proyecto
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 09.3-01 | project.db de esta máquina | ⬜ | 🖥️P | S:A.8.13 |
| 09.3-02 | Esquema/documentación de la DB | 🟡 | 🖥️M | Q:8.3.5 · S:A.8.13 |

---

## 🛠️ 10 OPERACIONES — ⑤ Mantener

> **Clasificación**: 🔵 Interno  
> **Responsable**: Servicio Técnico / Cliente  
> **Normativas**: ISO 9001:8.5 (producción y servicio) + 8.6 (liberación)

### 10.1 Mantenimiento Preventivo
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 10.1-01 | Plan mantenimiento preventivo | ⬜ | 🖥️P | Q:8.5.1 · S:A.7.13 |
| 10.1-02 | Checklist inspección periódica | ⬜ | 🖥️P | Q:8.5.1 |
| 10.1-03 | Calendario de mantenimiento | ⬜ | 🖥️P | Q:8.5.1 |

### 10.2 Mantenimiento Correctivo
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 10.2-01 | Registro de averías | ⬜ | 🖥️P | Q:8.7 · Q:10.2 |
| 10.2-02 | Informes de reparación | ⬜ | 🖥️P | Q:8.7 |
| 10.2-03 | Análisis causa raíz | ⬜ | 🖥️P | Q:10.2 |

### 10.3 Repuestos
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 10.3-01 | Lista repuestos recomendados | ⬜ | 🖥️P | Q:8.5.3 · C:II |
| 10.3-02 | Stock mínimo | ⬜ | 🖥️P | Q:8.5.3 |

### 10.4 Histórico Máquina
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 10.4-01 | Libro de máquina | ⬜ | 🖥️P | Q:8.5.2 · Q:7.5 |
| 10.4-02 | Registro de modificaciones | ⬜ | 🖥️P | Q:8.5.6 · S:A.8.32 |
| 10.4-03 | Histórico de alarmas | ⬜ | 🖥️P | C:I.1(g) · S:A.8.15 |

### 10.5 Puesta en Marcha
| ID | Documento | Estado | Ubic. | Norma |
|----|-----------|--------|-------|-------|
| 10.5-01 | Protocolo puesta en marcha | ⬜ | 🖥️P | **Q:8.6** · I:S4.4 |
| 10.5-02 | Checklist commissioning | ⬜ | 🖥️P | **Q:8.6** |
| 10.5-03 | Acta de recepción firmada | ⬜ | 🖥️P | **Q:8.6** · Q:8.2.3 |

---

## ⛔ INTERNO — Fuera del Árbol (No publicar)

> **Clasificación**: 🔴 RESTRINGIDO MÁXIMO  
> **Nota**: Credenciales y datos sensibles. NO van en ningún DMS público.

| ID | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|----|-----------|--------|-------|-------|-----------------|
| INT-01 | Credenciales iniciales clientes | ⚠️ | 🏢 | S:A.5.17 | `internal/CLIENTE_CREDENCIALES_INICIALES.md` |
| INT-02 | Credenciales internas Aquafrisch | ⚠️ | 🏢 | S:A.5.17 | `internal/INTERNAL_AQUAFRISCH_CREDENTIALS.md` |
| INT-03 | Documentación interna varios | ⚠️ | 🏢 | — | `internal/DOCUMENTACION_INTERNA_AQUAFRISCH.md` |

---

# 📊 MATRICES CRUZADAS MULTINORMATIVAS

## Matriz ISO 9001:2015 — Cobertura completa

| Cláusula | Requisito | Documento(s) en el árbol | Estado |
|----------|-----------|--------------------------|--------|
| 4.1 | Contexto de la organización | 01.1-04 Contexto + partes interesadas, **01.8-10 PG 10** ✅ (§5.1 contexto interno/externo) | ✅ parcial+🟢 |
| 4.2 | Partes interesadas | 01.1-04 Contexto + partes interesadas | 🟢 |
| 4.3 | Alcance del SGC | 01.1-03 Alcance del SGC | 🟢 |
| 4.4 | SGC y procesos | 01.1-01 Manual SGC, **01.8-01 MSG 00** ✅ | ✅+🟢 |
| 5.1 | Liderazgo y compromiso | 01.1-02 Política Calidad, 01.6-03 Revisión dirección | 🟢 |
| 5.2 | Política de calidad | 01.1-02 Política de Calidad | 🟢 |
| 5.3 | Roles y responsabilidades | 01.1-01 Manual SGC | 🟢 |
| 6.1 | Riesgos y oportunidades | **01.8-10 PG 10** ✅ (procedimiento R&O, ISO 31000, matriz 5×5), 01.7-01 Análisis riesgos calidad, 02.5-01/02 riesgos ciber, **01.8-07 PG 07** ✅ (emergencias) | ✅+🟢 |
| 6.2 | Objetivos de calidad | 01.3-01 Objetivos calidad anuales | 🟢 |
| 7.1 | Recursos | 01.1-01 Manual SGC, **01.8-04 PG 04** ✅ | ✅+🟢 |
| 7.2 | Competencia | 04.3-01 Formación operadores, 02.7-02 Concienciación | 🟡+🟢 |
| 7.3 | Toma de conciencia | 02.7-02 Plan concienciación, 04.3-01 Material formación | 🟡+🟢 |
| 7.4 | Comunicación | **01.8-11 PG 11** ✅ (interna ascendente/descendente/horizontal + externa + matriz comunicación FR 1113), 05.2-02 Template acta, 06.5-01/02 Actas proyecto | ✅+🟡+⬜ |
| 7.5 | Información documentada | 01.2-01 PGD ✅, 01.2-02 Estructura ✅, **01.8-02 PG 01** ✅ | ✅ |
| 8.1 | Planificación y control operacional | **01.8-05 PG 05** ✅, 05.1-01 Checklist arranque, 06.4-01 Cronograma | ✅+🟡+⬜ |
| 8.2 | Requisitos productos/servicios | **01.8-08 PG 08** ✅, 00.3-01/02/03 Datasheets, 06.2-03 Especificaciones, 06.2-04 Revisión requisitos | ✅+🟡+⬜ |
| 8.3 | Diseño y desarrollo | **01.8-05 PG 05** ✅, Categorías 03, 07, 08, 09 (múltiples docs) | ✅+⬜ |
| 8.4 | Proveedores externos | **01.8-06 PG 06** ✅, 05.3-03 Proveedores, 02.4-02 Eval Terceros, 03.4-01 Índice | ✅+🟡+🔴 |
| 8.5 | Producción y servicio | **01.8-05 PG 05** ✅, **01.8-09 PG 09** ✅ (postventa: reparaciones, mantenimientos, garantías), Categoría 10 (operaciones) | ✅+⬜ |
| 8.6 | Liberación productos | 05.1-02 Checklist entrega, 10.5-01/02/03 Puesta en marcha | 🟡+⬜ |
| 8.7 | Salidas no conformes | **01.8-03 PG 02** ✅, 01.4-01/02 NC + acciones correctivas | ✅+🟢 |
| 9.1 | Seguimiento y medición | 01.3-02 KPIs, 01.3-03 Satisfacción cliente, **01.8-08 PG 08** ✅ + **01.8-09 PG 09** ✅ (encuestas satisfacción ventas y postventa) | ✅ parcial+🟢 |
| 9.2 | Auditoría interna | 01.6-01/02 Programa + procedimiento auditoría | 🟢 |
| 9.3 | Revisión por la dirección | 01.6-03 Actas revisión por dirección | 🟢 |
| 10.1 | Mejora | 01.5-01 Plan mejora continua | 🟢 |
| 10.2 | No conformidad y acción correctiva | **01.8-03 PG 02** ✅, 01.4-01/02 Registro NC + procedimiento | ✅+🟢 |
| 10.3 | Mejora continua | 01.5-01 Plan mejora continua | 🟢 |

> **Cobertura ISO 9001**: 100% de cláusulas tienen documento asignado. Los PGs existentes (01.8) cubren las cláusulas operativas 8.1-8.7 + ventas (8.2) + postventa (8.5.5). Estado: ~50% ✅ existente, ~30% 🟢 por crear, ~20% ⬜ per-machine.

---

## Matriz ISO 27001:2022 — Cláusulas obligatorias

| Cláusula | Requisito | Documento(s) en el árbol | Estado |
|----------|-----------|--------------------------|--------|
| 4.1-4.2 | Contexto y partes interesadas | 01.1-04 (compartido con Q:4.1-4.2) | 🟢 |
| 4.3 | Alcance del SGSI | 02.1-10 Alcance SGSI | 🟢 |
| 5.1 | Liderazgo | **01.8-13 PG 13** ✅ (§2-3 política corporativa seguridad información), 02.1-05 DOC-01 | ✅+🔴 |
| 5.2 | Política de seguridad | **01.8-13 PG 13** ✅ (PG 13 ES la política corporativa de seguridad de la información, 42 cap.), 02.1-05 DOC-01 | ✅+🔴 |
| 5.3 | Roles de seguridad | **01.8-13 PG 13** ✅ (§5 responsabilidades: Dir. Corporativa, SIG, IT, Técnicos), 02.1-06 DOC-03 | ✅+🔴 |
| 6.1.2 | Evaluación de riesgos | **01.8-13 PG 13** ✅ (§34 Plan Director Seguridad, análisis riesgos ciber), 02.5-01 | ✅+🟢 |
| 6.1.3 | Tratamiento de riesgos | 02.5-02 Registro riesgos + Plan tratamiento | 🟢 |
| 6.1.3d | Declaración de Aplicabilidad | 02.1-11 SoA | 🟢 |
| 6.2 | Objetivos de seguridad | 02.1-12 Objetivos seguridad | 🟢 |
| 7.1-7.4 | Soporte (recursos, competencia, conciencia, com.) | **01.8-13 PG 13** ✅ (§22 formación+concienciación 2 manuales, §5 recursos), 02.7-02, 04.3-01, 01.1-01 | ✅+🟢+🟡 |
| 7.5 | Información documentada | 01.2-01 PGD | ✅ |
| 8.1-8.3 | Operación (planificar, eval riesgos, tratar riesgos) | **01.8-13 PG 13** ✅ (§34 PDS + 42 capítulos operativos seguridad), 02.5-01/02, 02.1-05 | ✅+🟢+🔴 |
| 9.1 | Monitorización y medición | 02.4-01 DOC-02 Estrategia + KPIs | 🔴 |
| 9.2 | Auditoría interna | 01.6-01/02 Programa auditoría (compartido con Q:9.2) | 🟢 |
| 9.3 | Revisión por dirección | 01.6-03 Actas revisión (compartido con Q:9.3) | 🟢 |
| 10.1 | No conformidad | 01.4-01/02 (compartido con Q:10.2) | 🟢 |
| 10.2 | Mejora continua | 01.5-01 (compartido con Q:10.3) | 🟢 |

---

## Matriz ISO 27001:2022 — Controles Anexo A (principales)

| Control | Descripción | Documento(s) en el árbol | Estado |
|---------|-------------|--------------------------|--------|
| A.5.1 | Políticas de seguridad | **01.8-13 PG 13** ✅ (procedimiento corporativo completo 42 cap.), 02.1-05 DOC-01 | ✅+🔴 |
| A.5.2 | Roles de seguridad | **01.8-13 PG 13** ✅ (§5 responsabilidades), 02.1-06 DOC-03, 02.1-03 ✅ | ✅+🔴 |
| A.5.9 | Inventario de activos | **01.8-13 PG 13** ✅ (§6 inventarios: SW, HW, dispositivos, actualizaciones, servidores; FR 1305), 02.5-03 | ✅+🟢 |
| A.5.10 | Uso aceptable de activos | **01.8-13 PG 13** ✅ (§16 aplicaciones permitidas, §11-14 almacenamiento), 02.5-03 | ✅+🟢 |
| A.5.12-13 | Clasificación y etiquetado | **01.8-13 PG 13** ✅ (§20 clasificación: confidencial/interna/pública + criterios), 02.1-13 | ✅+🟢 |
| A.5.15-18 | Control de acceso | 02.1-08 DOC-06, 02.2-02 GESTION_USUARIOS ✅ | 🔴+✅ |
| A.5.19-21 | Proveedores | 02.4-02 DOC-12, 03.4-01 INDICE_TERCEROS ✅ | 🔴+✅ |
| A.5.24-26 | Gestión de incidentes | **01.8-13 PG 13** ✅ (§38 protocolo completo: gabinete crisis, detección→aislamiento→recuperación→AEPD 72h→denuncia), 02.2-04 DOC-04 | ✅+🔴 |
| A.5.29-30 | Continuidad de negocio | **01.8-07 PG 07** ✅, **01.8-13 PG 13** ✅ (§23 PCN + §26 copias seguridad), 02.6-01 BCP, 02.6-02 DRP | ✅+🟢 |
| A.5.36 | Cumplimiento | 06.2-01 Gap analysis cliente ✅, 02.2-01 Roadmap CRA ✅ | ✅ |
| A.6.1-6.6 | Personas (antes empleo, durante, terminación) | **01.8-13 PG 13** ✅ (§33 RRHH: cláusulas, confidencialidad, formación, sanciones, baja+revocación), 02.7-01 | ✅+🟢 |
| A.6.3 | Concienciación | **01.8-13 PG 13** ✅ (§22 formación: PE1301 taller + PE1302 oficina, checklists FR1301/1302 anuales), 02.7-02 | ✅+🟢 |
| A.6.6 | Confidencialidad | **01.8-13 PG 13** ✅ (§7 protección datos, §33 acuerdos confidencialidad ingreso+baja), 02.7-03 | ✅+🟢 |
| A.7.1-7.4 | Seguridad física | **01.8-13 PG 13** ✅ (§7 protección centro: alarma, cámaras, bolardos; §9 zonas acceso; servidor bajo llave), 02.1-07 DOC-05 | ✅+🔴 |
| A.8.1-5 | Dispositivos y acceso | **01.8-13 PG 13** ✅ (§14 equipos trabajo, §24-25 contraseñas+acceso, §30-31 móviles/BYOD), 02.1-08 DOC-06 | ✅+🔴 |
| A.8.8 | Gestión vulnerabilidades técnicas | **01.8-13 PG 13** ✅ (§10 actualizaciones sw, §15 antimalware AVG, §17 auditoría sistemas), 00.5-02 DOC-13 | ✅+🔴 |
| A.8.9 | Gestión de configuración | 09.1-05 ProjectConfig, 02.1-09 DOC-07 | 🔴+⬜ |
| A.8.13 | Backup | 03.1-03 DATA_MANAGEMENT ✅ | ✅ |
| A.8.15-16 | Logging y monitoring | 02.2-03 SISTEMA_LOGS ✅, 03.1-02 ARQUITECTURA_LOGS ✅ | ✅ |
| A.8.20-22 | Seguridad de red | **01.8-13 PG 13** ✅ (§41 wifi y redes externas, VPN), 02.1-15 Política seguridad red | ✅+🟢 |
| A.8.24 | Criptografía | **01.8-13 PG 13** ✅ (§40 técnicas criptográficas, cifrado, VPN, certificados), 02.1-14 | ✅+🟢 |
| A.8.25-27 | Desarrollo seguro | 03.2-06 DOC-08 SDL, 03.2-01 GUIA_DESARROLLO ✅ | 🔴+✅ |
| A.8.28 | Codificación segura | 03.3-01 DOC-09 | 🔴 |
| A.8.29 | Testing | 03.5-03 Plan testing | 🟡 |

---

## Matriz IEC 62443 — Checklist Proveedor S1-S8

| Punto | Descripción | Documento(s) en el árbol | Estado |
|-------|-------------|--------------------------|--------|
| S1.1 | Política de ciberseguridad | 02.1-05 DOC-01 | 🔴 |
| S1.2 | Estrategia de ciberseguridad | 02.4-01 DOC-02 | 🔴 |
| S1.3 | KPIs de seguridad | 02.4-01 DOC-02 | 🔴 |
| S1.4 | Organización ciberseguridad | 02.1-06 DOC-03 | 🔴 |
| S1.5 | Responsable de ciberseguridad | 02.1-06 DOC-03 | 🔴 |
| S2.1 | Seguridad física | 02.1-07 DOC-05 | 🔴 |
| S2.2 | Seguridad de red | 02.1-07 DOC-05, 02.1-15 Política red | 🔴+🟢 |
| S2.3 | Gestión de cuentas | 02.1-08 DOC-06 | 🔴 |
| S2.4 | Gestión de incidentes | 02.2-04 DOC-04 | 🔴 |
| S2.5 | Acceso a instalaciones | 02.1-07 DOC-05 | 🔴 |
| S3.1 | Separación IT/OT | 02.1-09 DOC-07, 02.1-15 Política red | 🔴+🟢 |
| S3.2 | Gestión activos OT | 02.1-09 DOC-07 | 🔴 |
| S3.3 | Actualización OT | 02.1-09 DOC-07 | 🔴 |
| S4.1 | SDL (proceso desarrollo seguro) | 03.2-06 DOC-08 | 🔴 |
| S4.2 | Requisitos seguridad en diseño | 03.2-06 DOC-08 | 🔴 |
| S4.3 | Codificación segura | 03.3-01 DOC-09 | 🔴 |
| S4.4 | Verificación / testing | 03.2-06 DOC-08, 03.5-03 Plan testing | 🔴+🟡 |
| S5.1 | SBOM | 03.4-04 DOC-10 | 🔴 |
| S5.2 | Integridad software | 02.3-01 SOFTWARE_INTEGRITY ✅, 02.3-02/03 | **✅** |
| S6.1 | Requisitos ciber al proveedor | 06.2-02 DOC-11 | 🔴 |
| S6.2 | Evaluación proveedor | 06.2-02 DOC-11 | 🔴 |
| S6.3 | Monitoreo proveedor | 02.4-02 DOC-12 | 🔴 |
| S7.1 | Proceso gestión vulnerabilidades | 00.5-02 DOC-13 | 🔴 |
| S7.2 | Comunicación vulnerabilidades | 00.5-02 DOC-13, 00.5-01 VULNERABILITY_REPORT ✅ | 🔴+✅ |
| S8.1 | Roles ciber por proyecto | 06.4-02 DOC-14 | 🔴 |
| S8.2 | Integración seguridad en proyecto | 06.4-02 DOC-14 | 🔴 |

> **Cobertura IEC 62443**: S5.2 ya cubierto ✅. Los 14 DOCs (🔴) cubren los otros 30 puntos. Deadline: **abril 2026**.

---

## Matriz EU CRA — Cyber Resilience Act

| Requisito | Descripción | Documento(s) en el árbol | Estado |
|-----------|-------------|--------------------------|--------|
| **Anexo I.1(a)** | Sin vulnerabilidades conocidas | 03.2-06 DOC-08 SDL, 03.3-01 DOC-09, 00.5-01 VULNERABILITY_REPORT ✅ | 🔴+✅ |
| **Anexo I.1(b)** | Config segura por defecto | 03.1-07 SYSTEM_CONFIG ✅, 09.1-03 System Config Sheet ✅ | ✅ |
| **Anexo I.1(c)** | Proteger datos en tránsito | 02.1-14 Política criptografía (TLS, HTTPS) | 🟢 |
| **Anexo I.1(d)** | Proteger contra acceso no autorizado | 02.1-08 DOC-06 Gestión Cuentas, 02.2-02 GESTION_USUARIOS ✅ | 🔴+✅ |
| **Anexo I.1(e)** | Minimizar superficie de ataque | 03.2-06 DOC-08 SDL, 03.1-01 ARQUITECTURA ✅ | 🔴+✅ |
| **Anexo I.1(f)** | Minimizar impacto incidentes | 02.2-04 DOC-04 Plan Incidentes, 03.1-03 DATA_MANAGEMENT ✅ | 🔴+✅ |
| **Anexo I.1(g)** | Registrar actividad/eventos | 02.2-03 SISTEMA_LOGS ✅, 03.1-02 ARQUITECTURA_LOGS ✅ | **✅** |
| **Anexo I.1(h)** | Mecanismo actualización seguro | 02.3-01 SOFTWARE_INTEGRITY ✅, 04.2-02 COMO_USAR_NUEVA_VERSION ✅ | **✅** |
| **Anexo I.2** | Gestión de vulnerabilidades | 00.5-02 DOC-13 Proceso Gestión Vulnerabilidades | 🔴 |
| **Anexo II** | Información al usuario | 04.1-02 Manual usuario completo, 00.3-01 Datasheet SW, 03.5-04 Release Notes | 🟢+🟡 |
| **Anexo V** | Declaración de conformidad | 00.4-01 Declaración conformidad EU CRA | 🟢 |
| **Anexo VII** | Documentación técnica | 03.6-01 Documentación técnica CRA | 🟢 |

> **Cobertura EU CRA**: I.1(b), I.1(g), I.1(h) ya cubiertos ✅. Resto necesita los DOCs de auditoría (🔴) + documentos normativos (🟢).

---

# 🔴 RESUMEN: DOCUMENTOS POR CREAR — PRIORIZADO

## Prioridad 1: AUDITORÍA IEC 62443 — Abril 2026 (14 documentos)

| DOC | Documento | Posición | Ubic. | Norma principal | Págs |
|-----|-----------|----------|-------|-----------------|------|
| DOC-01 | Política de Ciberseguridad | 02.1-05 | 🏢 | I:S1.1 · S:5.2, A.5.1 | ~8 |
| DOC-02 | Estrategia Ciberseguridad + KPIs | 02.4-01 | 🏢 | I:S1.2, S1.3 · S:9.1 | ~5 |
| DOC-03 | Organigrama + RACI Ciberseguridad | 02.1-06 | 🏢 | I:S1.4, S1.5 · S:5.3, A.5.2 | ~4 |
| DOC-04 | Plan de Gestión de Incidentes | 02.2-04 | 🏢 | I:S2.4 · S:A.5.24-26 · C:I.1(f) | ~6 |
| DOC-05 | Política Protección Física y TI | 02.1-07 | 🏢 | I:S2.1, S2.2, S2.5 · S:A.7.1-4 | ~4 |
| DOC-06 | Política Gestión Cuentas TI | 02.1-08 | 🏢 | I:S2.3 · S:A.5.15-18, A.8.2-5 | ~3 |
| DOC-07 | Política Seguridad OT (TwinCAT/PLC) | 02.1-09 | 🏢 | I:S3.1-3.3 · S:A.8.9 | ~5 |
| DOC-08 | SDL — Proceso Desarrollo Seguro | 03.2-06 | 🏢 | I:S4.1, S4.2, S4.4 · S:A.8.25-27 · C:I.1(a,e) | ~8 |
| DOC-09 | Secure Coding Guidelines | 03.3-01 | 🏢 | I:S4.3 · S:A.8.28 · C:I.1(a) | ~6 |
| DOC-10 | SBOM Formal | 03.4-04 | 🏢 | I:S5.1 · C:VII · S:A.5.19 | ~3 |
| DOC-11 | Requisitos Ciberseguridad Producto | 06.2-02 | 🏢 | I:S6.1, S6.2 · S:A.5.19 | ~5 |
| DOC-12 | Procedimiento Evaluación Terceros | 02.4-02 | 🏢 | I:S6.3 · S:A.5.19-21 · Q:8.4 | ~4 |
| DOC-13 | Proceso Gestión Vulnerabilidades | 00.5-02 | 🖥️M | I:S7.1, S7.2 · C:I.2 · S:A.8.8 | ~5 |
| DOC-14 | Roles Ciberseguridad por Proyecto | 06.4-02 | 🏢 | I:S8.1, S8.2 · S:A.5.2 | ~3 |
| | **Subtotal** | | | | **~69 págs** |

## Prioridad 2: NORMATIVAS ISO (documentos 🟢)

| ID | Documento | Norma principal | Págs |
|----|-----------|-----------------|------|
| 01.1-01 | Manual del SGC | Q:4.4, Q:5.3, Q:7.1 | ~15 |
| 01.1-02 | Política de Calidad | Q:5.2 | ~2 |
| 01.1-03 | Alcance del SGC | Q:4.3 | ~2 |
| 01.1-04 | Contexto + partes interesadas | Q:4.1, Q:4.2 | ~4 |
| 01.3-01 | Objetivos calidad | Q:6.2 | ~2 |
| 01.3-02 | KPIs procesos | Q:9.1.1 | ~3 |
| 01.3-03 | Satisfacción cliente | Q:9.1.2 | ~2 |
| 01.4-01 | Registro NC | Q:10.2 · S:10.1 | ~2 |
| 01.4-02 | Procedimiento acc. correctivas | Q:10.2 · S:10.1 | ~3 |
| 01.5-01 | Plan mejora continua | Q:10.3 · S:10.2 | ~2 |
| 01.6-01/02 | Programa + proc. auditoría | Q:9.2 · S:9.2 | ~5 |
| 01.6-03 | Actas revisión dirección | Q:9.3 · S:9.3 | ~2 |
| 01.7-01 | Análisis riesgos calidad | Q:6.1 | ~3 |
| 02.1-10 | Alcance SGSI | S:4.3 | ~2 |
| 02.1-11 | Declaración Aplicabilidad (SoA) | S:6.1.3d | ~8 |
| 02.1-12 | Objetivos seguridad | S:6.2 | ~2 |
| 02.1-13 | Política clasificación | S:A.5.12-13 | ~3 |
| 02.1-14 | Política criptografía | S:A.8.24 · C:I.1(c) | ~3 |
| 02.1-15 | Política seguridad red | S:A.8.20-22 | ~3 |
| 02.5-01 | Metodología riesgos | S:6.1.2 | ~4 |
| 02.5-02 | Registro riesgos + tratamiento | S:6.1.3, S:8.3 | ~5 |
| 02.5-03 | Inventario activos | S:A.5.9-11 | ~5 |
| 02.6-01/02 | BCP + DRP (complementa PG 07 existente) | S:A.5.29-30 | ~6 |
| 02.7-01/02/03 | RRHH + concienciación + NDA | S:A.6.1-6 | ~6 |
| 03.5-04 | Release Notes formal | C:II · I:S5.2 | ~2 |
| 03.6-01 | Doc técnica CRA | C:VII | ~8 |
| 04.1-02 | Manual usuario CRA | C:II | ~10 |
| 06.2-04 | Revisión requisitos contrato | Q:8.2.3 | ~1 |
| | **Subtotal** | | **~100 págs** |

---

# 📅 ROADMAP

```
FEB-MAR 2026 ──── ABR 2026 ──── MAY-JUL 2026 ──── SEP-DIC 2026 ──── 2027
     │                │                │                  │              │
     │ 🔵 FASE 1      │ 🔴 AUDITORÍA  │ 🟠 FASE 2a       │ 🟠 FASE 2b   │ 🟢 FASE 3
     │                │                │                  │              │
     │ • 14 docs IEC  │ • Auditoría    │ • DMS Empresa v1 │ • Docs 9001  │ • Cert 9001
     │ • Completar    │   62443        │ • Docs ISO 27001 │ • Docs 27001 │ • Cert 27001
     │   Supervisor   │ • Software OK  │ • SoA + Riesgos  │ • BCP/DRP    │ • EU CRA
     │   DMS          │ • PASAR ✅     │ • Auditoría int. │ • RRHH sec.  │   cumplimiento
     │                │                │                  │              │
     │  ~69 págs      │               │  ~50 págs        │  ~50 págs    │
```

---

## Aprobación

| Rol | Nombre | Firma | Fecha |
|-----|--------|-------|-------|
| Director General | | | |
| Director Técnico | | | |
| Responsable IT / Software | | | |
| Responsable Calidad | | | |
| Responsable Ingeniería | | | |

---

> **Este documento se examina con Dirección ANTES de:**  
> 1. Empezar a escribir los 14 documentos de auditoría IEC 62443  
> 2. Planificar los documentos normativos ISO 9001/27001/CRA  
> 3. Modificar las categorías del Aquafrisch Supervisor DMS  
> 4. Planificar el desarrollo del DMS Empresa  
