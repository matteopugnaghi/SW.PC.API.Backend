@echo off
:: Toggle USB Storage - Bloquear/Desbloquear automáticamente
:: Se ejecuta como CustomTool2 desde SystemToolsMenu
:: Requiere privilegios de administrador (el servicio AqfSupervisor los tiene)
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0Toggle-UsbStorage.ps1"
