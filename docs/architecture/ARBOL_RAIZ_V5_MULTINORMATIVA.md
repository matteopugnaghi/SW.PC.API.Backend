# 🌳 ÁRBOL RAÍZ DOCUMENTAL v5.0 — MULTINORMATIVA COMPLETA

> **Código**: ARB-2026-001  
> **Versión**: 5.0  
> **Fecha**: 2026-02-16  
> **Estado**: Para revisión con Dirección  
> **Autor**: Departamento de Software  
> **Referencia**: PGD-2026-001 (Plan de Gestión Documental)  
> **Cambio v5.0**: Cobertura completa de las 4 normativas (ISO 9001, ISO 27001, IEC 62443, EU CRA) con cláusulas específicas por documento. Añadidos ~22 documentos nuevos para cubrir gaps normativos.  
> **Sustituye**: ARBOL_RAIZ_V4_COMPLETO.md (v4.1)

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

---

## Visión General del Árbol

```
🏭 AQUAFRISCH — GESTIÓN DOCUMENTAL (Maquinaria talleres ferroviarios)
│                                                              Ubic.  Normativa principal
│━━━ 📦 MASTER — Se escribe UNA VEZ, sirve para TODAS las máquinas ━━━
│
├── 🌐 00 PÚBLICO              5 subcat   10 docs               🏢+🖥️M  C:V, C:II, I:S7
├── 📋 01 CALIDAD              7 subcat   16 docs               🏢      Q:4-10 (TODO)
├── 🔒 02 SEGURIDAD            8 subcat   33 docs               🏢      S:4-10, I:S1-S3, C:I
├── 💻 03 SOFTWARE             6 subcat   22 docs               🏢      I:S4-S5, C:VII, Q:8.3
├── 📖 04 MANUALES             4 subcat   10 docs               🖥️M     C:II, Q:7.2-7.3
├── 📐 05 PLANTILLAS           4 subcat   10 docs               🏢      Q:8.1, Q:8.4
│
│━━━ 🔧 PER-MACHINE — Se repite para CADA instalación ━━━
│
├── 🏗️ 06 PROYECTO  ① Vender    5 subcat   11 docs              🏢      Q:8.2, I:S6, I:S8
├── ⚡ 07 INGENIERÍA ② Diseñar   8 subcat   14 docs              🏢      Q:8.3
├── 🔧 08 TWINCAT   ③ Programar 5 subcat   10 docs              🏢      I:S3, Q:8.3
├── ⚙️ 09 CONFIG SW  ④ Config    3 subcat    9 docs              🖥️M+🖥️P Q:8.3, C:I.1(b)
├── 🛠️ 10 OPERACIONES ⑤ Mantener 5 subcat   15 docs             🖥️P     Q:8.5-8.6
│
├── ⛔ INTERNO                                3 docs              🏢      —
│
│━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
│ TOTALES: 11 categorías │ 60 subcategorías │ ~163 posiciones
│          43 documentos EXISTENTES ✅
│          14 documentos AUDITORÍA 🔴 (abril 2026)
│          ~27 documentos FUTURO 🟡
│          ~22 documentos NORMATIVA 🟢 (completar ISO 9001/27001/CRA)
│          ~44 posiciones PER-MACHINE ⬜
│
│ POR UBICACIÓN:
│   🏢  DMS Empresa .............. ~115 docs
│   🖥️M Supervisor Master ........  ~18 docs
│   🖥️P Supervisor Project ........  ~23 docs
│
│ POR NORMATIVA:
│   Q ISO 9001 .................. ~60 docs contribuyen
│   S ISO 27001 ................. ~45 docs contribuyen
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

## 🗂️ Vista Explorador — Árbol Completo (v5.0)

```
🏭 AQUAFRISCH — ÁRBOL DOCUMENTAL v5.0 MULTINORMATIVA
│
│━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
│  📦 MASTER — Se escribe UNA VEZ, sirve para TODAS las máquinas
│━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
│
├── 🌐 00 PÚBLICO                               🏢+🖥️M  C:V, C:II, I:S7
│   ├── 00.1 Certificaciones
│   │   ├── 1.  🟡 Certificado ISO 9001                     Q:toda
│   │   ├── 2.  🟡 Certificado ISO 27001                    S:toda
│   │   ├── 3.  🟡 Declaración IEC 62443                    I:toda
│   │   └── 4.  🟡 Certificado EU CRA                       C:toda
│   ├── 00.2 Catálogo Producto
│   │   ├── 5.  ✅ Presentación Aquafrisch Supervisor        —
│   │   ├── 6.  ✅ Email comercial tipo                      —
│   │   └── 7.  ✅ Screenshots funcionales                   —
│   ├── 00.3 Ficha Técnica
│   │   └── 8.  🟡 Ficha técnica producto (datasheet)        C:II · Q:8.2
│   ├── 00.4 Declaración Conformidad
│   │   └── 9.  🟢 Declaración conformidad EU CRA            C:V
│   └── 00.5 Política Vulnerabilidades
│       ├── 10. ✅ Informe vulnerabilidades conocidas         C:I.2 · C:II
│       └── 11. 🔴 DOC-13: Gestión Vulnerabilidades          I:S7.1-2 · C:I.2 · S:A.8.8
│
├── 📋 01 CALIDAD                                🏢  Q:4-10
│   ├── 01.1 SGC — Sistema Gestión Calidad
│   │   ├── 12. 🟢 Manual del SGC                            Q:4.4, Q:5.3, Q:7.1
│   │   ├── 13. 🟢 Política de Calidad                       Q:5.2
│   │   ├── 14. 🟢 Alcance del SGC                           Q:4.3
│   │   └── 15. 🟢 Contexto org. + partes interesadas        Q:4.1, Q:4.2
│   ├── 01.2 Gestión Documental
│   │   ├── 16. ✅ PGD (Plan Gestión Documental)              Q:7.5 · S:7.5
│   │   └── 17. ✅ Estructura multinormativa (borrador)       Q:7.5
│   ├── 01.3 Objetivos y Medición
│   │   ├── 18. 🟢 Objetivos calidad anuales 2026            Q:6.2
│   │   ├── 19. 🟢 Indicadores y medición (KPIs)             Q:9.1.1
│   │   └── 20. 🟢 Satisfacción del cliente                  Q:9.1.2
│   ├── 01.4 No Conformidades
│   │   ├── 21. 🟢 Registro de no conformidades              Q:10.2 · S:10.1
│   │   └── 22. 🟢 Procedimiento acciones correctivas        Q:10.2 · S:10.1
│   ├── 01.5 Mejora Continua
│   │   └── 23. 🟢 Plan mejora continua                      Q:10.3 · S:10.2
│   ├── 01.6 Auditoría Interna y Revisión Dirección ← NUEVO
│   │   ├── 24. 🟢 Programa de auditoría interna             Q:9.2 · S:9.2
│   │   ├── 25. 🟢 Procedimiento de auditoría interna        Q:9.2 · S:9.2
│   │   └── 26. 🟢 Actas revisión por dirección              Q:9.3 · S:9.3
│   └── 01.7 Riesgos y Oportunidades ← NUEVO
│       └── 27. 🟢 Análisis riesgos y oportunidades          Q:6.1
│
├── 🔒 02 SEGURIDAD                              🏢  S:4-10, I:S1-S3, C:I
│   ├── 02.1 Políticas de Seguridad de la Información
│   │   ├── 28. ✅ Security overview                          S:A.5.1
│   │   ├── 29. ✅ Resumen ciberseguridad                     S:A.5.1
│   │   ├── 30. ✅ Roles y permisos del sistema               S:A.5.2 · I:S1.4
│   │   ├── 31. ✅ Quickstart roles                           S:A.5.2
│   │   ├── 32. 🔴 DOC-01: Política Ciberseguridad            S:5.2, A.5.1 · I:S1.1
│   │   ├── 33. 🔴 DOC-03: Organigrama + RACI Ciber           S:5.3, A.5.2 · I:S1.4-5
│   │   ├── 34. 🔴 DOC-05: Protección Física y TI             S:A.7.1-4 · I:S2.1-2, S2.5
│   │   ├── 35. 🔴 DOC-06: Gestión Cuentas TI                 S:A.5.15-18, A.8.2-5 · I:S2.3
│   │   ├── 36. 🔴 DOC-07: Seguridad OT (TwinCAT/PLC)        S:A.8.9 · I:S3.1-3
│   │   ├── 37. 🟢 Alcance del SGSI                           S:4.3
│   │   ├── 38. 🟢 Declaración Aplicabilidad (SoA)            S:6.1.3d
│   │   ├── 39. 🟢 Objetivos seguridad información            S:6.2
│   │   ├── 40. 🟢 Política clasificación información         S:A.5.12-13
│   │   ├── 41. 🟢 Política de criptografía                   S:A.8.24 · C:I.1(c)
│   │   └── 42. 🟢 Política seguridad de red                  S:A.8.20-22 · I:S3.1
│   ├── 02.2 CRA EU — Cumplimiento Europeo
│   │   ├── 43. ✅ Roadmap cumplimiento CRA                   C:I.1, C:I.2
│   │   ├── 44. ✅ Gestión usuarios CRA                       C:I.1(d) · S:A.5.15
│   │   ├── 45. ✅ Sistema de logs CRA                        C:I.1(g) · S:A.8.15
│   │   └── 46. 🔴 DOC-04: Plan Gestión Incidentes            I:S2.4 · S:A.5.24-26 · C:I.1(f)
│   ├── 02.3 Integridad
│   │   ├── 47. ✅ Software Integrity (firma, checksums)      I:S5.2 · C:I.1(h) · S:A.8.25
│   │   ├── 48. ✅ Estado integridad actual                    I:S5.2
│   │   └── 49. ✅ Versión deploy producción                   I:S5.2 · C:I.1(h)
│   ├── 02.4 Auditorías y Evaluaciones
│   │   ├── 50. ✅ Gap analysis RhB IT Standards              S:A.5.36
│   │   ├── 51. 🔴 DOC-02: Estrategia Ciberseg. + KPIs       I:S1.2-3 · S:9.1
│   │   └── 52. 🔴 DOC-12: Evaluación Terceros                I:S6.3 · S:A.5.19-21 · Q:8.4
│   ├── 02.5 Gestión de Riesgos
│   │   ├── 53. 🟢 Metodología evaluación de riesgos          S:6.1.2 · Q:6.1
│   │   ├── 54. 🟢 Registro riesgos + Plan tratamiento        S:6.1.3, S:8.3 · Q:6.1
│   │   └── 55. 🟢 Inventario activos de información          S:A.5.9-11
│   ├── 02.6 Continuidad de Negocio ← NUEVO
│   │   ├── 56. 🟢 Plan Continuidad Negocio (BCP)             S:A.5.29-30
│   │   └── 57. 🟢 Plan Recuperación Desastres (DRP)          S:A.5.30 · Q:6.1
│   └── 02.7 Seguridad del Personal ← NUEVO
│       ├── 58. 🟢 Procedimiento seguridad RRHH               S:A.6.1-6
│       ├── 59. 🟢 Plan concienciación seguridad              S:A.6.3 · Q:7.3 · I:S1.1
│       └── 60. 🟢 Acuerdos confidencialidad (NDA)            S:A.6.6 · S:A.5.14
│
├── 💻 03 SOFTWARE                                🏢  I:S4-S5, C:VII, Q:8.3
│   ├── 03.1 Arquitectura del Sistema
│   │   ├── 61. ✅ Arquitectura y despliegue                   Q:8.3 · C:VII · S:A.8.25
│   │   ├── 62. ✅ Arquitectura de logs                        C:I.1(g) · S:A.8.15
│   │   ├── 63. ✅ Gestión de datos (backup, restore)          S:A.8.13 · C:I.1(f)
│   │   ├── 64. ✅ Sistema gestión documental                  Q:7.5
│   │   ├── 65. ✅ Implementación modelos 3D                   Q:8.3
│   │   ├── 66. ✅ Sistema multi-proyecto                      Q:8.3 · S:A.8.31
│   │   └── 67. ✅ System Config implementation                Q:8.3 · C:I.1(b)
│   ├── 03.2 SDL — Desarrollo Seguro
│   │   ├── 68. ✅ Guía de desarrollo                          Q:8.3 · S:A.8.25
│   │   ├── 69. ✅ Ejemplo API Backend                         Q:8.3
│   │   ├── 70. ✅ Integración Backend                         Q:8.3
│   │   ├── 71. ✅ Integración Frontend bombas                 Q:8.3
│   │   ├── 72. ✅ Implementación pump elements                Q:8.3
│   │   └── 73. 🔴 DOC-08: SDL — Desarrollo Seguro            I:S4.1-2, S4.4 · S:A.8.25-27 · C:I.1(a,e)
│   ├── 03.3 Guías Codificación Segura
│   │   └── 74. 🔴 DOC-09: Secure Coding Guidelines           I:S4.3 · S:A.8.28 · C:I.1(a)
│   ├── 03.4 SBOM y Terceros
│   │   ├── 75. ✅ Índice de terceros                          I:S5.1 · S:A.5.19 · Q:8.4
│   │   ├── 76. ✅ README Beckhoff                             I:S5.1 · S:A.5.19
│   │   ├── 77. ✅ Config Beckhoff propia                      I:S5.1
│   │   └── 78. 🔴 DOC-10: SBOM Formal                        I:S5.1 · C:VII · S:A.5.19
│   ├── 03.5 Testing y Changelog
│   │   ├── 79. ✅ Estado integración                          Q:8.6 · I:S4.4
│   │   ├── 80. ✅ Resumen trabajo nocturno                    Q:8.3
│   │   ├── 81. 🟡 Plan de testing formal                     Q:8.6 · I:S4.4 · S:A.8.29
│   │   └── 82. 🟢 Release Notes / Changelog formal           C:II · I:S5.2 · Q:8.6
│   └── 03.6 Documentación Técnica CRA ← NUEVO
│       └── 83. 🟢 Documentación técnica formal EU CRA        C:VII
│
├── 📖 04 MANUALES                                🖥️M  C:II, Q:7.2-7.3
│   ├── 04.1 Manual de Usuario
│   │   ├── 84. ✅ Manual recuperación usuario                 C:II · Q:8.5
│   │   └── 85. 🟢 Manual usuario completo (CRA Anexo II)     C:II (obligatorio)
│   ├── 04.2 Manual de Instalación
│   │   ├── 86. ✅ Instalación producción completa             C:II · Q:8.5
│   │   ├── 87. ✅ Cómo usar nueva versión                     C:I.1(h) · C:II
│   │   ├── 88. ✅ Configuración modo Kiosk                    Q:8.5
│   │   └── 89. ✅ Deploy servidor empresa                     Q:8.5
│   ├── 04.3 Formación
│   │   ├── 90. 🟡 Material formación operadores              Q:7.2-3 · S:A.6.3
│   │   └── 91. 🟡 Guía rápida de arranque                    C:II · Q:7.2
│   └── 04.4 FAQ / Troubleshooting
│       ├── 92. ✅ Troubleshooting animación PLC               Q:8.7
│       └── 93. 🟡 FAQ general del producto                    C:II
│
├── 📐 05 PLANTILLAS                              🏢  Q:8.1, Q:8.4
│   ├── 05.1 Checklist Nuevo Proyecto
│   │   ├── 94. 🟡 Checklist arranque proyecto nuevo           Q:8.1
│   │   └── 95. 🟡 Checklist entrega final                    Q:8.6
│   ├── 05.2 Formatos Estándar
│   │   ├── 96. 🟡 Template informe ingeniería                Q:7.5
│   │   ├── 97. 🟡 Template acta reunión                      Q:7.4
│   │   └── 98. 🟡 Template informe test                      Q:8.6 · I:S4.4
│   ├── 05.3 Componentes Homologados
│   │   ├── 99. ✅ Vocabulario máquina (glosario)              Q:7.4
│   │   ├──100. 🟡 Lista componentes aprobados                Q:8.4 · S:A.5.19
│   │   └──101. 🟡 Proveedores homologados                    Q:8.4 · S:A.5.19 · I:S6.3
│   └── 05.4 Criterios de Aceptación
│       ├──102. 🟡 Criterios aceptación SW (SAT/FAT)          Q:8.6 · I:S4.4
│       └──103. 🟡 Criterios aceptación HW                    Q:8.6
│
│━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
│  🔧 PER-MACHINE — Se repite para CADA instalación
│━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
│
├── 🏗️ 06 PROYECTO — ① Vender                    🏢  Q:8.2, I:S6, I:S8
│   ├── 06.1 Oferta Comercial
│   │   ├──104. ⬜ Oferta técnica-económica                    Q:8.2
│   │   └──105. ⬜ Presupuesto detallado                       Q:8.2
│   ├── 06.2 Especificaciones Cliente
│   │   ├──106. ✅ Análisis estándares IT del cliente           S:A.5.36 · I:S6.1
│   │   ├──107. 🔴 DOC-11: Requisitos Ciber Producto           I:S6.1-2 · S:A.5.19 · C:I.1
│   │   ├──108. ⬜ Especificaciones técnicas del cliente        Q:8.2.2-3
│   │   └──109. 🟢 Revisión requisitos del contrato            Q:8.2.3
│   ├── 06.3 Contrato
│   │   ├──110. ⬜ Contrato firmado                             Q:8.2.3
│   │   └──111. ⬜ Condiciones de garantía                      Q:8.2.1 · C:II
│   ├── 06.4 Plan de Proyecto
│   │   ├──112. ⬜ Cronograma proyecto (Gantt)                  Q:8.1 · I:S8.2
│   │   └──113. 🔴 DOC-14: Roles Ciber por Proyecto            I:S8.1-2 · S:A.5.2
│   └── 06.5 Actas y Comunicaciones
│       ├──114. ⬜ Actas reunión con cliente                    Q:7.4
│       └──115. ⬜ Correspondencia técnica relevante            Q:7.4
│
├── ⚡ 07 INGENIERÍA — ② Diseñar                  🏢  Q:8.3
│   ├── 07.1 Esquemas Eléctricos
│   │   ├──116. ⬜ Esquemas de potencia                         Q:8.3.5
│   │   ├──117. ⬜ Esquemas de control/maniobra                 Q:8.3.5
│   │   └──118. ⬜ Lista de cables                              Q:8.3.5
│   ├── 07.2 P&ID (Piping & Instrumentation)
│   │   ├──119. ⬜ Diagrama tuberías e instrumentación          Q:8.3.5
│   │   └──120. ⬜ Lista de instrumentos                        Q:8.3.5
│   ├── 07.3 Layout / Implantación
│   │   ├──121. ⬜ Layout planta 2D                             Q:8.3.5
│   │   └──122. ⬜ Layout 3D (si aplica)                        Q:8.3.5
│   ├── 07.4 Planos Mecánicos
│   │   ├──123. ⬜ Planos de conjunto                           Q:8.3.5
│   │   └──124. ⬜ Planos de detalle / despiece                 Q:8.3.5
│   ├── 07.5 Esquemas Neumáticos/Hidráulicos
│   │   └──125. ⬜ Esquemas neumáticos (si aplica)              Q:8.3.5
│   ├── 07.6 BOM — Lista de Materiales
│   │   ├──126. ⬜ BOM materiales completa                      Q:8.3.5 · Q:8.4
│   │   └──127. ⬜ BOM componentes eléctricos                   Q:8.3.5 · Q:8.4
│   ├── 07.7 Datasheets Componentes
│   │   └──128. ⬜ Fichas técnicas equipos instalados           Q:8.4
│   └── 07.8 Planos As-Built
│       └──129. ⬜ Planos "como quedó" (cambios vs diseño)      Q:8.3.6 · Q:8.5.6
│
├── 🔧 08 TWINCAT / PLC — ③ Programar            🏢  I:S3, Q:8.3
│   ├── 08.1 Proyecto TwinCAT
│   │   ├──130. ⬜ Archivo proyecto .tsproj (backup)            Q:8.3.5 · I:S3.2
│   │   └──131. ⬜ Versión TwinCAT y runtime                    I:S3.3 · I:S5.1
│   ├── 08.2 Configuración I/O
│   │   ├──132. ⬜ Mapa de I/O (entradas/salidas)               Q:8.3.5 · I:S3.2
│   │   └──133. ⬜ Lista de señales con direcciones             Q:8.3.5
│   ├── 08.3 EtherCAT
│   │   ├──134. ⬜ Topología red EtherCAT                       I:S3.1 · S:A.8.20
│   │   └──135. ⬜ Config esclavos + firmware                   I:S3.2-3
│   ├── 08.4 Recetas PLC
│   │   ├──136. ⬜ Definición de recetas                        Q:8.5.1
│   │   └──137. ⬜ Parámetros de proceso                        Q:8.5.1
│   └── 08.5 Documentación PLC
│       ├──138. ⬜ Descripción funcional del PLC                Q:8.3.5 · C:VII
│       └──139. ⬜ Lista de Function Blocks                     Q:8.3.5
│
├── ⚙️ 09 CONFIG SW — ④ Configurar                🖥️M+🖥️P  Q:8.3, C:I.1(b)
│   ├── 09.1 Excel Config
│   │   ├──140. ✅ Referencia estructura Excel 15 cols          Q:8.3.5
│   │   ├──141. ✅ Mapeo columnas Excel                         Q:8.3.5
│   │   ├──142. ✅ System Config sheet                          Q:8.3.5 · C:I.1(b)
│   │   ├──143. ✅ Configuración elementos 3D                   Q:8.3.5
│   │   └──144. ⬜ ProjectConfig.xlsm de esta máquina           Q:8.3.5 · S:A.8.9
│   ├── 09.2 Modelos 3D
│   │   ├──145. ⬜ Archivos .glb de esta máquina                Q:8.3.5
│   │   └──146. 🟡 Guía configuración modelos 3D               Q:8.3.5
│   └── 09.3 Base de Datos Proyecto
│       ├──147. ⬜ project.db de esta máquina                   S:A.8.13
│       └──148. 🟡 Esquema/documentación de la DB              Q:8.3.5 · S:A.8.13
│
├── 🛠️ 10 OPERACIONES — ⑤ Mantener               🖥️P  Q:8.5-8.6
│   ├── 10.1 Mantenimiento Preventivo
│   │   ├──149. ⬜ Plan mantenimiento preventivo                Q:8.5.1 · S:A.7.13
│   │   ├──150. ⬜ Checklist inspección periódica               Q:8.5.1
│   │   └──151. ⬜ Calendario de mantenimiento                  Q:8.5.1
│   ├── 10.2 Mantenimiento Correctivo
│   │   ├──152. ⬜ Registro de averías                          Q:8.7 · Q:10.2
│   │   ├──153. ⬜ Informes de reparación                       Q:8.7
│   │   └──154. ⬜ Análisis causa raíz                          Q:10.2
│   ├── 10.3 Repuestos
│   │   ├──155. ⬜ Lista repuestos recomendados                 Q:8.5.3 · C:II
│   │   └──156. ⬜ Stock mínimo                                 Q:8.5.3
│   ├── 10.4 Histórico Máquina
│   │   ├──157. ⬜ Libro de máquina                             Q:8.5.2 · Q:7.5
│   │   ├──158. ⬜ Registro de modificaciones                   Q:8.5.6 · S:A.8.32
│   │   └──159. ⬜ Histórico de alarmas                         C:I.1(g) · S:A.8.15
│   └── 10.5 Puesta en Marcha
│       ├──160. ⬜ Protocolo puesta en marcha                   Q:8.6 · I:S4.4
│       ├──161. ⬜ Checklist commissioning                      Q:8.6
│       └──162. ⬜ Acta de recepción firmada                    Q:8.6 · Q:8.2.3
│
└── ⛔ INTERNO — Fuera del Árbol (RESTRINGIDO)     🏢
    ├──163. ⚠️ Credenciales iniciales clientes                 S:A.5.17
    ├──164. ⚠️ Credenciales internas Aquafrisch                S:A.5.17
    └──165. ⚠️ Documentación interna varios                    —

