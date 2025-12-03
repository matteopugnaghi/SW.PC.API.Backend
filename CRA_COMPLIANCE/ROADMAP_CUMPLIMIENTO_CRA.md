# 🇪🇺 ROADMAP DE CUMPLIMIENTO - REGLAMENTO (UE) 2024/2847
## Cyber Resilience Act (CRA) - Sistema SCADA/HMI Industrial

**Documento**: Plan de Implementación para Cumplimiento Normativo  
**Fecha**: Diciembre 2025  
**Versión**: 1.5  
**Producto**: Sistema SCADA/HMI con visualización 3D  

---

## 📅 FECHAS CLAVE DE APLICACIÓN

| Fecha | Obligación | Estado |
|-------|------------|--------|
| **11 junio 2026** | Notificación de organismos de evaluación | ⏳ Preparar |
| **11 septiembre 2026** | **Obligaciones de notificación de vulnerabilidades (Art. 14)** | 🔴 CRÍTICO |
| **11 diciembre 2027** | **Aplicación TOTAL del Reglamento** | 🔴 DEADLINE |

---

## 📊 ESTADO ACTUAL DE CUMPLIMIENTO

```
                    CUMPLIMIENTO CRA - DICIEMBRE 2025
    ┌────────────────────────────────────────────────────────┐
    │                                                        │
    │  Requisitos del Producto (Anexo I, Parte I)  [████████░░] 75%
    │  Gestión Vulnerabilidades (Anexo I, Parte II) [████░░░░░░] 35%
    │  Documentación Técnica (Anexo VII)            [██░░░░░░░░] 20%
    │  Información al Usuario (Anexo II)            [███░░░░░░░] 25%
    │  Sistema de Notificaciones (Art. 14)          [░░░░░░░░░░]  0%
    │                                                        │
    │  CUMPLIMIENTO GLOBAL                          [████░░░░░░] 35%
    │                                                        │
    └────────────────────────────────────────────────────────┘
```

---

## ✅ LO QUE YA TENEMOS IMPLEMENTADO

| Requisito CRA | Implementación Actual | Artículo/Anexo |
|---------------|----------------------|----------------|
| Integridad del código | ✅ Git + GPG/SSH signatures | Anexo I, Parte I, 2f |
| Trazabilidad de versiones | ✅ Panel SOFTWARE VERSIONS | Art. 13.7 |
| Autenticación | ✅ JWT implementado | Anexo I, Parte I, 2d |
| Control de acceso | ✅ Sistema de roles | Anexo I, Parte I, 2d |
| Verificación por componente | ✅ Backend/Frontend/PLC | Anexo I, Parte II, 1 |
| Identificación del producto | ✅ Versiones visibles | Art. 13.15 |

---

## 🔴 IMPLEMENTACIONES PENDIENTES

### FASE 1: CONTROL DE VERSIONES (EN CURSO)
**Prioridad**: 🔴 ALTA  
**Fecha objetivo**: Diciembre 2025

| Tarea | Estado | Descripción |
|-------|--------|-------------|
| Panel SOFTWARE VERSIONS | ✅ 90% | Mostrar versiones Git de Backend/Frontend/PLC |
| Verificación GPG/SSH | ✅ Implementado | Verificar firmas de commits |
| Información por componente | ✅ Implementado | Autor, email, mensaje, fecha verificación |
| Timer de re-verificación | ⏳ Pendiente | Re-verificar cada 2 minutos |
| TwinCAT Runtime real | ✅ Corregido | Mostrar versión real después de conexión |

---

### FASE 2: SBOM - SOFTWARE BILL OF MATERIALS
**Prioridad**: 🔴 ALTA  
**Fecha objetivo**: Enero 2026  
**Referencia**: Anexo I, Parte II, punto 1

| Tarea | Descripción |
|-------|-------------|
| Instalar CycloneDX (.NET) | `dotnet tool install --global CycloneDX` |
| Instalar CycloneDX (npm) | `npm install -g @cyclonedx/cyclonedx-npm` |
| Generar SBOM Backend | Formato JSON CycloneDX |
| Generar SBOM Frontend | Formato JSON CycloneDX |
| Integrar en build | Generación automática en cada release |
| Almacenar con releases | Conservar 10 años mínimo |

**Entregables**:
- `SBOM-Backend-vX.X.X.json`
- `SBOM-Frontend-vX.X.X.json`
- Script de generación automática

---

### FASE 3: PERÍODO DE SOPORTE
**Prioridad**: 🔴 ALTA  
**Fecha objetivo**: Enero 2026  
**Referencia**: Art. 13.8, Art. 13.19

| Tarea | Descripción |
|-------|-------------|
| Definir período | Mínimo 5 años desde comercialización |
| Mostrar en UI | Fecha fin visible en InfoPanel |
| Incluir en documentación | Manual de usuario, ficha técnica |
| Notificación fin de soporte | Aviso cuando queden 6 meses |

