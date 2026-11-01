# Neonix

Open-source RGB lighting and fan control for Windows. One app for your motherboard, GPU, AIO, fans and ARGB controllers.

> **Status: v0.1 (early).** Built and tested on one PC so far (Gigabyte B350 + Radeon RX 9060 XT). Other hardware may work through the OpenRGB driver layer, but expect rough edges. Bug reports and "it works on my X" reports are very welcome.

## Features

| Tab | What it does |
| --- | --- |
| **Dashboard** | Auto-detected specs, usage, temperatures and fan speeds |
| **Devices** | RGB devices and USB controllers found automatically, rename them, identify them, light a range of a daisy chain |
| **Lighting** | Static, Breathing, Rainbow, Wave, Colour cycle; speed, brightness, colours |
| **Sync** | Lighting colour follows CPU or GPU temperature |
| **Fans** | Per-fan curves, Silent / Balanced / Performance / 100% presets |
| **Profiles** | Save setups, auto-switch when a chosen program (e.g. a game) is running |
| **Settings** | Start with Windows, tray behaviour, RGB driver path |

Not yet: screen ambience, audio visualiser, game integrations, native drivers for individual brands (planned).

## Install

1. Download `Neonix-win-x64.zip` from [Releases](../../releases) and unzip it.
2. Download a recent **experimental** build of [OpenRGB](https://openrgb.org) and put its files in a folder called `OpenRGB` next to `Neonix.exe` (or set the path in Settings).
3. Close RGB Fusion, SignalRGB, iCUE and similar. Only one app can control a device at a time.
4. Run `Neonix.exe`. It asks for administrator rights, which are needed to read sensors and set fan speeds.

Neonix starts the OpenRGB driver in the background for you. You never need to open OpenRGB itself.

## Build from source

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download) on Windows.

```
dotnet run --project src/Neonix
```

## Notes

- Controllers that connect by USB (Airgoo, Nollie, Corsair Node, NZXT Hue and others) show up as their own devices. Lights wired in a daisy chain from a motherboard header can't be detected. Use **Devices → Daisy-chain setup** to find out which LEDs belong to which part.
- If antivirus or anti-cheat flags Neonix, it's because fan control uses a kernel driver (via LibreHardwareMonitor). Neonix doesn't need it for RGB only.
- Closing Neonix hands every fan back to the BIOS.

## Credits and licence

Neonix is released under the **GPL-2.0** licence because it works with [OpenRGB](https://gitlab.com/CalcProgrammer1/OpenRGB). Sensor and fan access is by [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (MPL-2.0). USB scanning uses HidSharp; the OpenRGB client library is OpenRGB.NET.