RESUMEN:
  ✅ Existentes ........... 43    🖥️M Supervisor Master .... ~18
  🔴 Auditoría IEC 62443 .. 14    🖥️P Supervisor Project ... ~23
  🟢 Normativa ISO/CRA .... 22    🏢  DMS Empresa ......... ~115
  🟡 Futuro ............... 27
  ⬜ Per-machine ........... 44    Q ISO 9001 contribuyen .. ~60
  ⚠️ Restringido ........... 3    S ISO 27001 contribuyen . ~45
  ─────────────────────────────    I IEC 62443 contribuyen . ~25
  TOTAL .................. 165    C EU CRA contribuyen .... ~20
```

---

# DETALLE COMPLETO POR CATEGORÍA — CON CLÁUSULAS NORMATIVAS

> Cada documento muestra exactamente **qué cláusula** de cada normativa cubre.  
> Formato: `Q:5.2` = ISO 9001 cl.5.2 | `S:A.5.1` = ISO 27001 Anexo A ctrl 5.1 | `I:S1.1` = IEC 62443 punto S1.1 | `C:I.1(a)` = CRA Anexo I.1(a)

---

## 🌐 00 PÚBLICO

> **Clasificación**: 🟢 Público  
> **Responsable**: Dirección / Comercial  
> **¿Qué es?**: Lo que puede ver cualquiera — clientes, auditores, web  
> **Normativas**: EU CRA Anexo II y V (principal), ISO 9001 (certificaciones)

### 00.1 Certificaciones
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 1 | Certificado ISO 9001 | 🟡 | 🏢 | Q:toda | Cuando se certifique |
| 2 | Certificado ISO 27001 | 🟡 | 🏢 | S:toda | Cuando se certifique |
| 3 | Declaración IEC 62443 | 🟡 | 🏢 | I:toda | Cuando se certifique |
| 4 | Certificado EU CRA | 🟡 | 🏢 | C:toda | Cuando entre en vigor |

### 00.2 Catálogo Producto
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 5 | Presentación Aquafrisch Supervisor | ✅ | 🏢 | — | `presentacion/Aquafrisch_Supervisor_Core_2026.pptx` |
| 6 | Email comercial tipo | ✅ | 🏢 | — | `presentacion/email_comercial.html` |
| 7 | Screenshots funcionales (10 capturas) | ✅ | 🏢 | — | `presentacion/01_login.png` → `10_hardware_monitor.png` |

### 00.3 Ficha Técnica
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 8 | Ficha técnica producto (datasheet) | 🟡 | 🖥️M | C:II · Q:8.2 | Nombre, fabricante, propósito, contacto |

### 00.4 Declaración Conformidad
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 9 | Declaración conformidad EU CRA | 🟢 | 🖥️M | **C:V** | Declaración formal según modelo Anexo V |

### 00.5 Política Vulnerabilidades
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 10 | Informe vulnerabilidades conocidas | ✅ | 🖥️M | C:I.2 · C:II | `user-guides/VULNERABILITY_REPORT.md` |
| 11 | **DOC-13: Proceso Gestión Vulnerabilidades** | 🔴 | 🖥️M | **I:S7.1, S7.2** · C:I.2 · S:A.8.8 | **CREAR** ~5 págs |

---

## 📋 01 CALIDAD

> **Clasificación**: 🔵 Interno  
> **Responsable**: Responsable Calidad / Dirección  
> **¿Qué es?**: El sistema de gestión de calidad — la columna vertebral de ISO 9001  
> **Normativas**: **ISO 9001 (principal)** — cláusulas 4 a 10 completas

### 01.1 SGC — Sistema Gestión Calidad
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 12 | Manual del SGC | 🟢 | 🏢 | **Q:4.4, Q:5.3, Q:7.1** | Mapa de procesos, roles, recursos |
| 13 | Política de Calidad (firmada dirección) | 🟢 | 🏢 | **Q:5.2** | Firmada por Director General |
| 14 | Alcance del SGC | 🟢 | 🏢 | **Q:4.3** | Qué procesos/productos/sitios cubre |
| 15 | Contexto de la organización + partes interesadas | 🟢 | 🏢 | **Q:4.1, Q:4.2** | Análisis DAFO, stakeholders |

### 01.2 Gestión Documental
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 16 | Plan de Gestión Documental (PGD) | ✅ | 🏢 | **Q:7.5** · S:7.5 | `architecture/PGD_PLAN_GESTION_DOCUMENTAL.md` |
| 17 | Estructura multinormativa (borrador) | ✅ | 🏢 | Q:7.5 | `architecture/DMS_ESTRUCTURA_MULTINORMATIVA.md` |

### 01.3 Objetivos y Medición
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 18 | Objetivos calidad anuales 2026 | 🟢 | 🏢 | **Q:6.2** | Objetivos SMART medibles |
| 19 | Indicadores y medición de procesos (KPIs) | 🟢 | 🏢 | **Q:9.1.1** | KPIs de cada proceso |
| 20 | Satisfacción del cliente | 🟢 | 🏢 | **Q:9.1.2** | Encuestas, reclamaciones, feedback |

### 01.4 No Conformidades
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 21 | Registro de no conformidades | 🟢 | 🏢 | **Q:10.2** · S:10.1 | Registro NC internas y externas |
| 22 | Procedimiento acciones correctivas | 🟢 | 🏢 | **Q:10.2** · S:10.1 | Análisis causa raíz + acciones |

### 01.5 Mejora Continua
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 23 | Plan mejora continua | 🟢 | 🏢 | **Q:10.3** · S:10.2 | Oportunidades de mejora |

### 01.6 Auditoría Interna y Revisión por Dirección ← NUEVO
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 24 | Programa de auditoría interna | 🟢 | 🏢 | **Q:9.2** · **S:9.2** | Calendario, alcance, auditores, criterios |
| 25 | Procedimiento de auditoría interna | 🟢 | 🏢 | **Q:9.2** · **S:9.2** | Cómo se ejecuta la auditoría |
| 26 | Actas de revisión por dirección | 🟢 | 🏢 | **Q:9.3** · **S:9.3** | Entradas, salidas, decisiones, acciones |

### 01.7 Riesgos y Oportunidades ← NUEVO
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 27 | Análisis de riesgos y oportunidades (calidad) | 🟢 | 🏢 | **Q:6.1** | Riesgos de negocio (complementa 02.5 ciber) |

---

## 🔒 02 SEGURIDAD

> **Clasificación**: 🟠 Confidencial / 🔴 Restringido  
> **Responsable**: Responsable Seguridad / IT  
> **¿Qué es?**: Ciberseguridad, cumplimiento, protección de datos — **LA CATEGORÍA MÁS IMPORTANTE PARA AUDITORÍA**  
> **Normativas**: **ISO 27001 (principal)** + IEC 62443 S1-S3 + EU CRA Anexo I

### 02.1 Políticas de Seguridad de la Información
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 28 | Security overview (existente) | ✅ | 🏢 | S:A.5.1 | `compliance/SECURITY.md` |
| 29 | Resumen ciberseguridad | ✅ | 🏢 | S:A.5.1 | `compliance/resumen-ciberseguridad.md` |
| 30 | Roles y permisos del sistema | ✅ | 🏢 | S:A.5.2 · I:S1.4 | `development/ROLES_PERMISSIONS.md` |
| 31 | Quickstart roles | ✅ | 🏢 | S:A.5.2 | `development/ROLES_PERMISSIONS_QUICKSTART.md` |
| 32 | **DOC-01: Política de Ciberseguridad** | 🔴 | 🏢 | **S:5.2, S:A.5.1** · **I:S1.1** | **CREAR** ~8 págs. Política general firmada dirección |
| 33 | **DOC-03: Organigrama + RACI Ciberseguridad** | 🔴 | 🏢 | **S:5.3, S:A.5.2** · **I:S1.4, S1.5** | **CREAR** ~4 págs |
| 34 | **DOC-05: Política Protección Física y TI** | 🔴 | 🏢 | **S:A.7.1-7.4** · **I:S2.1, S2.2, S2.5** | **CREAR** ~4 págs |
| 35 | **DOC-06: Política Gestión de Cuentas TI** | 🔴 | 🏢 | **S:A.5.15-18, A.8.2-5** · **I:S2.3** | **CREAR** ~3 págs |
| 36 | **DOC-07: Política Seguridad OT (TwinCAT/PLC)** | 🔴 | 🏢 | **S:A.8.9** · **I:S3.1, S3.2, S3.3** | **CREAR** ~5 págs |
| 37 | Alcance del SGSI | 🟢 | 🏢 | **S:4.3** | Qué sistemas, datos, sitios cubre el SGSI |
| 38 | Declaración de Aplicabilidad (SoA) | 🟢 | 🏢 | **S:6.1.3d** | 93 controles Anexo A: aplica / no aplica / justificación |
| 39 | Objetivos de seguridad de la información | 🟢 | 🏢 | **S:6.2** | Objetivos medibles de seguridad |
| 40 | Política de clasificación de información | 🟢 | 🏢 | **S:A.5.12, A.5.13** | Niveles: público, interno, confidencial, restringido |
| 41 | Política de criptografía | 🟢 | 🏢 | **S:A.8.24** · C:I.1(c) | TLS, cifrado DB, hashing contraseñas, certificados |
| 42 | Política de seguridad de red | 🟢 | 🏢 | **S:A.8.20-22** · I:S3.1 | Segmentación, firewall, monitorización red |

### 02.2 CRA EU — Cumplimiento Europeo
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 43 | Roadmap cumplimiento CRA | ✅ | 🏢 | **C:I.1, C:I.2** | `compliance/ROADMAP_CUMPLIMIENTO_CRA.md` |
| 44 | Gestión usuarios CRA | ✅ | 🏢 | C:I.1(d) · S:A.5.15 | `compliance/GESTION_USUARIOS_EU_CRA.md` |
| 45 | Sistema de logs CRA | ✅ | 🏢 | **C:I.1(g)** · S:A.8.15 | `compliance/SISTEMA_LOGS_CRA.md` |
| 46 | **DOC-04: Plan de Gestión de Incidentes** | 🔴 | 🏢 | **I:S2.4** · **S:A.5.24-26** · C:I.1(f) | **CREAR** ~6 págs |

### 02.3 Integridad
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 47 | Software Integrity (firma, checksums) | ✅ | 🏢 | **I:S5.2** · C:I.1(h) · S:A.8.25 | `architecture/SOFTWARE_INTEGRITY.md` |
| 48 | Estado integridad actual | ✅ | 🏢 | I:S5.2 | `integrity-state.json` |
| 49 | Versión deploy producción | ✅ | 🖥️M | I:S5.2 · C:I.1(h) | `deploy-version.json` (generado en deploy) |

### 02.4 Auditorías y Evaluaciones
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 50 | Gap analysis RhB IT Standards | ✅ | 🏢 | S:A.5.36 | `compliance/rhb-it-standards-gap-analysis.md` |
| 51 | **DOC-02: Estrategia Ciberseguridad + KPIs** | 🔴 | 🏢 | **I:S1.2, S1.3** · **S:9.1** | **CREAR** ~5 págs |
| 52 | **DOC-12: Procedimiento Evaluación Terceros** | 🔴 | 🏢 | **I:S6.3** · **S:A.5.19-21** · Q:8.4 | **CREAR** ~4 págs |

### 02.5 Gestión de Riesgos
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 53 | Metodología de evaluación de riesgos | 🟢 | 🏢 | **S:6.1.2** · Q:6.1 | Metodología (ISO 27005): activos, amenazas, vulnerabilidades |
| 54 | Registro de riesgos + Plan de tratamiento | 🟢 | 🏢 | **S:6.1.3, S:8.3** · Q:6.1 | Riesgos identificados + acciones de mitigación |
| 55 | Inventario de activos de información | 🟢 | 🏢 | **S:A.5.9, A.5.10, A.5.11** | HW, SW, datos, personas, servicios — propietario de cada activo |

### 02.6 Continuidad de Negocio ← NUEVO
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 56 | Plan de Continuidad de Negocio (BCP) | 🟢 | 🏢 | **S:A.5.29, A.5.30** | Qué hacer si falla un sistema crítico |
| 57 | Plan de Recuperación ante Desastres (DRP) | 🟢 | 🏢 | **S:A.5.30** · Q:6.1 | Backup, restore, tiempos de recuperación (RTO/RPO) |

### 02.7 Seguridad del Personal ← NUEVO
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 58 | Procedimiento seguridad RRHH | 🟢 | 🏢 | **S:A.6.1-6.6** | Selección, NDAs, durante empleo, terminación |
| 59 | Plan de concienciación y formación en seguridad | 🟢 | 🏢 | **S:A.6.3** · Q:7.3 · I:S1.1 | Formación periódica en ciberseguridad para todo el personal |
| 60 | Acuerdos de confidencialidad (NDA tipo) | 🟢 | 🏢 | **S:A.6.6** · S:A.5.14 | Template NDA para empleados y terceros |

---

## 💻 03 SOFTWARE

> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Departamento Software  
> **¿Qué es?**: Documentación del código (Frontend + Backend) — siempre igual para todas las máquinas. **LA CATEGORÍA MÁS LLENA**  
> **Normativas**: **IEC 62443 S4-S5 (principal)** + EU CRA Anexo VII + ISO 9001:8.3

### 03.1 Arquitectura del Sistema
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 61 | Arquitectura y despliegue | ✅ | 🏢 | Q:8.3 · C:VII · S:A.8.25 | `architecture/ARQUITECTURA_DESPLIEGUE.md` |
| 62 | Arquitectura de logs | ✅ | 🏢 | C:I.1(g) · S:A.8.15 | `architecture/ARQUITECTURA_LOGS.md` |
| 63 | Gestión de datos (backup, restore) | ✅ | 🏢 | S:A.8.13 · C:I.1(f) | `architecture/DATA_MANAGEMENT.md` |
| 64 | Sistema de gestión documental | ✅ | 🏢 | Q:7.5 | `architecture/DOCUMENT_MANAGEMENT_SYSTEM.md` |
| 65 | Implementación modelos 3D | ✅ | 🏢 | Q:8.3 | `architecture/MODELOS_3D_IMPLEMENTATION.md` |
| 66 | Sistema multi-proyecto | ✅ | 🏢 | Q:8.3 · S:A.8.31 | `architecture/MULTI_PROJECT_SYSTEM.md` |
| 67 | Implementación System Config | ✅ | 🏢 | Q:8.3 · C:I.1(b) | `configuration/SYSTEM_CONFIG_IMPLEMENTATION.md` |

### 03.2 SDL — Desarrollo Seguro (Secure Development Lifecycle)
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 68 | Guía de desarrollo | ✅ | 🏢 | Q:8.3 · S:A.8.25 | `development/GUIA_DESARROLLO.md` |
| 69 | Ejemplo API Backend | ✅ | 🏢 | Q:8.3 | `development/BACKEND_API_EXAMPLE.md` |
| 70 | Integración Backend | ✅ | 🏢 | Q:8.3 | `development/INTEGRACION_BACKEND.md` |
| 71 | Integración Frontend bombas | ✅ | 🏢 | Q:8.3 | `development/INTEGRACION_FRONTEND_PUMPS.md` |
| 72 | Implementación pump elements | ✅ | 🏢 | Q:8.3 | `development/IMPLEMENTACION_PUMP_ELEMENTS.md` |
| 73 | **DOC-08: SDL — Proceso Desarrollo Seguro** | 🔴 | 🏢 | **I:S4.1, S4.2, S4.4** · **S:A.8.25-27** · C:I.1(a,e) | **CREAR** ~8 págs |

### 03.3 Guías de Codificación Segura
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 74 | **DOC-09: Secure Coding Guidelines** | 🔴 | 🏢 | **I:S4.3** · **S:A.8.28** · C:I.1(a) | **CREAR** ~6 págs |

### 03.4 SBOM y Terceros
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 75 | Índice de terceros | ✅ | 🏢 | I:S5.1 · S:A.5.19 · Q:8.4 | `compliance/terceros/INDICE_TERCEROS.md` |
| 76 | README Beckhoff | ✅ | 🏢 | I:S5.1 · S:A.5.19 | `compliance/terceros/beckhoff/README_BECKHOFF.md` |
| 77 | Config Beckhoff propia | ✅ | 🏢 | I:S5.1 | `compliance/terceros/beckhoff/Nuestra_Configuracion_Beckhoff.md` |
| 78 | **DOC-10: SBOM Formal** | 🔴 | 🏢 | **I:S5.1** · **C:VII** · S:A.5.19 | **CREAR** ~3 págs (semi-auto) |

### 03.5 Testing y Changelog
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 79 | Estado integración | ✅ | 🏢 | Q:8.6 · I:S4.4 | `changelog/ESTADO_INTEGRACION.md` |
| 80 | Resumen trabajo nocturno | ✅ | 🏢 | Q:8.3 | `changelog/RESUMEN_TRABAJO_NOCTURNO.md` |
| 81 | Plan de testing formal | 🟡 | 🏢 | **Q:8.6** · I:S4.4 · S:A.8.29 | Casos de test, criterios aceptación SW |
| 82 | Release Notes / Changelog formal | 🟢 | 🖥️M | **C:II** · I:S5.2 · Q:8.6 | Qué cambió en cada versión |

### 03.6 Documentación Técnica CRA ← NUEVO
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 83 | Documentación técnica formal EU CRA | 🟢 | 🏢 | **C:VII** | Descripción general producto, diseño, desarrollo, evaluación riesgos, pruebas |

---

## 📖 04 MANUALES

> **Clasificación**: 🟢 Público / 🔵 Interno  
> **Responsable**: Departamento Software / Ingeniería  
> **¿Qué es?**: Documentación de usuario del producto  
> **Normativas**: **EU CRA Anexo II (principal)** + ISO 9001:7.2-7.3

### 04.1 Manual de Usuario
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 84 | Manual recuperación usuario | ✅ | 🖥️M | **C:II** · Q:8.5 | `user-guides/MANUAL_USUARIO_RECUPERACION.md` |
| 85 | Manual usuario completo (CRA Anexo II) | 🟢 | 🖥️M | **C:II** (obligatorio) | Nombre, fabricante, contacto, instalación, uso, soporte, vulnerabilidades |

### 04.2 Manual de Instalación
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 86 | Instalación producción completa | ✅ | 🖥️M | C:II · Q:8.5 | `deployment/INSTALACION_PRODUCCION.md` |
| 87 | Cómo usar nueva versión | ✅ | 🖥️M | C:I.1(h) · C:II | `deployment/COMO_USAR_NUEVA_VERSION.md` |
| 88 | Configuración modo Kiosk | ✅ | 🖥️M | Q:8.5 | `deployment/README_KIOSK.md` |
| 89 | Deploy servidor empresa | ✅ | 🏢 | Q:8.5 | `deployment/SERVIDOR_EMPRESA.md` |

### 04.3 Formación
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 90 | Material formación operadores | 🟡 | 🖥️M | **Q:7.2, Q:7.3** · S:A.6.3 | Formación operadores de la máquina |
| 91 | Guía rápida de arranque | 🟡 | 🖥️M | C:II · Q:7.2 | Quick start guide |

### 04.4 FAQ / Troubleshooting
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 92 | Troubleshooting animación PLC | ✅ | 🖥️M | Q:8.7 | `development/TROUBLESHOOTING_ANIMACION_PLC.md` |
| 93 | FAQ general del producto | 🟡 | 🖥️M | C:II | FAQ operativo |

---

## 📐 05 PLANTILLAS

> **Clasificación**: 🔵 Interno  
> **Responsable**: Ingeniería / Dirección Técnica  
> **¿Qué es?**: Estándares y plantillas para estandarizar el trabajo  
> **Normativas**: ISO 9001:8.1, 8.4 (estandarización de procesos)

### 05.1 Checklist Nuevo Proyecto
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 94 | Checklist arranque proyecto nuevo | 🟡 | 🏢 | Q:8.1 | Lista para empezar proyecto |
| 95 | Checklist entrega final | 🟡 | 🏢 | Q:8.6 | Lista para entregar al cliente |

### 05.2 Formatos Estándar
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 96 | Template informe ingeniería | 🟡 | 🏢 | Q:7.5 | Formato estándar |
| 97 | Template acta reunión | 🟡 | 🏢 | Q:7.4 | Formato estándar |
| 98 | Template informe test | 🟡 | 🏢 | Q:8.6 · I:S4.4 | Formato estándar |

### 05.3 Componentes Homologados
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 99 | Vocabulario máquina (glosario) | ✅ | 🏢 | Q:7.4 | `internal/VOCABULARIO_MAQUINA.xlsx` |
| 100 | Lista componentes aprobados | 🟡 | 🏢 | **Q:8.4** · S:A.5.19 | Componentes homologados |
| 101 | Proveedores homologados | 🟡 | 🏢 | **Q:8.4** · **S:A.5.19** · I:S6.3 | Lista proveedores evaluados |

### 05.4 Criterios de Aceptación
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 102 | Criterios aceptación SW (SAT/FAT) | 🟡 | 🏢 | **Q:8.6** · I:S4.4 | Qué debe pasar para liberar SW |
| 103 | Criterios aceptación HW | 🟡 | 🏢 | **Q:8.6** | Qué debe pasar para liberar HW |

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
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 104 | Oferta técnica-económica | ⬜ | 🏢 | Q:8.2 | Per-machine |
| 105 | Presupuesto detallado | ⬜ | 🏢 | Q:8.2 | Per-machine |

### 06.2 Especificaciones Cliente
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 106 | Análisis estándares IT del cliente | ✅ | 🏢 | S:A.5.36 · I:S6.1 | `especificaciones_clientes/RhB_IT_Standards_v9.0.4_Analisis.md` |
| 107 | **DOC-11: Requisitos Ciberseguridad Producto** | 🔴 | 🏢 | **I:S6.1, S6.2** · S:A.5.19 · C:I.1 | **CREAR** ~5 págs |
| 108 | Especificaciones técnicas del cliente | ⬜ | 🏢 | **Q:8.2.2, Q:8.2.3** | Requisitos funcionales + revisión contrato |
| 109 | Revisión de requisitos del contrato | 🟢 | 🏢 | **Q:8.2.3** | Acta de revisión: ¿podemos cumplir todo? |

### 06.3 Contrato
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 110 | Contrato firmado | ⬜ | 🏢 | Q:8.2.3 | Per-machine |
| 111 | Condiciones de garantía | ⬜ | 🏢 | Q:8.2.1 · C:II | Per-machine |

### 06.4 Plan de Proyecto
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 112 | Cronograma proyecto (Gantt) | ⬜ | 🏢 | Q:8.1 · I:S8.2 | Per-machine |
| 113 | **DOC-14: Roles Ciberseguridad por Proyecto** | 🔴 | 🏢 | **I:S8.1, S8.2** · S:A.5.2 | **CREAR** ~3 págs (plantilla reutilizable) |

### 06.5 Actas y Comunicaciones
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 114 | Actas reunión con cliente | ⬜ | 🏢 | Q:7.4 | Per-machine |
| 115 | Correspondencia técnica relevante | ⬜ | 🏢 | Q:7.4 | Per-machine |

---

## ⚡ 07 INGENIERÍA — ② Diseñar

> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Ingeniería  
> **Normativas**: ISO 9001:8.3 (diseño y desarrollo)

### 07.1 Esquemas Eléctricos
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 116 | Esquemas de potencia | ⬜ | 🏢 | Q:8.3.5 |
| 117 | Esquemas de control/maniobra | ⬜ | 🏢 | Q:8.3.5 |
| 118 | Lista de cables | ⬜ | 🏢 | Q:8.3.5 |

### 07.2 P&ID (Piping & Instrumentation)
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 119 | Diagrama tuberías e instrumentación | ⬜ | 🏢 | Q:8.3.5 |
| 120 | Lista de instrumentos | ⬜ | 🏢 | Q:8.3.5 |

### 07.3 Layout / Implantación
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 121 | Layout planta 2D | ⬜ | 🏢 | Q:8.3.5 |
| 122 | Layout 3D (si aplica) | ⬜ | 🏢 | Q:8.3.5 |

### 07.4 Planos Mecánicos
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 123 | Planos de conjunto | ⬜ | 🏢 | Q:8.3.5 |
| 124 | Planos de detalle / despiece | ⬜ | 🏢 | Q:8.3.5 |

### 07.5 Esquemas Neumáticos/Hidráulicos
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 125 | Esquemas neumáticos (si aplica) | ⬜ | 🏢 | Q:8.3.5 |

### 07.6 BOM — Lista de Materiales
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 126 | BOM materiales completa | ⬜ | 🏢 | Q:8.3.5 · Q:8.4 |
| 127 | BOM componentes eléctricos | ⬜ | 🏢 | Q:8.3.5 · Q:8.4 |

### 07.7 Datasheets Componentes
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 128 | Fichas técnicas equipos instalados | ⬜ | 🏢 | Q:8.4 |

### 07.8 Planos As-Built
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 129 | Planos "como quedó" (cambios vs diseño) | ⬜ | 🏢 | Q:8.3.6 · Q:8.5.6 |

---

## 🔧 08 TWINCAT / PLC — ③ Programar

> **Clasificación**: 🔴 Restringido  
> **Responsable**: Programador PLC  
> **Normativas**: IEC 62443 S3 (seguridad OT) + ISO 9001:8.3

### 08.1 Proyecto TwinCAT
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 130 | Archivo proyecto .tsproj (backup completo) | ⬜ | 🏢 | Q:8.3.5 · I:S3.2 |
| 131 | Versión TwinCAT y runtime utilizados | ⬜ | 🏢 | I:S3.3 · I:S5.1 |

### 08.2 Configuración I/O
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 132 | Mapa de I/O (entradas/salidas) | ⬜ | 🏢 | Q:8.3.5 · I:S3.2 |
| 133 | Lista de señales con direcciones | ⬜ | 🏢 | Q:8.3.5 |

### 08.3 EtherCAT
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 134 | Topología red EtherCAT | ⬜ | 🏢 | I:S3.1 · S:A.8.20 |
| 135 | Configuración esclavos + firmware | ⬜ | 🏢 | I:S3.2, S3.3 |

### 08.4 Recetas PLC
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 136 | Definición de recetas | ⬜ | 🏢 | Q:8.5.1 |
| 137 | Parámetros de proceso | ⬜ | 🏢 | Q:8.5.1 |

### 08.5 Documentación PLC
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 138 | Descripción funcional del PLC | ⬜ | 🏢 | Q:8.3.5 · C:VII |
| 139 | Lista de Function Blocks | ⬜ | 🏢 | Q:8.3.5 |

---

## ⚙️ 09 CONFIG SW — ④ Configurar

> **Clasificación**: 🟠 Confidencial  
> **Responsable**: Departamento Software  
> **Normativas**: ISO 9001:8.3 + EU CRA I.1(b) (config segura por defecto)

### 09.1 Excel Config
| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 140 | Referencia estructura Excel 15 columnas | ✅ | 🖥️M | Q:8.3.5 | `configuration/ESTRUCTURA_EXCEL_15_COLUMNAS_REFERENCIA.md` |
| 141 | Mapeo columnas Excel | ✅ | 🖥️M | Q:8.3.5 | `configuration/MAPEO_COLUMNAS_EXCEL.md` |
| 142 | System Config sheet | ✅ | 🖥️M | Q:8.3.5 · C:I.1(b) | `configuration/SYSTEM_CONFIG_SHEET.md` |
| 143 | Configuración elementos 3D | ✅ | 🖥️M | Q:8.3.5 | `excel configuration/3D_Elements_Info_Setting.md` |
| 144 | ProjectConfig.xlsm de esta máquina | ⬜ | 🖥️P | Q:8.3.5 · S:A.8.9 | Per-machine |

### 09.2 Modelos 3D
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 145 | Archivos .glb de esta máquina | ⬜ | 🖥️P | Q:8.3.5 |
| 146 | Guía configuración modelos 3D | 🟡 | 🖥️M | Q:8.3.5 |

### 09.3 Base de Datos Proyecto
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 147 | project.db de esta máquina | ⬜ | 🖥️P | S:A.8.13 |
| 148 | Esquema/documentación de la DB | 🟡 | 🖥️M | Q:8.3.5 · S:A.8.13 |

---

## 🛠️ 10 OPERACIONES — ⑤ Mantener

> **Clasificación**: 🔵 Interno  
> **Responsable**: Servicio Técnico / Cliente  
> **Normativas**: ISO 9001:8.5 (producción y servicio) + 8.6 (liberación)

### 10.1 Mantenimiento Preventivo
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 149 | Plan mantenimiento preventivo | ⬜ | 🖥️P | Q:8.5.1 · S:A.7.13 |
| 150 | Checklist inspección periódica | ⬜ | 🖥️P | Q:8.5.1 |
| 151 | Calendario de mantenimiento | ⬜ | 🖥️P | Q:8.5.1 |

### 10.2 Mantenimiento Correctivo
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 152 | Registro de averías | ⬜ | 🖥️P | Q:8.7 · Q:10.2 |
| 153 | Informes de reparación | ⬜ | 🖥️P | Q:8.7 |
| 154 | Análisis causa raíz | ⬜ | 🖥️P | Q:10.2 |

### 10.3 Repuestos
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 155 | Lista repuestos recomendados | ⬜ | 🖥️P | Q:8.5.3 · C:II |
| 156 | Stock mínimo | ⬜ | 🖥️P | Q:8.5.3 |

### 10.4 Histórico Máquina
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 157 | Libro de máquina | ⬜ | 🖥️P | Q:8.5.2 · Q:7.5 |
| 158 | Registro de modificaciones | ⬜ | 🖥️P | Q:8.5.6 · S:A.8.32 |
| 159 | Histórico de alarmas | ⬜ | 🖥️P | C:I.1(g) · S:A.8.15 |

### 10.5 Puesta en Marcha
| # | Documento | Estado | Ubic. | Norma |
|---|-----------|--------|-------|-------|
| 160 | Protocolo puesta en marcha | ⬜ | 🖥️P | **Q:8.6** · I:S4.4 |
| 161 | Checklist commissioning | ⬜ | 🖥️P | **Q:8.6** |
| 162 | Acta de recepción firmada | ⬜ | 🖥️P | **Q:8.6** · Q:8.2.3 |

---

## ⛔ INTERNO — Fuera del Árbol (No publicar)

> **Clasificación**: 🔴 RESTRINGIDO MÁXIMO  
> **Nota**: Credenciales y datos sensibles. NO van en ningún DMS público.

| # | Documento | Estado | Ubic. | Norma | Archivo / Notas |
|---|-----------|--------|-------|-------|-----------------|
| 163 | Credenciales iniciales clientes | ⚠️ | 🏢 | S:A.5.17 | `internal/CLIENTE_CREDENCIALES_INICIALES.md` |
| 164 | Credenciales internas Aquafrisch | ⚠️ | 🏢 | S:A.5.17 | `internal/INTERNAL_AQUAFRISCH_CREDENTIALS.md` |
| 165 | Documentación interna varios | ⚠️ | 🏢 | — | `internal/DOCUMENTACION_INTERNA_AQUAFRISCH.md` |

---

# 📊 MATRICES CRUZADAS MULTINORMATIVAS

## Matriz ISO 9001:2015 — Cobertura completa

| Cláusula | Requisito | Documento(s) en el árbol | Estado |
|----------|-----------|--------------------------|--------|
| 4.1 | Contexto de la organización | #15 Contexto + partes interesadas | 🟢 |
| 4.2 | Partes interesadas | #15 Contexto + partes interesadas | 🟢 |
| 4.3 | Alcance del SGC | #14 Alcance del SGC | 🟢 |
| 4.4 | SGC y procesos | #12 Manual SGC (mapa procesos) | 🟢 |
| 5.1 | Liderazgo y compromiso | #13 Política Calidad, #26 Revisión dirección | 🟢 |
| 5.2 | Política de calidad | #13 Política de Calidad | 🟢 |
| 5.3 | Roles y responsabilidades | #12 Manual SGC | 🟢 |
| 6.1 | Riesgos y oportunidades | #27 Análisis riesgos calidad, #53-54 riesgos ciber | 🟢 |
| 6.2 | Objetivos de calidad | #18 Objetivos calidad anuales | 🟢 |
| 7.1 | Recursos | #12 Manual SGC | 🟢 |
| 7.2 | Competencia | #90 Formación operadores, #59 Concienciación seguridad | 🟡 |
| 7.3 | Toma de conciencia | #59 Plan concienciación, #90 Material formación | 🟡+🟢 |
| 7.4 | Comunicación | #97 Template acta, #114-115 Actas proyecto | 🟡 |
| 7.5 | Información documentada | #16 PGD ✅, #17 Estructura multinormativa ✅ | ✅ |
| 8.1 | Planificación y control operacional | #94 Checklist arranque, #112 Cronograma | 🟡+⬜ |
| 8.2 | Requisitos productos/servicios | #108 Especificaciones, #109 Revisión requisitos | ⬜+🟢 |
| 8.3 | Diseño y desarrollo | Categorías 03, 07, 08, 09 (múltiples docs) | ✅+⬜ |
| 8.4 | Proveedores externos | #101 Proveedores, #52 Eval Terceros, #75 Índice | 🟡+🔴+✅ |
| 8.5 | Producción y servicio | Categoría 10 (mantenimiento, operaciones) | ⬜ |
| 8.6 | Liberación productos | #95 Checklist entrega, #160-162 Puesta en marcha | 🟡+⬜ |
| 8.7 | Salidas no conformes | #21-22 NC + acciones correctivas | 🟢 |
| 9.1 | Seguimiento y medición | #19 KPIs, #20 Satisfacción cliente | 🟢 |
| 9.2 | Auditoría interna | #24-25 Programa + procedimiento auditoría | 🟢 |
| 9.3 | Revisión por la dirección | #26 Actas revisión por dirección | 🟢 |
| 10.1 | Mejora | #23 Plan mejora continua | 🟢 |
| 10.2 | No conformidad y acción correctiva | #21-22 Registro NC + procedimiento | 🟢 |
| 10.3 | Mejora continua | #23 Plan mejora continua | 🟢 |

> **Cobertura ISO 9001**: 100% de cláusulas tienen documento asignado. Estado: ~30% ✅ existente, ~50% 🟢 por crear, ~20% ⬜ per-machine.

---

## Matriz ISO 27001:2022 — Cláusulas obligatorias

| Cláusula | Requisito | Documento(s) en el árbol | Estado |
|----------|-----------|--------------------------|--------|
| 4.1-4.2 | Contexto y partes interesadas | #15 (compartido con Q:4.1-4.2) | 🟢 |
| 4.3 | Alcance del SGSI | #37 Alcance SGSI | 🟢 |
| 5.1 | Liderazgo | #32 DOC-01 Política ciberseguridad | 🔴 |
| 5.2 | Política de seguridad | #32 DOC-01 Política ciberseguridad | 🔴 |
| 5.3 | Roles de seguridad | #33 DOC-03 Organigrama + RACI | 🔴 |
| 6.1.2 | Evaluación de riesgos | #53 Metodología evaluación riesgos | 🟢 |
| 6.1.3 | Tratamiento de riesgos | #54 Registro riesgos + Plan tratamiento | 🟢 |
| 6.1.3d | Declaración de Aplicabilidad | #38 SoA | 🟢 |
| 6.2 | Objetivos de seguridad | #39 Objetivos seguridad | 🟢 |
| 7.1-7.4 | Soporte (recursos, competencia, conciencia, comunicación) | #59 Concienciación, #90 Formación, #12 Manual SGC | 🟢+🟡 |
| 7.5 | Información documentada | #16 PGD | ✅ |
| 8.1-8.3 | Operación (planificar, eval riesgos, tratar riesgos) | #53-54, #32 | 🟢+🔴 |
| 9.1 | Monitorización y medición | #51 DOC-02 Estrategia + KPIs | 🔴 |
| 9.2 | Auditoría interna | #24-25 Programa auditoría (compartido con Q:9.2) | 🟢 |
| 9.3 | Revisión por dirección | #26 Actas revisión (compartido con Q:9.3) | 🟢 |
| 10.1 | No conformidad | #21-22 (compartido con Q:10.2) | 🟢 |
| 10.2 | Mejora continua | #23 (compartido con Q:10.3) | 🟢 |

---

## Matriz ISO 27001:2022 — Controles Anexo A (principales)

| Control | Descripción | Documento(s) en el árbol | Estado |
|---------|-------------|--------------------------|--------|
| A.5.1 | Políticas de seguridad | #32 DOC-01 | 🔴 |
| A.5.2 | Roles de seguridad | #33 DOC-03, #30 ROLES_PERMISSIONS ✅ | 🔴+✅ |
| A.5.9 | Inventario de activos | #55 Inventario activos | 🟢 |
| A.5.10 | Uso aceptable de activos | #55 (parte del inventario) | 🟢 |
| A.5.12-13 | Clasificación y etiquetado | #40 Política clasificación | 🟢 |
| A.5.15-18 | Control de acceso | #35 DOC-06, #44 GESTION_USUARIOS ✅ | 🔴+✅ |
| A.5.19-21 | Proveedores | #52 DOC-12, #75 INDICE_TERCEROS ✅ | 🔴+✅ |
| A.5.24-26 | Gestión de incidentes | #46 DOC-04 | 🔴 |
| A.5.29-30 | Continuidad de negocio | #56 BCP, #57 DRP | 🟢 |
| A.5.36 | Cumplimiento | #50 Gap analysis ✅, #43 Roadmap CRA ✅ | ✅ |
| A.6.1-6.6 | Personas (antes empleo, durante, terminación) | #58 Seguridad RRHH | 🟢 |
| A.6.3 | Concienciación | #59 Plan concienciación | 🟢 |
| A.6.6 | Confidencialidad | #60 NDA tipo | 🟢 |
| A.7.1-7.4 | Seguridad física | #34 DOC-05 | 🔴 |
| A.8.1-5 | Dispositivos y acceso | #35 DOC-06 | 🔴 |
| A.8.8 | Gestión vulnerabilidades técnicas | #11 DOC-13 | 🔴 |
| A.8.9 | Gestión de configuración | #144 ProjectConfig, #36 DOC-07 | 🔴+⬜ |
| A.8.13 | Backup | #63 DATA_MANAGEMENT ✅ | ✅ |
| A.8.15-16 | Logging y monitoring | #45 SISTEMA_LOGS ✅, #62 ARQUITECTURA_LOGS ✅ | ✅ |
| A.8.20-22 | Seguridad de red | #42 Política seguridad red | 🟢 |
| A.8.24 | Criptografía | #41 Política criptografía | 🟢 |
| A.8.25-27 | Desarrollo seguro | #73 DOC-08 SDL, #68 GUIA_DESARROLLO ✅ | 🔴+✅ |
| A.8.28 | Codificación segura | #74 DOC-09 | 🔴 |
| A.8.29 | Testing | #81 Plan testing | 🟡 |

---

## Matriz IEC 62443 — Checklist Proveedor S1-S8

| Punto | Descripción | Documento(s) en el árbol | Estado |
|-------|-------------|--------------------------|--------|
| S1.1 | Política de ciberseguridad | #32 DOC-01 | 🔴 |
| S1.2 | Estrategia de ciberseguridad | #51 DOC-02 | 🔴 |
| S1.3 | KPIs de seguridad | #51 DOC-02 | 🔴 |
| S1.4 | Organización ciberseguridad | #33 DOC-03 | 🔴 |
| S1.5 | Responsable de ciberseguridad | #33 DOC-03 | 🔴 |
| S2.1 | Seguridad física | #34 DOC-05 | 🔴 |
| S2.2 | Seguridad de red | #34 DOC-05, #42 Política red | 🔴+🟢 |
| S2.3 | Gestión de cuentas | #35 DOC-06 | 🔴 |
| S2.4 | Gestión de incidentes | #46 DOC-04 | 🔴 |
| S2.5 | Acceso a instalaciones | #34 DOC-05 | 🔴 |
| S3.1 | Separación IT/OT | #36 DOC-07, #42 Política red | 🔴+🟢 |
| S3.2 | Gestión activos OT | #36 DOC-07 | 🔴 |
| S3.3 | Actualización OT | #36 DOC-07 | 🔴 |
| S4.1 | SDL (proceso desarrollo seguro) | #73 DOC-08 | 🔴 |
| S4.2 | Requisitos seguridad en diseño | #73 DOC-08 | 🔴 |
| S4.3 | Codificación segura | #74 DOC-09 | 🔴 |
| S4.4 | Verificación / testing | #73 DOC-08, #81 Plan testing | 🔴+🟡 |
| S5.1 | SBOM | #78 DOC-10 | 🔴 |
| S5.2 | Integridad software | #47 SOFTWARE_INTEGRITY ✅, #48-49 | **✅** |
| S6.1 | Requisitos ciber al proveedor | #107 DOC-11 | 🔴 |
| S6.2 | Evaluación proveedor | #107 DOC-11 | 🔴 |
| S6.3 | Monitoreo proveedor | #52 DOC-12 | 🔴 |
| S7.1 | Proceso gestión vulnerabilidades | #11 DOC-13 | 🔴 |
| S7.2 | Comunicación vulnerabilidades | #11 DOC-13, #10 VULNERABILITY_REPORT ✅ | 🔴+✅ |
| S8.1 | Roles ciber por proyecto | #113 DOC-14 | 🔴 |
| S8.2 | Integración seguridad en proyecto | #113 DOC-14 | 🔴 |

> **Cobertura IEC 62443**: S5.2 ya cubierto ✅. Los 14 DOCs (🔴) cubren los otros 30 puntos. Deadline: **abril 2026**.

---

## Matriz EU CRA — Cyber Resilience Act

| Requisito | Descripción | Documento(s) en el árbol | Estado |
|-----------|-------------|--------------------------|--------|
| **Anexo I.1(a)** | Sin vulnerabilidades conocidas | #73 DOC-08 SDL, #74 DOC-09, #10 VULNERABILITY_REPORT ✅ | 🔴+✅ |
| **Anexo I.1(b)** | Config segura por defecto | #67 SYSTEM_CONFIG ✅, #142 System Config Sheet ✅ | ✅ |
| **Anexo I.1(c)** | Proteger datos en tránsito | #41 Política criptografía (TLS, HTTPS) | 🟢 |
| **Anexo I.1(d)** | Proteger contra acceso no autorizado | #35 DOC-06 Gestión Cuentas, #44 GESTION_USUARIOS ✅ | 🔴+✅ |
| **Anexo I.1(e)** | Minimizar superficie de ataque | #73 DOC-08 SDL, #61 ARQUITECTURA ✅ | 🔴+✅ |
| **Anexo I.1(f)** | Minimizar impacto incidentes | #46 DOC-04 Plan Incidentes, #63 DATA_MANAGEMENT ✅ | 🔴+✅ |
| **Anexo I.1(g)** | Registrar actividad/eventos | #45 SISTEMA_LOGS ✅, #62 ARQUITECTURA_LOGS ✅ | **✅** |
| **Anexo I.1(h)** | Mecanismo actualización seguro | #47 SOFTWARE_INTEGRITY ✅, #87 COMO_USAR_NUEVA_VERSION ✅ | **✅** |
| **Anexo I.2** | Gestión de vulnerabilidades | #11 DOC-13 Proceso Gestión Vulnerabilidades | 🔴 |
| **Anexo II** | Información al usuario | #85 Manual usuario completo, #8 Ficha técnica, #82 Release Notes | 🟢+🟡 |
| **Anexo V** | Declaración de conformidad | #9 Declaración conformidad EU CRA | 🟢 |
| **Anexo VII** | Documentación técnica | #83 Documentación técnica CRA | 🟢 |

> **Cobertura EU CRA**: I.1(b), I.1(g), I.1(h) ya cubiertos ✅. Resto necesita los DOCs de auditoría (🔴) + documentos normativos (🟢).

---

# 🔴 RESUMEN: DOCUMENTOS POR CREAR — PRIORIZADO

## Prioridad 1: AUDITORÍA IEC 62443 — Abril 2026 (14 documentos)

| ID | Documento | Posición | Ubic. | Norma principal | Págs |
|----|-----------|----------|-------|-----------------|------|
| DOC-01 | Política de Ciberseguridad | 02.1 #32 | 🏢 | I:S1.1 · S:5.2, A.5.1 | ~8 |
| DOC-02 | Estrategia Ciberseguridad + KPIs | 02.4 #51 | 🏢 | I:S1.2, S1.3 · S:9.1 | ~5 |
| DOC-03 | Organigrama + RACI Ciberseguridad | 02.1 #33 | 🏢 | I:S1.4, S1.5 · S:5.3, A.5.2 | ~4 |
| DOC-04 | Plan de Gestión de Incidentes | 02.2 #46 | 🏢 | I:S2.4 · S:A.5.24-26 · C:I.1(f) | ~6 |
| DOC-05 | Política Protección Física y TI | 02.1 #34 | 🏢 | I:S2.1, S2.2, S2.5 · S:A.7.1-4 | ~4 |
| DOC-06 | Política Gestión Cuentas TI | 02.1 #35 | 🏢 | I:S2.3 · S:A.5.15-18, A.8.2-5 | ~3 |
| DOC-07 | Política Seguridad OT (TwinCAT/PLC) | 02.1 #36 | 🏢 | I:S3.1-3.3 · S:A.8.9 | ~5 |
| DOC-08 | SDL — Proceso Desarrollo Seguro | 03.2 #73 | 🏢 | I:S4.1, S4.2, S4.4 · S:A.8.25-27 · C:I.1(a,e) | ~8 |
| DOC-09 | Secure Coding Guidelines | 03.3 #74 | 🏢 | I:S4.3 · S:A.8.28 · C:I.1(a) | ~6 |
| DOC-10 | SBOM Formal | 03.4 #78 | 🏢 | I:S5.1 · C:VII · S:A.5.19 | ~3 |
| DOC-11 | Requisitos Ciberseguridad Producto | 06.2 #107 | 🏢 | I:S6.1, S6.2 · S:A.5.19 | ~5 |
| DOC-12 | Procedimiento Evaluación Terceros | 02.4 #52 | 🏢 | I:S6.3 · S:A.5.19-21 · Q:8.4 | ~4 |
| DOC-13 | Proceso Gestión Vulnerabilidades | 00.5 #11 | 🖥️M | I:S7.1, S7.2 · C:I.2 · S:A.8.8 | ~5 |
| DOC-14 | Roles Ciberseguridad por Proyecto | 06.4 #113 | 🏢 | I:S8.1, S8.2 · S:A.5.2 | ~3 |
| | **Subtotal** | | | | **~69 págs** |

## Prioridad 2: NORMATIVAS ISO (22 documentos 🟢)

| # | Documento | Posición | Norma principal | Págs |
|---|-----------|----------|-----------------|------|
| 12 | Manual del SGC | 01.1 | Q:4.4, Q:5.3, Q:7.1 | ~15 |
| 13 | Política de Calidad | 01.1 | Q:5.2 | ~2 |
| 14 | Alcance del SGC | 01.1 | Q:4.3 | ~2 |
| 15 | Contexto + partes interesadas | 01.1 | Q:4.1, Q:4.2 | ~4 |
| 18 | Objetivos calidad | 01.3 | Q:6.2 | ~2 |
| 19 | KPIs procesos | 01.3 | Q:9.1.1 | ~3 |
| 20 | Satisfacción cliente | 01.3 | Q:9.1.2 | ~2 |
| 21 | Registro NC | 01.4 | Q:10.2 · S:10.1 | ~2 |
| 22 | Procedimiento acc. correctivas | 01.4 | Q:10.2 · S:10.1 | ~3 |
| 23 | Plan mejora continua | 01.5 | Q:10.3 · S:10.2 | ~2 |
| 24-25 | Programa + proc. auditoría interna | 01.6 | Q:9.2 · S:9.2 | ~5 |
| 26 | Actas revisión dirección | 01.6 | Q:9.3 · S:9.3 | ~2 |
| 27 | Análisis riesgos calidad | 01.7 | Q:6.1 | ~3 |
| 37 | Alcance SGSI | 02.1 | S:4.3 | ~2 |
| 38 | Declaración Aplicabilidad (SoA) | 02.1 | S:6.1.3d | ~8 |
| 39 | Objetivos seguridad | 02.1 | S:6.2 | ~2 |
| 40 | Política clasificación | 02.1 | S:A.5.12-13 | ~3 |
| 41 | Política criptografía | 02.1 | S:A.8.24 · C:I.1(c) | ~3 |
| 42 | Política seguridad red | 02.1 | S:A.8.20-22 | ~3 |
| 53 | Metodología riesgos | 02.5 | S:6.1.2 | ~4 |
| 54 | Registro riesgos + tratamiento | 02.5 | S:6.1.3, S:8.3 | ~5 |
| 55 | Inventario activos | 02.5 | S:A.5.9-11 | ~5 |
| 56-57 | BCP + DRP | 02.6 | S:A.5.29-30 | ~6 |
| 58-60 | Seguridad RRHH + concienciación + NDA | 02.7 | S:A.6.1-6 | ~6 |
| 82 | Release Notes formal | 03.5 | C:II · I:S5.2 | ~2 |
| 83 | Doc técnica CRA | 03.6 | C:VII | ~8 |
| 85 | Manual usuario CRA | 04.1 | C:II | ~10 |
| 109 | Revisión requisitos contrato | 06.2 | Q:8.2.3 | ~1 |
| | **Subtotal** | | | **~100 págs** |

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
> 2. Planificar los 22 documentos normativos ISO 9001/27001/CRA  
> 3. Modificar las categorías del Aquafrisch Supervisor DMS  
> 4. Planificar el desarrollo del DMS Empresa  