**Criterios para determinar período** (Art. 13.8):
- Expectativas razonables de usuarios
- Naturaleza del producto (industrial = vida larga)
- Derecho de la UE aplicable
- Productos similares en el mercado

**Recomendación**: **10 años** para software industrial SCADA

---

### FASE 4: CANAL DE REPORTE DE VULNERABILIDADES
**Prioridad**: 🔴 CRÍTICA (obligatorio sept 2026)  
**Fecha objetivo**: Febrero 2026  
**Referencia**: Art. 13.6, Art. 13.17, Anexo I Parte II punto 6

| Tarea | Descripción |
|-------|-------------|
| Crear email dedicado | security@empresa.com |
| Formulario web (opcional) | Página de reporte de vulnerabilidades |
| Mostrar en UI | Visible en InfoPanel y documentación |
| Proceso de respuesta | SLA definido (24h acuse recibo) |

**Requisitos del punto de contacto** (Art. 13.17):
- Fácilmente identificable para usuarios
- No solo herramientas automatizadas (chatbots)
- Múltiples medios de comunicación
- Información actualizada

---

### FASE 5: POLÍTICA DE DIVULGACIÓN COORDINADA
**Prioridad**: 🔴 CRÍTICA (obligatorio sept 2026)  
**Fecha objetivo**: Febrero 2026  
**Referencia**: Anexo I, Parte II, punto 5

**Documento a crear**: `SECURITY_POLICY.md`

Contenido obligatorio:
```markdown
1. Cómo reportar vulnerabilidades
2. Qué información incluir en el reporte
3. Tiempos de respuesta comprometidos:
   - Acuse de recibo: 24 horas
   - Evaluación inicial: 72 horas
   - Plan de acción: 7 días
   - Parche disponible: según severidad
4. Proceso de coordinación con investigadores
5. Política de reconocimiento (créditos)
6. Contacto con CSIRT nacional
```

---

### FASE 6: EVALUACIÓN DE RIESGOS DE CIBERSEGURIDAD
**Prioridad**: 🟡 MEDIA  
**Fecha objetivo**: Marzo 2026  
**Referencia**: Art. 13.2, Art. 13.3, Anexo VII punto 3

**Documento a crear**: `EVALUACION_RIESGOS_CIBERSEGURIDAD.pdf`

Contenido obligatorio:
```
1. Descripción del producto y finalidad prevista
2. Análisis del entorno operativo
3. Activos a proteger (datos, funciones, accesos)
4. Identificación de amenazas
5. Análisis de vulnerabilidades potenciales
6. Evaluación de riesgos (probabilidad x impacto)
7. Medidas de mitigación implementadas
8. Riesgos residuales aceptados
9. Plan de revisión periódica
```

---

### FASE 7: DOCUMENTACIÓN TÉCNICA (Anexo VII)
**Prioridad**: 🟡 MEDIA  
**Fecha objetivo**: Junio 2026  
**Referencia**: Art. 31, Anexo VII

**Contenido obligatorio**:

| Elemento | Descripción | Estado |
|----------|-------------|--------|
| Descripción general | Finalidad, versiones, arquitectura | ⏳ Parcial |
| Diseño y desarrollo | Planos, esquemas, arquitectura sistema | ⏳ Parcial |
| Gestión vulnerabilidades | SBOM, política divulgación, proceso parches | ❌ Pendiente |
| Evaluación de riesgos | Documento formal | ❌ Pendiente |
| Período de soporte | Justificación del período elegido | ❌ Pendiente |
| Normas aplicadas | Lista de normas armonizadas | ❌ Pendiente |
| Informes de pruebas | Tests de seguridad realizados | ❌ Pendiente |
| Declaración conformidad | Copia del documento | ❌ Pendiente |

---

### FASE 8: INFORMACIÓN AL USUARIO (Anexo II)
**Prioridad**: 🟡 MEDIA  
**Fecha objetivo**: Junio 2026  
**Referencia**: Art. 13.18, Anexo II

**Documento a crear**: `MANUAL_SEGURIDAD_USUARIO.pdf`

Contenido obligatorio:
```
1. Datos del fabricante (nombre, dirección, contacto)
2. Punto de contacto para vulnerabilidades
3. Identificación del producto (nombre, tipo, versión)
4. Finalidad prevista y entorno de seguridad
5. Circunstancias de riesgo conocidas
6. Enlace a declaración de conformidad
7. Fecha fin del período de soporte
8. Instrucciones de:
   - Instalación segura
   - Configuración segura
   - Instalación de actualizaciones
   - Retirada segura del servicio
   - Eliminación de datos
9. Información para integradores (si aplica)
```

---

