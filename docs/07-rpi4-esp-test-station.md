# RPi ESP/Nordic Firmware Test Station

> **See also:**
> - [Test Station Hardware](06-test-station-hardware.md) — per-board emulation status and how this station fits the validation workflow
> - [Development Methodology](04-development-methodology.md) — JTAG/OpenOCD usage on the ESP32-C3
> - [Document Index](README.md)

---

A firmware development and testing station using a Raspberry Pi 4 as the USB host for
Espressif (ESP32) and Nordic Semiconductor (nRF) development boards connected via a
USB hub. The RPi provides stable serial device paths, USB power cycling for device
reset, and remote SSH access for automated firmware flashing and testing.

## Hardware Overview

```
                    Genesys Logic USB 2.0 Hub (05e3:0610, ganged power)
                    ├── Port 1: Realtek RTL8188CUS WiFi adapter (wlanE)
   ┌────────────────┤
   │                ├── Port 2: Nordic nRF52840 dongle (PCA10059)   ──→  /dev/ttyNRF52840
   │                ├── Port 3: CH340 → ESP32-CAM-MB (D0WD-V3)     ──→  /dev/ttyESP32CAM
   │                └── Port 4: ESP32-C3 USB JTAG/serial            ──→  /dev/ttyESP32C3
   │
RPi 4 (rpi4-esp)
   │    VIA Labs USB 2.0 Hub (2109:3431, ppps)
   │    └── Port 4: CP2102 → ESP32 DevKit (D0WDQ6)                 ──→  /dev/ttyESP32DEV
   │
   ├── eth0 ──→ GSM7252PS-S1 port 1/0/27 (PoE powered, VLAN 90)
   └── wlan0 ──→ ansells-iot WiFi (VLAN 90)
```

- **RPi 4** (`rpi4-esp`): USB host, firmware flash/test controller, PoE powered
- **ESP32-C3 board**: ESP32-C3 (QFN32, rev v0.4) with built-in USB-JTAG/serial, 4MB flash (XMC), MAC `e8:3d:c1:8c:5c:ac`
- **ESP32-CAM-MB**: ESP32-D0WD-V3 (rev v3.1), dual-core 240MHz, 4MB flash, MAC `a4:f0:0f:76:46:64`, via CH340
- **ESP32 DevKit**: ESP32-D0WDQ6 (rev v1.0), dual-core, 4MB flash, MAC `24:0a:c4:11:44:e8`, via CP2102
- **Nordic nRF52840 dongle** (PCA10059): currently in Open DFU Bootloader mode, FICR ID `DADB34875764`
- **Genesys Logic hub**: 4-port USB 2.0 hub — **ganged power switching** (all ports share power)
- **Realtek RTL8188CUS**: USB WiFi adapter (wlanE) — unmanaged, for hostapd AP or ESP client use

## Network Information

### rpi4-esp (Raspberry Pi 4)

| Property | Value |
|----------|-------|
| Hostname | `rpi4-esp` |
| Model | Raspberry Pi 4 Model B Rev 1.4 |
| RPi Serial | `1000000053f279e5` |
| OS | Debian GNU/Linux 13 (trixie) |
| Kernel | `6.12.47+rpt-rpi-v8` aarch64 |
| CPU | BCM2711 (Cortex-A72), 4 cores |
| RAM | 8 GB |
| Storage | 32 GB SD card (`/dev/mmcblk0`) |
| eth0 MAC | `dc:a6:32:b4:5f:29` |
| eth0 IPv4 | `10.1.90.206` (static DHCP) |
| eth0 IPv6 | `2404:e80:a137:190::206` (DUID-LL) |
| wlan0 MAC | `dc:a6:32:b4:5f:2a` (onboard BCM43455) |
| wlan0 IPv4 | `10.1.90.207` (static DHCP, ansells-iot) |
| wlan0 IPv6 | `2404:e80:a137:190::207` (DUID-LL) |
| wlanE MAC | `64:70:02:0c:68:02` (RTL8188CUS USB, unmanaged — for AP/client) |
| DNS (eth0) | `ipv4.eth0.rpi4-esp.iot.welland.mithis.com` |
| DNS (wlan0) | `ipv4.wlan0.rpi4-esp.iot.welland.mithis.com` |
| PoE Switch | GSM7252PS-S1 (`10.1.5.22`) |
| Switch Port | `1/0/27` (VLAN 90, PoE) |
| SSH | `ssh tim@ipv4.eth0.rpi4-esp.iot.welland.mithis.com` |

