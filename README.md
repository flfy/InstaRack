# InstaRack

**InstaRack** removes the blocking wait when a rack is unboxed onto the floor in *Data Center*.

## Features

- Instantaneous rack placement. That is all.

## Requirements

- **[MelonLoader](https://melonwiki.xyz/#/)**

## Installation

1. Install **MelonLoader** for **Data Center**.
2. Download the latest `InstaRack.dll` release.
3. Drag `InstaRack.dll` into your **Data Center/Mods** folder.

```text
Data Center/
└── Mods/
    └── InstaRack.dll
```

4. Launch the game and place a rack as normal.

## Building From Source

### Prerequisites

- .NET SDK 6.0 or newer.
- A working **Data Center** install with **MelonLoader** already installed.

### Setup

Set `gamepath` in `Local.Build.props` to your **Data Center** game folder containing **Data Center.exe**.

```xml
<Project>
  <PropertyGroup>
    <gamepath>/path/to/Data Center</gamepath>
  </PropertyGroup>
</Project>
```

### Build

```bash
dotnet build InstaRack.csproj
```

The compiled DLL is written to:

```text
bin/1.0.0/Debug/net6.0/InstaRack.dll
```

After the build completes, the project also copies `InstaRack.dll` into:

```text
<gamepath>/Mods/InstaRack.dll
```