### FASE 9: DECLARACIÓN UE DE CONFORMIDAD
**Prioridad**: 🟡 MEDIA  
**Fecha objetivo**: Septiembre 2026  
**Referencia**: Art. 28, Anexo V

**Documento a crear**: `DECLARACION_UE_CONFORMIDAD.pdf`

Contenido obligatorio (Anexo V):
```
1. Nombre y tipo del producto
2. Nombre y dirección del fabricante
3. Declaración de responsabilidad exclusiva
4. Objeto de la declaración (identificación del producto)
5. Afirmación de conformidad con legislación UE
6. Referencias a normas armonizadas aplicadas
7. Datos del organismo notificado (si aplica)
8. Información adicional
9. Firma, lugar y fecha
```

---

### FASE 10: SISTEMA DE ACTUALIZACIONES SEGURAS
**Prioridad**: 🟢 MEDIA-BAJA  
**Fecha objetivo**: Septiembre 2026  
**Referencia**: Anexo I, Parte I, 2c; Anexo I, Parte II, 7-8

| Tarea | Descripción |
|-------|-------------|
| Mecanismo de distribución | Canal seguro para parches |
| Firmas en actualizaciones | Verificar integridad con GPG/SHA256 |
| Notificación a usuarios | Avisar de actualizaciones disponibles |
| Actualizaciones separadas | Seguridad separada de funcionalidad |
| Opt-out configurable | Usuario puede desactivar auto-update |

---

### FASE 11: LOGGING DE SEGURIDAD
**Prioridad**: 🟢 MEDIA-BAJA  
**Fecha objetivo**: Octubre 2026  
**Referencia**: Anexo I, Parte I, 2l

| Evento a registrar | Información |
|-------------------|-------------|
| Accesos al sistema | Usuario, fecha, IP, resultado |
| Cambios de configuración | Qué cambió, quién, cuándo |
| Errores de autenticación | Intentos fallidos |
| Modificación de datos | Qué datos, quién, cuándo |
| Accesos a funciones sensibles | PLC, configuración, admin |

---

### FASE 12: CIFRADO DE DATOS SENSIBLES
**Prioridad**: 🟢 MEDIA-BAJA  
**Fecha objetivo**: Octubre 2026  
**Referencia**: Anexo I, Parte I, 2e

| Ámbito | Implementación |
|--------|----------------|
| Datos en tránsito | HTTPS obligatorio (ya implementado) |
| Configuración Excel | Cifrar campos sensibles |
| Credenciales | No almacenar en texto plano |
| Base de datos | Cifrado de columnas sensibles |

---

### FASE 13: ELIMINACIÓN SEGURA DE DATOS
**Prioridad**: 🟢 BAJA  
**Fecha objetivo**: Noviembre 2026  
**Referencia**: Anexo I, Parte I, 2m

| Funcionalidad | Descripción |
|---------------|-------------|
| Función de borrado | Eliminar todos los datos de usuario |
| Borrado permanente | Sin posibilidad de recuperación |
| Confirmación | Doble confirmación antes de borrar |
| Registro | Log de que se solicitó eliminación |

---

### FASE 14: SISTEMA DE NOTIFICACIÓN CSIRT
**Prioridad**: 🔴 CRÍTICA (obligatorio sept 2026)  
**Fecha objetivo**: Agosto 2026  
**Referencia**: Art. 14, Art. 16

| Tarea | Descripción |
|-------|-------------|
| Identificar CSIRT español | CCN-CERT o INCIBE-CERT |
| Registrarse en plataforma | Plataforma única de notificación UE |
| Proceso interno | Quién notifica, cómo, cuándo |
| Templates de notificación | Formularios pre-preparados |
| Simulacro | Probar el proceso antes de sept 2026 |

**Plazos de notificación**:

| Tipo | 24h | 72h | Final |
|------|-----|-----|-------|
| Vulnerabilidad explotada | Alerta temprana | Detalles | 14 días tras parche |
| Incidente grave | Alerta temprana | Detalles | 1 mes |

---

## 📋 CRONOGRAMA RESUMEN