**WiFi interfaces**:
- **wlan0** (`dc:a6:32:b4:5f:2a`): RPi4 onboard BCM43455 (brcmfmac) — connects to
  ansells-iot WiFi (VLAN 90), managed by NetworkManager
- **wlanE** (`64:70:02:0c:68:02`): Realtek RTL8188CUS USB dongle on Genesys hub —
  **unmanaged by NetworkManager**, reserved for hostapd AP or ESP/Nordic client use

Interface names are stabilised by systemd .link files matching on MAC address:
- `/etc/systemd/network/10-wlan0-onboard.link` → onboard BCM43455 = `wlan0`
- `/etc/systemd/network/10-wlanE-usb-rtl8188.link` → USB RTL8188CUS = `wlanE`

**Note**: DNS entries need to be added to the gdoc2netcfg Google Sheet for static
DHCP/DNS assignment.

## USB Device Inventory

### Complete MAC/Serial Reference

| Device | Chip | WiFi MAC | USB Serial | VID:PID | Driver | Hub Path | Symlink |
|--------|------|----------|------------|---------|--------|----------|---------|
| ESP32-C3 board | ESP32-C3 (QFN32) rev v0.4 | `e8:3d:c1:8c:5c:ac` | `E8:3D:C1:8C:5C:AC` | `303a:1001` | `cdc_acm` | `1-1.2.4` | `/dev/ttyESP32C3` |
| ESP32-CAM-MB | ESP32-D0WD-V3 rev v3.1 | `a4:f0:0f:76:46:64` | *(none, CH340)* | `1a86:7523` | `ch341` | `1-1.2.3` | `/dev/ttyESP32CAM` |
| ESP32 DevKit | ESP32-D0WDQ6 rev v1.0 | `24:0a:c4:11:44:e8` | `0001` (CP2102) | `10c4:ea60` | `cp210x` | `1-1.4` | `/dev/ttyESP32DEV` |
| Nordic nRF52840 dongle | nRF52840 (PCA10059) | — | `DADB34875764` | `1915:521f` | `cdc_acm` | `1-1.2.2` | `/dev/ttyNRF52840` |
| Realtek WiFi adapter | RTL8188CUS | — | `00e04c000001` | `0bda:8176` | `rtl8192cu` | `1-1.2.1` | — (wlanE) |

### WiFi Soft-AP Networks (from connected ESP boards)

These WiFi networks are broadcast by the ESP32 boards connected to this station:

| SSID | BSSID | Base MAC | Board |
|------|-------|----------|-------|
| `ESP_1144E8` | `26:0A:C4:11:44:E8` | `24:0a:c4:11:44:e8` | ESP32 DevKit (CP2102) |
| `ESP32-CAM-MB` | `A4:F0:0F:76:46:65` | `a4:f0:0f:76:46:64` | ESP32-CAM-MB (CH340) |

Note: ESP32 WiFi soft-AP MAC = base MAC + 1.

### USB Topology

```
Bus 001 (USB 2.0, xhci_hcd, 480Mbps):
└── Port 1: VIA Labs Hub (2109:3431), 4-port, per-port power switching (ppps)
    ├── Port 2: Genesys Logic Hub (05e3:0610), 4-port, ganged power
    │   ├── Port 1: Realtek RTL8188CUS WiFi (0bda:8176), 480Mbps → wlanE
    │   ├── Port 2: Nordic nRF dongle (1915:521f), 12Mbps → /dev/ttyACM0
    │   ├── Port 3: CH340 serial (1a86:7523), 12Mbps → /dev/ttyUSB1
    │   └── Port 4: Espressif ESP32 (303a:1001), 12Mbps → /dev/ttyACM1
    └── Port 4: CP2102 (10c4:ea60), 12Mbps → /dev/ttyUSB0

Bus 002 (USB 3.0, xhci_hcd, 5Gbps):
└── Port 2: Genesys Logic Hub (05e3:0616), 4-port, 5Gbps (empty)
```

