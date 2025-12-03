# 🔧 Beckhoff - Documentación de Seguridad

**Fabricante**: Beckhoff Automation GmbH  
**Web**: https://www.beckhoff.com  
**Documentación de seguridad**: https://infosys.beckhoff.com/content/1033/ipc_security/

---

## 📋 Componentes Beckhoff Utilizados

| Componente | Versión | Función | Conformidad CRA |
|------------|---------|---------|-----------------|
| TwinCAT 3 Runtime | 3.1.4024.x | PLC Software | Responsabilidad Beckhoff |
| IPC Industrial | CX-xxxx / C6xxx | Hardware de control | Responsabilidad Beckhoff |
| TwinCAT ADS | 3.x | Comunicación PLC | Responsabilidad Beckhoff |

---

## 📚 Documentos de Referencia

### Obligatorios (guardar copia local)

| Documento | Versión | Fecha Descarga | Archivo Local |
|-----------|---------|----------------|---------------|
| IPC Security Guideline Windows 11 | 2024 | [ RELLENAR ] | `IPC_Security_Guideline_Win11_en.pdf` |
| TwinCAT 3 Security | 2024 | [ RELLENAR ] | `TwinCAT_Security_Hardening.pdf` |
| ADS Security Configuration | 2024 | [ RELLENAR ] | `ADS_Security_Config.pdf` |

### Fuentes Online

- **IPC Security General**: https://infosys.beckhoff.com/content/1033/ipc_security/
- **TwinCAT Security**: https://infosys.beckhoff.com/content/1033/tc3_security/
- **ADS Protocol**: https://infosys.beckhoff.com/content/1033/tc3_ads_intro/

---

## 🔐 Guía IPC Security - Puntos Clave

Según `IPC_Security_Guideline_Win11_en.pdf`:

### 1. Hardening del Sistema Operativo
- [ ] Deshabilitar servicios innecesarios
- [ ] Configurar Windows Firewall
- [ ] Habilitar BitLocker (cifrado de disco)
- [ ] Configurar Secure Boot
- [ ] Deshabilitar usuario Administrator por defecto
- [ ] Política de contraseñas robusta

### 2. Configuración de Red
- [ ] Segmentar red de control (OT) de red IT
- [ ] Firewall entre segmentos
- [ ] Solo puertos necesarios abiertos
- [ ] VPN para acceso remoto

### 3. TwinCAT Runtime
- [ ] Actualizar a última versión estable
- [ ] Configurar usuarios TwinCAT
- [ ] Restringir acceso ADS por IP
- [ ] Considerar ADS over TLS (si disponible)

### 4. Actualizaciones
- [ ] Plan de actualizaciones Windows
- [ ] Plan de actualizaciones TwinCAT
- [ ] Testear actualizaciones antes de producción

---

## 📝 Nuestra Configuración

**Ver archivo**: `Nuestra_Configuracion_Beckhoff.md`

Este archivo documenta específicamente cómo hemos configurado los componentes Beckhoff en nuestra instalación, siguiendo las guías oficiales.

---

## 🔗 Contacto Seguridad Beckhoff

Para reportar vulnerabilidades en productos Beckhoff:

- **Email**: security@beckhoff.com
- **Web**: https://www.beckhoff.com/security

---

## ⚠️ Notas Importantes

1. **Beckhoff es responsable** de la conformidad CRA de sus productos
2. **Nosotros somos responsables** de:
   - Configurar correctamente siguiendo sus guías
   - Mantener actualizado el software
   - Documentar nuestra configuración específica
3. **Conservar** versiones de documentación usadas durante desarrollo
4. **Revisar** periódicamente actualizaciones de seguridad de Beckhoff