```
2025
────────────────────────────────────────────────────────────────
DIC │ ████ FASE 1: Control de Versiones (completar)

2026
────────────────────────────────────────────────────────────────
ENE │ ████ FASE 2: SBOM
    │ ████ FASE 3: Período de Soporte
────────────────────────────────────────────────────────────────
FEB │ ████ FASE 4: Canal de Reporte Vulnerabilidades
    │ ████ FASE 5: Política de Divulgación
────────────────────────────────────────────────────────────────
MAR │ ████ FASE 6: Evaluación de Riesgos
────────────────────────────────────────────────────────────────
ABR │ ░░░░ Buffer / Revisión
────────────────────────────────────────────────────────────────
MAY │ ░░░░ Buffer / Revisión
────────────────────────────────────────────────────────────────
JUN │ ████ FASE 7: Documentación Técnica
    │ ████ FASE 8: Información al Usuario
    │ ⚠️  11 JUN - Deadline organismos notificación
────────────────────────────────────────────────────────────────
JUL │ ░░░░ Buffer / Pruebas
────────────────────────────────────────────────────────────────
AGO │ ████ FASE 14: Sistema Notificación CSIRT
────────────────────────────────────────────────────────────────
SEP │ ████ FASE 9: Declaración UE Conformidad
    │ ████ FASE 10: Sistema Actualizaciones
    │ 🔴 11 SEP - OBLIGATORIO: Notificaciones Art. 14
────────────────────────────────────────────────────────────────
OCT │ ████ FASE 11: Logging de Seguridad
    │ ████ FASE 12: Cifrado de Datos
────────────────────────────────────────────────────────────────
NOV │ ████ FASE 13: Eliminación Segura de Datos
────────────────────────────────────────────────────────────────
DIC │ ░░░░ Revisión final / Auditoría interna

2027
────────────────────────────────────────────────────────────────
    │ 🔴 11 DIC - APLICACIÓN TOTAL DEL CRA
```

---

## 📁 ESTRUCTURA DE DOCUMENTACIÓN COMPLETA

Como **fabricantes de maquinaria industrial**, la documentación se divide en dos grandes bloques:
1. **Documentación de Máquinas** (Directiva Máquinas 2006/42/CE → 2023/1230)
2. **Documentación CRA** (Cyber Resilience Act - Software integrado)

