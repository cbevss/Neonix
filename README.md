# Neonix

Open-source RGB lighting and fan control for Windows. One app for your motherboard, GPU, AIO, fans and ARGB controllers.

> **Status: v0.1 (early).** Built and tested on one PC so far (Gigabyte B350 + Radeon RX 9060 XT). Other hardware may work through the OpenRGB driver layer, but expect rough edges. Bug reports and "it works on my X" reports are very welcome.

## Features

| Tab | What it does |
| --- | --- |
| **Dashboard** | Auto-detected specs, usage, temperatures and fan speeds |
| **Devices** | RGB devices and USB controllers found automatically, rename them, identify them, light a range of a daisy chain |
| **Lighting** | Static, Breathing, Rainbow, Wave, Colour cycle, Fire, Comet, Sparkle, plus **Screen ambience** and an **Audio visualiser**; live preview, speed, brightness, colours |
| **Sync** | Lighting colour follows CPU or GPU temperature |
| **Fans** | Per-fan curves, Silent / Balanced / Performance / 100% presets |
| **Profiles** | Save setups, auto-switch when a chosen program (e.g. a game) is running |
| **Account** | Local email + password accounts that keep each person's profiles separate (stored on this PC only, no cloud sync yet) |
| **Settings** | Start with Windows, tray behaviour, RGB driver path |

The sidebar slides away with the chevron at its top right (or **Ctrl+B**).

Not yet: game integrations, cloud accounts and sync, native drivers for individual brands (planned).

## Install

1. Download **Neonix-Setup-x.y.z.exe** from [Releases](../../releases) and run it.
2. Close RGB Fusion, SignalRGB, iCUE and similar. Only one app can control a device at a time.
3. Start Neonix. It asks for administrator rights, which are needed to read sensors and set fan speeds.

The installer includes the OpenRGB driver layer where it could be bundled; Neonix starts it in the background for you, so you never need to open OpenRGB yourself. If your hardware is very new, drop a newer experimental OpenRGB build into the `OpenRGB` folder inside the install folder (or set its path in Settings).

A portable zip (`Neonix-portable-win-x64.zip`) is also available.

## Build from source

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download) on Windows.

```
dotnet run --project src/Neonix
```

## Notes

- Controllers that connect by USB (Airgoo, Nollie, Corsair Node, NZXT Hue and others) show up as their own devices. Lights wired in a daisy chain from a motherboard header can't be detected. Use **Devices → Daisy-chain setup** to find out which LEDs belong to which part.
- If antivirus or anti-cheat flags Neonix, it's because fan control uses a kernel driver (via LibreHardwareMonitor). Neonix doesn't need it for RGB only.
- Closing Neonix hands every fan back to the BIOS.
- Screen ambience samples your main monitor and the audio visualiser listens to what's *playing* on your PC (not the microphone). Both run only while their effect is selected, entirely on your PC; nothing is recorded or sent anywhere.
- Accounts are local. Passwords are stored only as a salted PBKDF2 hash, and a forgotten password can't be recovered.

## Credits and licence

Neonix is released under the **GPL-2.0** licence because it works with [OpenRGB](https://gitlab.com/CalcProgrammer1/OpenRGB). Sensor and fan access is by [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (MPL-2.0). USB scanning uses HidSharp; the OpenRGB client library is OpenRGB.NET.
