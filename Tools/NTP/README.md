# Configure-NTP — NTP Configuration Tool

Configures Windows Time Service (W32Time) on industrial IPCs.  
Supports 4 languages: Spanish, English, French, Italian.

---

## Files

| File | Description |
|------|-------------|
| `Configure-NTP.ps1` | Main script (do not edit) |
| `Configure-NTP-Launcher.ps1` | Interactive menu — asks all parameters step by step |
| `Lanzar-NTP.bat` | Generic launcher — opens the interactive menu |
| `Lanzar-NTP-Client.bat` | A72.TOUTWP — CLIENT preconfigurated |
| `Lanzar-NTP-Server.bat` | A72.TOUTWP — SERVER preconfigurated |

---

## A72.TOUTWP — Quick Start

### 1. Configure CLIENT (MAL-IPC-CLIENT)

1. RDP to CLIENT (192.168.2.163 dev / 10.11.100.121 prod)
2. Copy this entire `NTP/` folder to the PC
3. Double-click **`Lanzar-NTP-Client.bat`** (auto-elevates to Admin)
4. Done — CLIENT syncs from FortiGate (10.11.100.122) and relays to SERVER

### 2. Configure SERVER (MAL-IPC-SERVER)

1. From CLIENT, RDP to SERVER (192.168.1.161)
2. Copy the same `NTP/` folder to the SERVER
3. Double-click **`Lanzar-NTP-Server.bat`** (auto-elevates to Admin)
4. Done — SERVER syncs from CLIENT (192.168.1.162)

### NTP Chain

```
CSP NTP (10.8.80.1) → FortiGate (10.11.100.122) → CLIENT → SERVER
```

---

## Generic — Any Project

### Option A: Interactive Menu (recommended)

1. Copy this `NTP/` folder to the target PC
2. Double-click **`Lanzar-NTP.bat`**
3. Follow the menu:
   - Select language (SPA/ENG/FRA/ITA)
   - Select role: **Client** or **Server**
   - Select mode: **DryRun** (test), **Real** (apply), or **Rollback** (undo)
   - Enter NTP server IP (the time source)
   - Enter fallback NTP server (optional, press Enter to skip)
   - Enter poll interval in seconds (default: 900 = 15 min)
   - Remote execution? (yes = enter target PC IP + credentials)
   - Confirm and execute

### Option B: Direct Command

Open PowerShell **as Administrator** and run:

```powershell
# CLIENT — syncs from external NTP, relays time to SERVER
.\Configure-NTP.ps1 -Role Client -NtpServer "10.11.100.122" -PollIntervalSeconds 900 -Language FRA

# SERVER — syncs from CLIENT only
.\Configure-NTP.ps1 -Role Server -NtpServer "192.168.1.162" -PollIntervalSeconds 900 -Language FRA

# DryRun — test without changes
.\Configure-NTP.ps1 -Role Client -NtpServer "10.11.100.122" -DryRun -Language ENG

# Rollback — undo last configuration
.\Configure-NTP.ps1 -Role Client -Rollback -Language ENG

# Remote — configure another PC via WinRM
.\Configure-NTP.ps1 -Role Server -NtpServer "192.168.1.162" -Language FRA -ComputerName 192.168.1.161
```

---

## Roles Explained

| Role | What it does |
|------|-------------|
| **Client** | Syncs from external NTP server + enables NTP Server provider (relay for other PCs) |
| **Server** | Syncs from Client IPC only (leaf node, does not relay) |

---

## Modes

| Mode | Description |
|------|-------------|
| **DryRun** | Shows what would change, no modifications |
| **Real** | Applies configuration (saves rollback file automatically) |
| **Rollback** | Restores previous configuration from saved rollback file |

---

## Requirements

- Windows 10/11 or Windows Server
- PowerShell 5.1+
- Administrator privileges
- For remote: WinRM enabled on target PC