```
📁 DOCUMENTACION_EMPRESA/
│
│   ╔══════════════════════════════════════════════════════════════════╗
│   ║  🏭 MAQUINAS/ - Directiva Máquinas (documentación física)        ║
│   ╚══════════════════════════════════════════════════════════════════╝
├── 📁 MAQUINAS/
│   ├── 📁 MODELO_MAQUINA_001/                  ← Por cada modelo de máquina
│   │   ├── 📁 PLANOS/
│   │   │   ├── Plano_General.dwg/.pdf
│   │   │   ├── Esquemas_Electricos.pdf
│   │   │   ├── Esquemas_Neumaticos.pdf
│   │   │   └── Esquemas_Hidraulicos.pdf
│   │   ├── 📁 MANUALES/
│   │   │   ├── Manual_Usuario_Maquina.pdf
│   │   │   ├── Manual_Mantenimiento.pdf
│   │   │   ├── Manual_Instalacion.pdf
│   │   │   └── Lista_Recambios.pdf
│   │   ├── 📁 CERTIFICACIONES/
│   │   │   ├── Declaracion_CE_Maquina.pdf      ← Directiva Máquinas
│   │   │   ├── Analisis_Riesgos_Maquina.pdf    ← Seguridad física
│   │   │   ├── Informes_Auditorias.pdf
│   │   │   └── Certificados_Componentes/       ← CE de componentes
│   │   ├── 📁 COMPONENTES/
│   │   │   └── Fichas_Tecnicas/
│   │   └── README_MAQUINA.md                   ← Índice del modelo
│   │
│   ├── 📁 MODELO_MAQUINA_002/
│   │   └── ...
│   │
│   └── 📁 PLANTILLAS_MAQUINAS/                 ← Templates para nuevos modelos
│       ├── Template_Manual_Usuario.docx
│       ├── Template_Analisis_Riesgos.xlsx
│       └── Template_Declaracion_CE.docx
│
│   ╔══════════════════════════════════════════════════════════════════╗
│   ║  🔐 CRA_COMPLIANCE/ - Cyber Resilience Act (software integrado)  ║
│   ╚══════════════════════════════════════════════════════════════════╝
└── 📁 CRA_COMPLIANCE/
    ├── 📄 ROADMAP_CUMPLIMIENTO_CRA.md          ← Este documento (INTERNO)
    │
    │   ┌──────────────────────────────────────────────────────────────┐
    │   │  🌐 PUBLICA/ - Accesible sin login (web empresa)            │
    │   └──────────────────────────────────────────────────────────────┘
    ├── 📁 PUBLICA/
    │   ├── SECURITY_POLICY.md                  ← Política divulgación vulnerabilidades
    │   ├── Como_Reportar_Vulnerabilidades.md   ← Instrucciones para investigadores
    │   ├── Periodos_Soporte.md                 ← Tabla versiones y fechas fin soporte
    │   ├── Manual_Usuario_Software.pdf         ← Sin datos sensibles
    │   ├── Guia_Instalacion_Segura.pdf         ← Recomendaciones generales
    │   └── Declaracion_UE_Conformidad_CRA.pdf  ← Obligatorio por Art. 28
    │
    │   ┌──────────────────────────────────────────────────────────────┐
    │   │  🔐 PORTAL_CLIENTE/ - Solo clientes con login                │
    │   └──────────────────────────────────────────────────────────────┘
    ├── 📁 PORTAL_CLIENTE/
    │   ├── README_PORTAL.md                    ← Explicación del sistema
    │   └── 📁 PLANTILLAS/                      ← Templates para cada cliente
    │       ├── SBOM_Template.json
    │       ├── Configuracion_Especifica_Template.md
    │       ├── Manual_Tecnico_Completo_Template.pdf
    │       └── Historial_Actualizaciones_Template.md
    │
    │   ┌──────────────────────────────────────────────────────────────┐
    │   │  🔒 INTERNO/ - Solo empresa (nunca publicar)                 │
    │   └──────────────────────────────────────────────────────────────┘
    └── 📁 INTERNO/
        │
        ├── 📁 DOCUMENTACION_TECNICA/           ← Para auditorías CRA
        │   ├── Descripcion_General.pdf
        │   ├── Arquitectura_Sistema.pdf
        │   ├── Evaluacion_Riesgos_Ciberseguridad.pdf  ← ⚠️ CONFIDENCIAL
        │   ├── Informes_Pruebas_Seguridad.pdf
        │   └── Componentes_Terceros.pdf
        │
        ├── 📁 SEGURIDAD/                       ← Procesos internos
        │   ├── Proceso_Notificacion_CSIRT.pdf
        │   ├── Proceso_Gestion_Vulnerabilidades.md
        │   └── 📁 Plantillas_Notificacion/
        │
        ├── 📁 TERCEROS/                        ← Documentación de terceros
        │   ├── INDICE_TERCEROS.md
        │   ├── 📁 BECKHOFF/
        │   │   ├── README_BECKHOFF.md
        │   │   ├── IPC_Security_Guideline_Win11_en.pdf
        │   │   ├── TwinCAT_Security_Hardening.pdf
        │   │   └── Nuestra_Configuracion_Beckhoff.md
        │   ├── 📁 MICROSOFT/
        │   │   ├── README_MICROSOFT.md
        │   │   └── Nuestra_Configuracion_Windows.md
        │   └── 📁 OTROS/
        │
        ├── 📁 POR_PROYECTO/                    ← Datos de CADA instalación
        │   ├── 📁 CLIENTE_001_MAQUINA_XXX/
        │   │   ├── Configuracion_Especifica.md ← IPs, puertos, usuarios
        │   │   ├── Evaluacion_Riesgos.pdf      ← Riesgos de ESA instalación
        │   │   ├── Credenciales_Entrega.md     ← ⚠️ CIFRADO - Destruir tras uso
        │   │   ├── SBOM_Instalacion.json       ← SBOM específico de esta instalación
        │   │   ├── Historial_Actualizaciones.md
        │   │   └── 🔗 Link_Docs_Maquina.md     ← Referencia a MAQUINAS/MODELO_XXX
        │   ├── 📁 CLIENTE_002_MAQUINA_YYY/
        │   └── ...
        │
        ├── 📁 VERSIONES/                       ← SBOMs genéricos por release
        │   ├── 📁 Release_v1.0.0/
        │   │   ├── SBOM-Backend.json           ← SBOM del código base
        │   │   ├── SBOM-Frontend.json
        │   │   └── Changelog.md
        │   └── 📁 Release_v1.1.0/
        │       └── ...
        │
        └── 📁 LEGAL/
            └── Periodo_Soporte_Interno.pdf     ← Justificación del período
```

### 🔗 Relación entre Documentación de Máquinas y CRA

| En documentación MÁQUINA | Referencia a CRA |
|--------------------------|------------------|
| Manual Usuario Máquina | "Sistema de control: ver documentación CRA en portal" |
| Declaración CE Máquina | "Software conforme a Reglamento (UE) 2024/2847" |
| Análisis Riesgos Máquina | "Riesgos ciberseguridad: ver Evaluación CRA" |

| En documentación CRA | Referencia a MÁQUINA |
|----------------------|----------------------|
| Evaluación Riesgos Ciber | "Este software se integra en máquina modelo XXX" |
| Manual Usuario Software | "Para instalación física ver manual de máquina" |
| Por Proyecto | "Máquina asociada: ver MAQUINAS/MODELO_XXX" |

### Resumen de Acceso

| Carpeta | Quién accede | Dónde se publica | Normativa |
|---------|--------------|------------------|-----------|
| `MAQUINAS/` | Interno + Cliente (su máquina) | Con entrega máquina | Dir. Máquinas |
| `CRA_COMPLIANCE/PUBLICA/` | Cualquiera | Web empresa | CRA |
| `CRA_COMPLIANCE/PORTAL_CLIENTE/` | Solo ese cliente | Portal con login | CRA |
| `CRA_COMPLIANCE/INTERNO/` | Solo empleados | Servidor interno (nunca web) | CRA |

