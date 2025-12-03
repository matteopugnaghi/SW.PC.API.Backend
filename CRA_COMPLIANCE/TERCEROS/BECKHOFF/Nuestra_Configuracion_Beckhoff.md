# 🔧 Nuestra Configuración de Seguridad - Beckhoff

**Proyecto**: Sistema SCADA/HMI Industrial  
**Última actualización**: [ RELLENAR FECHA ]  
**Responsable**: [ RELLENAR NOMBRE ]

---

## 📋 Resumen de Configuración

Este documento describe **nuestra configuración específica** de los componentes Beckhoff, siguiendo las guías oficiales de seguridad.

---

## 1. 🖥️ IPC Industrial

### Modelo y Especificaciones
| Campo | Valor |
|-------|-------|
| Modelo IPC | [ CX-xxxx / C6xxx ] |
| CPU | [ Intel Core i5/i7 ] |
| RAM | [ 8GB / 16GB ] |
| Almacenamiento | [ SSD 256GB ] |
| Sistema Operativo | Windows 11 IoT Enterprise LTSC 2024 |

### BIOS/UEFI
- [ ] Secure Boot: **HABILITADO**
- [ ] TPM 2.0: **HABILITADO**
- [ ] Boot desde USB: **DESHABILITADO**
- [ ] Password BIOS: **CONFIGURADO**

---

## 2. 🪟 Windows 11 IoT Enterprise

### Configuración Base
Siguiendo: `IPC_Security_Guideline_Win11_en.pdf`

| Configuración | Estado | Notas |
|---------------|--------|-------|
| Windows Firewall | ✅ Habilitado | Reglas personalizadas |
| BitLocker | ✅ Habilitado | Cifrado completo |
| Windows Defender | ✅ Activo | Actualizaciones automáticas |
| UAC | ✅ Habilitado | Nivel por defecto |
| Actualizaciones | ✅ Configurado | WSUS / Manual |

### Usuarios Configurados
| Usuario | Tipo | Propósito |
|---------|------|-----------|
| Administrador | Deshabilitado | - |
| SCADAOperator | Estándar | Operación diaria |
| SCADAAdmin | Administrador | Mantenimiento |
| TcUser | Servicio | TwinCAT Runtime |

### Servicios Deshabilitados
- [ ] Remote Desktop (si no necesario)
- [ ] Telnet
- [ ] FTP Server
- [ ] SNMP (si no necesario)
- [ ] [ OTROS ]

### Puertos de Firewall Abiertos
| Puerto | Protocolo | Servicio | Dirección |
|--------|-----------|----------|-----------|
| 5000 | TCP | API Backend | Entrada |
| 48898 | TCP | TwinCAT ADS | Local only |
| 443 | TCP | HTTPS | Salida |
| [ ] | [ ] | [ ] | [ ] |

---

## 3. ⚙️ TwinCAT 3 Runtime

### Versión Instalada
| Campo | Valor |
|-------|-------|
| TwinCAT Version | 3.1.4024.[ BUILD ] |
| XAE Version | [ SI APLICA ] |
| Fecha instalación | [ FECHA ] |

### Configuración ADS
| Configuración | Valor |
|---------------|-------|
| AMS Net ID | [ 5.x.x.x.1.1 ] |
| Puerto ADS | 48898 |
| Acceso remoto ADS | ❌ Deshabilitado |
| IPs permitidas | 127.0.0.1, [ IP SCADA ] |

### Usuarios TwinCAT
| Usuario | Rol | Permisos |
|---------|-----|----------|
| [ USUARIO ] | [ ROL ] | [ PERMISOS ] |

### Proyectos PLC
| Proyecto | Versión | Última modificación |
|----------|---------|---------------------|
| [ NOMBRE ] | [ v1.x ] | [ FECHA ] |

---

## 4. 🔒 Medidas de Seguridad Adicionales

### Red
- [ ] Red OT separada de red IT
- [ ] VLAN dedicada para control
- [ ] Sin acceso directo a Internet
- [ ] VPN para acceso remoto (si necesario)

### Físico
- [ ] IPC en armario cerrado
- [ ] Acceso físico restringido
- [ ] Puertos USB deshabilitados/bloqueados

### Backup
- [ ] Backup de proyecto TwinCAT: [ FRECUENCIA ]
- [ ] Backup de configuración Windows: [ FRECUENCIA ]
- [ ] Ubicación backups: [ UBICACIÓN ]

---

## 5. 📅 Plan de Actualizaciones

| Componente | Frecuencia | Responsable | Procedimiento |
|------------|------------|-------------|---------------|
| Windows Updates | Mensual | [ NOMBRE ] | Testear en lab primero |
| TwinCAT Runtime | Según CVE | [ NOMBRE ] | Ventana de mantenimiento |
| Antivirus | Automático | Sistema | - |

---

## 6. ✅ Checklist de Verificación

### Instalación Inicial
- [ ] BIOS configurado según guía
- [ ] Windows hardening aplicado
- [ ] TwinCAT instalado y configurado
- [ ] Firewall configurado
- [ ] Usuarios creados
- [ ] Backup inicial realizado
- [ ] Documentación completada

### Verificación Periódica (Trimestral)
- [ ] Revisar logs de seguridad Windows
- [ ] Verificar actualizaciones pendientes
- [ ] Comprobar estado de backups
- [ ] Revisar accesos de usuarios
- [ ] Verificar configuración de firewall

---

## 7. 📝 Historial de Cambios

| Fecha | Cambio | Responsable |
|-------|--------|-------------|
| [ FECHA ] | Configuración inicial | [ NOMBRE ] |
| [ ] | [ ] | [ ] |

---

## 8. 🔗 Referencias

- Guía de seguridad Beckhoff: `../IPC_Security_Guideline_Win11_en.pdf`
- Documentación TwinCAT: https://infosys.beckhoff.com
- Contacto seguridad: security@beckhoff.com
