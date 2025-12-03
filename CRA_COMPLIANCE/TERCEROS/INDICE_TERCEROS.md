# 📦 Índice de Componentes de Terceros

**Última actualización**: Diciembre 2025  
**Referencia CRA**: Anexo VII, punto 2; Anexo I, Parte II, punto 1 (SBOM)

---

## 📋 Resumen de Responsabilidades

El Reglamento (UE) 2024/2847 (CRA) establece que:

1. **Cada fabricante** es responsable de la conformidad de sus propios productos
2. **El integrador** (nosotros) debe documentar qué componentes usa y cómo los configura
3. **El SBOM** debe incluir todos los componentes de terceros

---

## 🏭 Componentes Integrados

| ID | Componente | Fabricante | Versión | Tipo | CRA Responsable |
|----|------------|------------|---------|------|-----------------|
| T01 | TwinCAT 3 Runtime | Beckhoff Automation GmbH | 3.1.4024.x | Software PLC | Beckhoff |
| T02 | Windows 11 IoT Enterprise | Microsoft Corporation | LTSC 2024 | Sistema Operativo | Microsoft |
| T03 | IPC Industrial (CX/C6) | Beckhoff Automation GmbH | Varios | Hardware | Beckhoff |
| T04 | .NET Runtime | Microsoft Corporation | 8.0 | Runtime | Microsoft |
| T05 | ASP.NET Core | Microsoft Corporation | 8.0 | Framework | Microsoft |
| T06 | React | Meta Platforms | 19.x | Framework Frontend | Open Source |
| T07 | Babylon.js | Babylon.js Team | 8.x | Motor 3D | Open Source |
| T08 | SignalR | Microsoft Corporation | 8.0 | Comunicación RT | Microsoft |

---

## 📁 Estructura de Documentación

```
TERCEROS/
├── INDICE_TERCEROS.md          ← Este archivo
├── BECKHOFF/
│   ├── README_BECKHOFF.md      ← Referencias oficiales
│   ├── IPC_Security_Guideline_Win11_en.pdf
│   ├── TwinCAT_Security_Hardening.pdf
│   └── Nuestra_Configuracion_Beckhoff.md
├── MICROSOFT/
│   ├── README_MICROSOFT.md
│   └── Nuestra_Configuracion_Windows.md
└── OTROS/
    └── README_OTROS.md
```

---

## ✅ Checklist de Documentación por Tercero

### Beckhoff
- [ ] Copia de IPC Security Guideline (versión usada)
- [ ] Copia de TwinCAT Security docs (versión usada)
- [ ] Nuestra configuración documentada
- [ ] Versiones específicas registradas

### Microsoft  
- [ ] Windows Security Baseline aplicado
- [ ] .NET Security Guidelines revisadas
- [ ] Nuestra configuración documentada
- [ ] Versiones específicas registradas

### Open Source (React, Babylon.js, etc.)
- [ ] Incluido en SBOM automáticamente
- [ ] Licencias verificadas (MIT, Apache, etc.)
- [ ] Vulnerabilidades conocidas revisadas (npm audit)

---

## 🔗 Enlaces a Documentación Oficial

| Fabricante | URL Documentación Seguridad |
|------------|----------------------------|
| Beckhoff | https://infosys.beckhoff.com/content/1033/ipc_security/ |
| Microsoft | https://docs.microsoft.com/security/ |
| React | https://reactjs.org/docs/security.html |
| Babylon.js | https://doc.babylonjs.com/ |

---

## ⚠️ Importante

- **Conservar** las versiones de documentación usadas durante el desarrollo
- **Actualizar** este índice cuando se añadan nuevos componentes
- **Verificar** periódicamente actualizaciones de seguridad de terceros
- **El SBOM** se genera automáticamente e incluye dependencias transitivas