> **Nota**: `VERSIONES/` ahora está dentro de `INTERNO/` porque los SBOMs contienen 
> información detallada de dependencias que podría revelar vulnerabilidades.

---

## 🌐 DISTRIBUCIÓN DE DOCUMENTACIÓN (Pública vs Privada)

El CRA exige que cierta información sea **accesible al público**, mientras que otra debe 
mantenerse **confidencial**. Esta sección define qué va dónde.

### 🔓 WEB PÚBLICA (Accesible sin login)
**Referencia**: Art. 13.17, Art. 13.18, Anexo II

| Documento | Obligatorio | Descripción |
|-----------|-------------|-------------|
| `SECURITY_POLICY.md` | ✅ SÍ | Política de divulgación de vulnerabilidades |
| Cómo reportar vulnerabilidades | ✅ SÍ | Email/formulario de contacto seguridad |
| Período de soporte por versión | ✅ SÍ | Fechas de fin de soporte |
| Manual de usuario (versión pública) | ✅ SÍ | Sin datos sensibles de configuración |
| Declaración UE de Conformidad | ✅ SÍ | Descargable en PDF |
| Guía de instalación segura | ✅ SÍ | Recomendaciones generales |

**URL sugerida**: `https://www.empresa.com/seguridad/` o `https://security.empresa.com`

```
🌐 WEB PÚBLICA
├── /seguridad
│   ├── politica-seguridad.html      ← SECURITY_POLICY.md
│   ├── reportar-vulnerabilidad.html ← Formulario de contacto
│   └── periodo-soporte.html         ← Tabla de versiones y fechas
├── /documentacion
│   ├── manual-usuario.pdf           ← Versión pública (sin configs)
│   ├── guia-instalacion-segura.pdf
│   └── declaracion-conformidad.pdf
└── /descargas
    └── [Actualizaciones de seguridad]
```

---

### 🔐 PORTAL CLIENTE (Acceso con login)
**Referencia**: Art. 13.18, Anexo II punto 8

Cada cliente accede **solo a su información**:

| Documento | Por qué en portal | Contenido |
|-----------|-------------------|-----------|
| SBOM de su instalación | Específico por versión | Dependencias exactas |
| Configuración de su máquina | Datos sensibles | IPs, puertos, usuarios |
| Manual técnico completo | Información detallada | Arquitectura, APIs |
| Historial de actualizaciones | Por instalación | Qué se actualizó y cuándo |
| Credenciales iniciales | **CRÍTICO** | Entrega segura única |

```
🔐 PORTAL CLIENTE (https://portal.empresa.com)
├── /mi-instalacion
│   ├── sbom.json                    ← SBOM específico
│   ├── configuracion.pdf            ← Config de SU máquina
│   └── historial-actualizaciones.md
├── /documentacion-tecnica
│   ├── manual-tecnico-completo.pdf
│   └── api-reference.pdf
└── /credenciales (acceso único)
    └── [Sistema de entrega segura]
```

---

### 🔒 SERVIDOR INTERNO (Solo empresa - NO accesible a clientes)
**Referencia**: Anexo VII (documentación técnica para autoridades)

| Documento | Por qué interno | Acceso |
|-----------|-----------------|--------|
| Evaluaciones de riesgo detalladas | Revelan vulnerabilidades potenciales | Solo equipo + autoridades |
| Configuraciones de TODOS los clientes | Datos de todos los proyectos | Solo equipo autorizado |
| Proceso interno gestión vulnerabilidades | Procedimientos internos | Solo equipo |
| Código fuente completo | Propiedad intelectual | Solo desarrollo |
| Documentación técnica completa | Para auditorías CRA | Solo si autoridad lo pide |

```
🔒 SERVIDOR INTERNO
├── /CRA_COMPLIANCE (este repositorio)
│   └── [Toda la documentación de cumplimiento]
├── /PROYECTOS
│   ├── /CLIENTE_001
│   ├── /CLIENTE_002
│   └── ...
├── /EVALUACIONES_RIESGO
│   └── [Análisis detallados]
└── /PROCESOS_INTERNOS
    ├── Gestión_Vulnerabilidades.md
    └── Proceso_CSIRT.md
```

---

## 🔑 GESTIÓN DE CREDENCIALES (Anexo I, Parte I, 2d)

### Requisitos CRA para credenciales

El CRA establece en **Anexo I, Parte I, punto 2d**:
> *"garantizar que las contraseñas sean almacenadas de forma segura y que no estén 
> codificadas de forma rígida en el código fuente"*

Y en **Anexo II, punto 8**:
> *"instrucciones para la configuración inicial segura del producto"*

### ❌ Lo que NO debemos hacer NUNCA