### ESP32-C3 Board (built-in USB)

| Property | Value |
|----------|-------|
| Chip | **ESP32-C3** (QFN32), revision v0.4 |
| Features | Wi-Fi, BT 5 (LE), Single Core, 160MHz |
| Flash | 4MB embedded (XMC), manufacturer ID `0x46` |
| Crystal | 40 MHz |
| WiFi MAC | `e8:3d:c1:8c:5c:ac` |
| USB mode | USB-Serial/JTAG (built-in) |
| USB VID:PID | `303a:1001` |
| USB Serial | `E8:3D:C1:8C:5C:AC` |
| USB Speed | Full Speed (12 Mbps) |
| Power | Self Powered, 500mA max |
| Interface 0-1 | CDC ACM (serial port) → `/dev/ttyACM1` → `/dev/ttyESP32C3` |
| Interface 2 | Vendor Specific (JTAG) — used by OpenOCD/esptool |

The ESP32-C3 has a built-in USB-JTAG/serial interface providing both a serial
console (for logging/flashing via esptool) and a JTAG debug interface (for OpenOCD).
No external USB-UART bridge needed.

```bash
# Flash firmware via esptool
esptool.py --port /dev/ttyESP32C3 --baud 460800 write_flash 0x0 firmware.bin

# Monitor serial output
python3 -m serial.tools.miniterm /dev/ttyESP32C3 115200

# Use with ESP-IDF
idf.py -p /dev/ttyESP32C3 flash monitor
```

### Nordic nRF52840 Dongle (PCA10059)

| Property | Value |
|----------|-------|
| Chip | **nRF52840** (ARM Cortex-M4F) |
| Board | PCA10059 USB dongle |
| Features | BLE, Thread, Zigbee, 802.15.4, USB |
| FICR Device ID | `DADB34875764` |
| USB VID:PID | `1915:521f` (Open DFU Bootloader) |
| USB Speed | Full Speed (12 Mbps) |
| Power | Self Powered, 500mA max |
| Interface 0-1 | CDC ACM (serial port) → `/dev/ttyACM0` → `/dev/ttyNRF52840` |
| Status | **DFU bootloader mode** — no application firmware loaded |

The device is currently in **DFU bootloader mode** (`1915:521f`). Once application
firmware is flashed, the VID:PID will change (commonly to `1915:cafe` for nRF
Connect SDK applications, or a custom PID).

The udev rules match both DFU mode (`1915:521f`) and application mode (`1915:cafe`)
to maintain the `/dev/ttyNRF52840` symlink across firmware changes.

```bash
# Flash firmware via nrfutil (nRF Connect SDK)
nrfutil device program --firmware app_signed.hex --serial-number DADB34875764

# Flash via nrfdfu (Open DFU Bootloader)
nrfdfu /dev/ttyNRF52840 firmware.zip

# Monitor serial output
python3 -m serial.tools.miniterm /dev/ttyNRF52840 115200
```

### ESP32-CAM-MB (via CH340)

| Property | Value |
|----------|-------|
| Chip | **ESP32-D0WD-V3** (revision v3.1) |
| Features | Wi-Fi, BT, Dual Core + LP Core, 240MHz, Vref calibration in eFuse |
| Flash | 4MB, manufacturer ID `0x68`, 3.3V (strapping pin) |
| Crystal | 40 MHz |
| WiFi MAC | `a4:f0:0f:76:46:64` |
| Soft-AP SSID | `ESP32-CAM-MB` (BSSID `A4:F0:0F:76:46:65`) |
| USB bridge | CH340 (QinHeng `1a86:7523`), no serial number |
| USB Speed | Full Speed (12 Mbps) |
| Power | Bus Powered, 98mA |
| Driver | `ch341` |
| Device | `/dev/ttyUSB1` → `/dev/ttyESP32CAM` |