| Práctica Prohibida | Razón | Riesgo |
|--------------------|-------|--------|
| Misma contraseña en todas las máquinas | Un leak = todos comprometidos | 🔴 CRÍTICO |
| Enviar contraseñas por email sin cifrar | Puede ser interceptado | 🔴 CRÍTICO |
| Contraseñas en manual impreso | Cualquiera puede verlo | 🔴 ALTO |
| Contraseñas hardcodeadas en código | Viola CRA directamente | 🔴 CRÍTICO |
| Contraseñas tipo "admin/admin" | Obvio, fácil de adivinar | 🔴 CRÍTICO |

### ✅ Proceso correcto de entrega de credenciales

#### Opción A: Portal de Activación (RECOMENDADO)
```
1. Sistema genera contraseña única aleatoria
2. Cliente recibe email: "Su sistema está listo"
3. Enlace a portal seguro (HTTPS) con código único
4. Cliente ve credenciales UNA VEZ
5. Sistema marca "debe cambiar contraseña"
6. Primer login → obligatorio cambiar contraseña
```

#### Opción B: Documento Cifrado
```
1. Generar PDF cifrado con credenciales
2. Enviar PDF por email
3. Contraseña del PDF por OTRO canal (SMS, teléfono)
4. Cliente abre, apunta credenciales
5. Primer login → obligatorio cambiar
```

#### Opción C: Entrega en Persona
```
1. Durante puesta en marcha
2. Técnico configura con cliente presente
3. Cliente introduce SUS contraseñas directamente
4. No hay transmisión de credenciales
```

### 📋 Credenciales a entregar por instalación

| Sistema | Credencial | Método Entrega | Cambio Obligatorio |
|---------|------------|----------------|-------------------|
| SCADA Login | Usuario operador | Portal/Doc cifrado | ✅ Primer login |
| SCADA Login | Usuario admin | Portal/Doc cifrado | ✅ Primer login |
| Windows | Usuario operador | Doc cifrado/Presencial | ✅ Primer login |
| Windows | Usuario admin | Doc cifrado/Presencial | ✅ Primer login |
| TwinCAT | Si aplica | Presencial | ✅ Configuración |

### 🔐 Requisitos técnicos de contraseñas

| Requisito | Valor Mínimo |
|-----------|--------------|
| Longitud mínima | 12 caracteres |
| Complejidad | Mayúsculas + minúsculas + números + símbolos |
| No reutilizar | Últimas 5 contraseñas |
| Caducidad | Según política (90-180 días recomendado) |
| Bloqueo | Tras 5 intentos fallidos |

### 📄 Plantilla de entrega de credenciales

```markdown
# CREDENCIALES DE ACCESO - [NOMBRE CLIENTE]
# ⚠️ DOCUMENTO CONFIDENCIAL - DESTRUIR DESPUÉS DE USAR

Fecha entrega: [FECHA]
Instalación: [ID PROYECTO]
Entregado por: [NOMBRE TÉCNICO]

## Credenciales SCADA
- Usuario Operador: [usuario]
- Contraseña inicial: [contraseña aleatoria]
- ⚠️ CAMBIAR EN PRIMER ACCESO

- Usuario Administrador: [usuario]  
- Contraseña inicial: [contraseña aleatoria]
- ⚠️ CAMBIAR EN PRIMER ACCESO

## Credenciales Windows (si aplica)
- Usuario: [usuario]
- Contraseña inicial: [contraseña]
- ⚠️ CAMBIAR INMEDIATAMENTE

## Instrucciones
1. Acceder al sistema con las credenciales proporcionadas
2. El sistema obligará a cambiar la contraseña
3. Elegir contraseña segura (mín 12 caracteres, mayús, minús, números)
4. DESTRUIR ESTE DOCUMENTO después de cambiar contraseñas

Contacto soporte: [EMAIL/TELÉFONO]
```

El CRA exige documentar **todos los componentes de terceros** integrados en el producto.
Cada fabricante es responsable de su propia conformidad CRA, pero **nosotros debemos**:

1. **Listar** todos los componentes (incluido en SBOM)
2. **Referenciar** su documentación de seguridad oficial
3. **Documentar** nuestra configuración específica
4. **Conservar** copia de las guías usadas durante el desarrollo

### 📋 Responsabilidades

| Componente | Fabricante | Su Responsabilidad | Nuestra Responsabilidad |
|------------|------------|-------------------|------------------------|
| TwinCAT 3 Runtime | Beckhoff | Declaración CRA propia | Configuración segura, SBOM |
| Windows 10/11 IoT | Microsoft | Conformidad CRA propia | Hardening, actualizaciones |
| IPC Industrial | Beckhoff | Hardware + BIOS seguro | Configuración, documentación |
| .NET Runtime | Microsoft | Seguridad del runtime | Actualizaciones, SBOM |
| React/Node.js | Meta/OpenJS | Comunidad open source | SBOM, auditoría deps |

---

### 🔧 BECKHOFF - Documentación Requerida

**Fuente oficial**: https://infosys.beckhoff.com/content/1033/ipc_security/

| Documento | Versión | Ubicación Local | Descripción |
|-----------|---------|-----------------|-------------|
| IPC Security Guideline Win11 | 2024 | `TERCEROS/BECKHOFF/` | Hardening Windows 11 en IPC Beckhoff |
| TwinCAT 3 Security | 2024 | `TERCEROS/BECKHOFF/` | Seguridad del runtime TwinCAT |
| ADS Security | 2024 | `TERCEROS/BECKHOFF/` | Configuración segura de comunicación ADS |

**Lo que debemos documentar nosotros** (`Nuestra_Configuracion_Beckhoff.md`):

```markdown
## Configuración de Seguridad Beckhoff - Nuestra Implementación

### 1. Windows 11 IoT Enterprise
- Versión: Windows 11 IoT Enterprise LTSC 2024
- Hardening aplicado según: IPC_Security_Guideline_Win11_en.pdf
- Configuraciones específicas:
  - [ ] Windows Firewall habilitado
  - [ ] BitLocker activado
  - [ ] Secure Boot habilitado
  - [ ] Usuario administrador deshabilitado
  - [ ] Actualizaciones automáticas configuradas

### 2. TwinCAT 3 Runtime
- Versión: 3.1.4024.xx
- Configuración de seguridad:
  - [ ] ADS sobre TLS configurado
  - [ ] Acceso ADS restringido por IP
  - [ ] Usuarios TwinCAT con permisos mínimos

### 3. Comunicación ADS
- Puerto: 48898 (TCP)
- Restricciones de acceso: Solo localhost + IP del SCADA
- Cifrado: ADS over TLS (si disponible)
```

---

### 🪟 MICROSOFT - Documentación Requerida

**Fuentes oficiales**:
- https://docs.microsoft.com/security/
- https://www.microsoft.com/en-us/security/business/security-101/what-is-windows-security

| Documento | Ubicación | Descripción |
|-----------|-----------|-------------|
| Windows Security Baseline | `TERCEROS/MICROSOFT/` | Configuración base de seguridad |
| .NET Security Guidelines | `TERCEROS/MICROSOFT/` | Desarrollo seguro en .NET |

**Lo que debemos documentar nosotros** (`Nuestra_Configuracion_Windows.md`):

```markdown
## Configuración de Seguridad Windows - Nuestra Implementación

### 1. Sistema Operativo
- Versión: Windows 11 IoT Enterprise LTSC 2024
- Actualizaciones: WSUS interno / Windows Update

### 2. Hardening Aplicado
- [ ] Firewall configurado (solo puertos necesarios)
- [ ] Antivirus/Windows Defender activo
- [ ] UAC habilitado
- [ ] Políticas de contraseñas
- [ ] Auditoría de eventos habilitada

### 3. Servicios Deshabilitados
- Remote Desktop (si no necesario)
- Telnet
- FTP
- etc.
```

---

## ⚠️ SANCIONES POR INCUMPLIMIENTO

| Infracción | Multa Máxima |
|------------|--------------|
| Requisitos esenciales (Anexo I) + Art. 13/14 | **15M€ o 2.5% facturación mundial** |
| Otras obligaciones | **10M€ o 2% facturación mundial** |
| Información incorrecta a autoridades | **5M€ o 1% facturación mundial** |

---

## 📞 CONTACTOS ÚTILES

| Entidad | Función | Contacto |
|---------|---------|----------|
| **INCIBE-CERT** | CSIRT nacional España | incidencias@incibe-cert.es |
| **CCN-CERT** | CSIRT sector público | ccn-cert@cni.es |
| **ENISA** | Agencia UE Ciberseguridad | info@enisa.europa.eu |
| **AEPD** | Protección de datos | ciudadano@aepd.es |

---

## 📝 HISTORIAL DE CAMBIOS

| Versión | Fecha | Cambios |
|---------|-------|---------|
| 1.0 | Dic 2025 | Documento inicial |
| 1.1 | Dic 2025 | Añadida sección TERCEROS (Beckhoff, Microsoft) |
| 1.2 | Dic 2025 | Añadida distribución Pública/Portal/Interna + Gestión de credenciales |
| 1.3 | Dic 2025 | Reorganizada estructura carpetas con PUBLICA/PORTAL_CLIENTE/INTERNO |
| 1.4 | Dic 2025 | Añadida estructura MAQUINAS/ + relación Directiva Máquinas y CRA |
| 1.5 | Dic 2025 | Movido VERSIONES/ dentro de INTERNO/ (SBOMs son confidenciales) |

---

**Documento preparado para cumplimiento con Reglamento (UE) 2024/2847**  
**Cyber Resilience Act - Sistema SCADA/HMI Industrial**