ESP32-CAM module on a CH340-based programmer board. The CH340 has no USB serial
number, so the udev rule uses the USB bus path (`1-1.2.3`, Genesys hub port 3)
for stable identification. If the CH340 is moved to a different hub port, the
udev rule must be updated.

```bash
# Flash ESP32-CAM firmware
/opt/esptool/bin/esptool --port /dev/ttyESP32CAM --baud 460800 write_flash 0x0 firmware.bin

# Monitor serial output
python3 -m serial.tools.miniterm /dev/ttyESP32CAM 115200
```

### ESP32 DevKit (via CP2102)

| Property | Value |
|----------|-------|
| Chip | **ESP32-D0WDQ6** (revision v1.0) |
| Features | Wi-Fi, BT, Dual Core + LP Core |
| Flash | 4MB, manufacturer ID `0xc8` (GigaDevice), 3.3V (strapping pin) |
| Crystal | 40 MHz |
| WiFi MAC | `24:0a:c4:11:44:e8` |
| Soft-AP SSID | `ESP_1144E8` (BSSID `26:0A:C4:11:44:E8`) |
| USB bridge | CP2102 (Silicon Labs `10c4:ea60`), serial `0001` |
| USB Speed | Full Speed (12 Mbps) |
| Power | Bus Powered, 100mA |
| Driver | `cp210x` |
| Device | `/dev/ttyUSB0` → `/dev/ttyESP32DEV` |

ESP32 DevKit board with CP2102 USB-UART bridge. On the outer VIA Labs hub
(port 4), not through the Genesys hub — this board is on a **separate power
domain** and can be power-cycled independently.

```bash
# Flash firmware
/opt/esptool/bin/esptool --port /dev/ttyESP32DEV --baud 460800 write_flash 0x0 firmware.bin

# Monitor serial output
python3 -m serial.tools.miniterm /dev/ttyESP32DEV 115200
```

## WiFi Adapter (RTL8188CUS) — AP / Client for Dev Boards

The USB WiFi adapter (wlanE) is removed from NetworkManager control and can be
used either as a **wireless access point** for ESP32/Nordic devices to connect to,
or as a **client** to connect to ESP32 soft-APs.

| Property | Value |
|----------|-------|
| Interface | `wlanE` |
| Phy | `phy#1` |
| MAC | `64:70:02:0c:68:02` |
| Chip | Realtek RTL8188CUS |
| Driver | `rtl8192cu` |
| USB VID:PID | `0bda:8176` |
| Hub path | `1-1.2.1` (Genesys hub port 1) |
| Supported modes | IBSS, managed, **AP**, AP/VLAN, monitor, mesh, P2P |
| Band | 2.4 GHz only (802.11b/g/n, HT20) |
| NM status | **unmanaged** (`/etc/NetworkManager/conf.d/99-unmanage-usb-wifi.conf`) |
| .link file | `/etc/systemd/network/10-wlanE-usb-rtl8188.link` |

### Using as Access Point (hostapd)

hostapd is installed (masked by default). To start an AP for ESP32/Nordic devices:

```bash
# Unmask and enable hostapd
sudo systemctl unmask hostapd
sudo systemctl enable hostapd

# Configure hostapd (example: open AP on channel 6)
sudo tee /etc/hostapd/hostapd.conf <<'EOF'
interface=wlanE
driver=nl80211
ssid=esp-test
hw_mode=g
channel=6
wmm_enabled=0
auth_algs=1
wpa=0
EOF

# Set static IP on wlanE
sudo ip addr add 192.168.4.1/24 dev wlanE
sudo ip link set wlanE up

# Start hostapd
sudo systemctl start hostapd
```

For DHCP on the AP network, install and configure dnsmasq or use a static IP on
each ESP32 device.

### Using as Client (connect to ESP soft-AP)

```bash
# Connect to an ESP32 soft-AP
sudo wpa_supplicant -B -i wlanE -c <(wpa_passphrase "ESP_1144E8" "")
sudo ip addr add 192.168.4.100/24 dev wlanE

# Or for open networks:
sudo /usr/sbin/iw dev wlanE connect "ESP32-CAM-MB"
sudo ip addr add 192.168.4.100/24 dev wlanE
```

## udev Rules

Stable device symlinks are configured in `/etc/udev/rules.d/99-esp-nordic.rules`.
These symlinks persist across device disconnects and reconnects, and across host
reboots.

| Symlink | Matches on | Chip |
|---------|-----------|------|
| `/dev/ttyESP32C3` | VID `303a`, PID `1001` | ESP32-C3 (built-in USB) |
| `/dev/ttyNRF52840` | VID `1915`, serial `DADB34875764` or PID `cafe` | nRF52840 (PCA10059) |
| `/dev/ttyESP32CAM` | VID `1a86`, PID `7523`, USB path `1-1.2.3*` | ESP32-D0WD-V3 (via CH340) |
| `/dev/ttyESP32DEV` | VID `10c4`, PID `ea60` | ESP32-D0WDQ6 (via CP2102) |

All devices are set to `MODE="0666"` (world read/write) so flashing tools don't
require root.

### Updating Rules

```bash
# Edit rules
sudo nano /etc/udev/rules.d/99-esp-nordic.rules

# Reload and re-trigger
sudo udevadm control --reload-rules
sudo udevadm trigger

# Verify symlinks
ls -la /dev/ttyESP32C3 /dev/ttyNRF52840 /dev/ttyESP32CAM /dev/ttyESP32DEV
```

### Adding New Devices

If a new ESP32 or nRF board is added, identify it:

```bash
# Find the new device
lsusb
ls -la /dev/serial/by-id/

# Get detailed attributes for udev matching
udevadm info -a -n /dev/ttyACMx   # or /dev/ttyUSBx
```

Add a rule matching on `idVendor`/`idProduct` (and `serial` if available, or
`KERNELS` for the USB path if no serial number).

## Power Management

### RPi4 Power (PoE)

The RPi4 is powered via PoE from the GSM7252PS-S1 switch, port `1/0/27`.

### USB Device Power Cycling

USB power cycling is available via `uhubctl` for resetting hung devices without
physically unplugging them.

**Important power topology**:

| Hub | Type | Power Switching | Devices |
|-----|------|----------------|---------|
| VIA Labs (1-1) | 4-port | **Per-port (ppps)** | CP2102 (port 4), Genesys hub (port 2) |
| Genesys Logic (1-1.2) | 4-port | **Ganged** | wlanE, nRF52840, ESP32-CAM, ESP32-C3 |

The VIA Labs hub supports per-port power switching (ppps), so the CP2102 can be
power-cycled independently. However, the Genesys Logic hub has **ganged power
switching** — cycling its power toggles ALL four devices (wlanE, nRF52840, ESP32-CAM,
ESP32-C3) simultaneously.

### Power Cycle Commands

```bash
# Power cycle ALL devices on the Genesys hub (ESP32-C3, nRF52840, ESP32-CAM, wlanE)
sudo uhubctl -l 1-1 -p 2 -a off   # turn off Genesys hub power
sleep 3
sudo uhubctl -l 1-1 -p 2 -a on    # turn on Genesys hub power

# Power cycle CP2102 only (independent, on VIA Labs hub port 4)
sudo uhubctl -l 1-1 -p 4 -a off
sleep 2
sudo uhubctl -l 1-1 -p 4 -a on

# Power cycle with automatic on (off for 2 seconds, then on)
sudo uhubctl -l 1-1 -p 2 -a cycle -d 2   # Genesys hub (all dev board devices)
sudo uhubctl -l 1-1 -p 4 -a cycle -d 2   # CP2102 only

# Check current power status
sudo uhubctl
```

### Software Reset (without power cycling)

For devices that support it, a USB port rebind can reset the device without
power cycling:

```bash
# Unbind and rebind a specific USB device (e.g., ESP32 at 1-1.2.4)
echo '1-1.2.4' | sudo tee /sys/bus/usb/drivers/usb/unbind
sleep 1
echo '1-1.2.4' | sudo tee /sys/bus/usb/drivers/usb/bind

# ESP32 can also be reset into bootloader via esptool:
esptool.py --port /dev/ttyESP32C3 --before default_reset --after no_reset run
```

## LLDP Configuration

lldpd is installed and configured with a descriptive system string.

| Property | Value |
|----------|-------|
| Service | `lldpd.service` (enabled, running) |
| Config | `/etc/lldpd.d/description.conf` |
| SysDescr | `RPi 4 8GB - ESP32/Nordic firmware test station` |
| ChassisID | `dc:a6:32:b4:5f:29` (eth0 MAC) |
| Upstream switch | GSM7252PS-S1 port `1/0/27` |

```bash
# View neighbors
sudo lldpcli show neighbors

# View local chassis info
sudo lldpcli show chassis
```

## Installed Software

Key packages for firmware development:

| Package | Location | Purpose |
|---------|----------|---------|
| `lldpd` | system | LLDP neighbor discovery |
| `uhubctl` | system | USB hub power control |
| `hostapd` 2.10 | system (masked) | WiFi AP for wlanE |
| `iw` | `/usr/sbin/iw` | WiFi configuration |
| `esptool` 5.2.0 | `/opt/esptool/` (venv) | ESP32 flash/probe tool |
| `nrfutil` 5.2.0 | `/opt/esptool/` (venv) | Nordic nRF DFU flash tool |
| `python3` | system | Scripting |

A second, per-user esptool venv exists at `~/.venvs/esptool/` (also with `pyserial`); this is the one `tools/capture_hardware_baseline.py` and `tools/capture_all_baselines.py` invoke over SSH. Both venvs are usable interchangeably.

### Using esptool / nrfutil

```bash
# Probe connected ESP32
/opt/esptool/bin/esptool --port /dev/ttyESP32C3 chip-id
/opt/esptool/bin/esptool --port /dev/ttyESP32C3 flash-id

# Flash firmware
/opt/esptool/bin/esptool --port /dev/ttyESP32C3 --baud 460800 write_flash 0x0 firmware.bin

# Nordic DFU flash
/opt/esptool/bin/nrfutil dfu serial -pkg firmware.zip -p /dev/ttyNRF52840
```

### Recommended Additional Packages

```bash
# ESP-IDF prerequisites
sudo apt install -y git wget flex bison gperf python3-pip python3-venv \
    cmake ninja-build ccache libffi-dev libssl-dev dfu-util libusb-1.0-0

# Nordic nRF Connect SDK prerequisites
sudo apt install -y python3-pip python3-venv git cmake ninja-build \
    gperf ccache dfu-util device-tree-compiler

# Serial tools
sudo apt install -y picocom minicom python3-serial
```

## Status

### Completed
- [x] Hostname set to `rpi4-esp`
- [x] lldpd installed and configured
- [x] uhubctl installed
- [x] esptool and nrfutil installed (`/opt/esptool/` venv)
- [x] USB device inventory documented (all chips probed via esptool)
- [x] udev rules for stable `/dev/tty*` symlinks
- [x] USB power cycling tested and documented
- [x] ESP32 chips identified: ESP32-C3 (native USB), ESP32-D0WD-V3 (CAM-MB), ESP32-D0WDQ6 (DevKit)
- [x] Nordic dongle identified: nRF52840 PCA10059 (in DFU bootloader mode)

### TODO
- [x] DNS entries added to gdoc2netcfg Google Sheet (eth0 + wlan0)
- [x] IPv6 DUID-LL configured on both eth0 and wlan0
- [x] Moved to GSM7252PS-S1 port 1/0/27, VLAN 90 configured
- [ ] Flash application firmware onto Nordic dongle (currently in DFU bootloader)
- [ ] Install ESP-IDF for full development workflow
- [ ] Install Nordic nRF Connect SDK (west/Zephyr)
- [ ] Set up automated firmware test scripts
