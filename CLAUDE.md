# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

LazyBootstrap is a Windows Forms launcher application for SDVX (Sound Voltex) game packages. It provides a GUI for configuring and launching the game with spice2x/spicetools, managing Asphyxia Core server, and handling various game settings.

## Build Commands

```bash
# Build the solution (Debug)
msbuild LazyBootstrap.sln /p:Configuration=Debug

# Build the solution (Release)
msbuild LazyBootstrap.sln /p:Configuration=Release

# Alternative: Use Visual Studio
# Open LazyBootstrap.sln in Visual Studio and build from IDE (Ctrl+Shift+B)
```

Output locations:
- Debug: `LazyBootstrap\bin\Debug\LazyBootstrap.exe`
- Release: `LazyBootstrap\bin\Release\LazyBootstrap.exe`

## Architecture

### Core Components

**BootstrapForm.cs** (BootstrapForm:260-387, 390-425)
- Main form handling game launch orchestration
- Manages process lifecycle: screen rotation → Asphyxia Core → game (spice64.exe)
- Handles cleanup on game exit: kills Asphyxia, restores screen rotation
- Builds spice2x command-line arguments based on user configuration

**ConfigHandler.cs**
- INI file configuration manager using Windows Kernel32 API
- Persists settings between sessions (EA server, PCBID, network config)
- Stores history items for combo boxes (last 10 entries per field)

**ScreenRotate.cs**
- Uses Windows Display Settings API (ChangeDisplaySettingsEx)
- Rotates screen at launch and optionally restores on exit
- Supports 0°, 90°, 180°, 270° orientations

### Launch Flow

1. User clicks "启动" (Start) button
2. Screen rotation applied if configured
3. Asphyxia Core launched (unless "不启动氧无" checked)
4. spice64.exe launched with configured arguments
5. On game exit: Asphyxia killed, screen restored (unless disabled)

### Configuration Modes

**Preconfig Mode** (default, `chkUsePreconfig.Checked = true`)
- Uses predefined XML config: `lazy/spicetools.xml`
- Uses patch manager: `lazy/spicetools_patch_manager.json`
- Applies user-specified EA server, PCBID, network settings via command-line

**Expert Mode** (`chkUsePreconfig.Checked = false`)
- No default arguments passed to spice64.exe
- User must configure everything via spicecfg.exe

### Expected Directory Structure

The application expects this layout relative to the executable:
```
LazyBootstrap.exe
config.ini                          # Saved settings
asphyxia/
  asphyxia-core-x64.exe             # Server backend
contents/
  spice64.exe                       # Game launcher (spicetools)
  spicecfg.exe                      # Configuration editor
  modules/                          # DLL injection folder
    nvcuda.dll                      # Compat layer files (optional)
    nvcuvid.dll
    nvEncodeAPI64.dll
    d3d9.dll
  lazy/
    spicetools.xml                  # Preconfig settings
    spicetools_patch_manager.json   # Patch configuration
    stubs/                          # Compat layer storage
  data_mods/
    _cache/                         # ifs_hook cache (can be cleared)
runtime/
  install.bat                       # Runtime installer script
```

## Key Features

### Compatibility Layer Management (BootstrapForm:670-742)
- Load/unload NVIDIA API and DXVK stubs for AMD/Intel GPU compatibility
- Moves files between `contents/modules/` and `contents/lazy/stubs/`
- Status indicator shows: Loaded (green), Partial (orange), Not loaded (red)

### Process Management (BootstrapForm:428-542)
- Force-kill spice64 and asphyxia-core-x64 processes
- Attempts graceful termination, falls back to `taskkill /F` if needed
- Handles permission elevation for stubborn processes

### History Tracking (BootstrapForm:48-143)
- Stores last 10 unique values for EA server, PCBID, network IP, subnet mask
- Persists to INI file under `[History]` section
- Deduplicates entries, most recent first

## Common Modifications

### Adding New Launch Options
1. Add checkbox to `BootstrapForm.Designer.cs` in `groupBoxOptions`
2. In `btnStart_Click` (BootstrapForm:260), append argument to `argsBuilder`
3. Example: `if (chkNewOption.Checked) argsBuilder.Append("-newoption ");`

### Modifying Default Paths
- Game executable: `BootstrapForm:356` (`spice64.exe`)
- Asphyxia path: `BootstrapForm:280` (`asphyxia-core-x64.exe`)
- Config editor: `BootstrapForm:585` (`spicecfg.exe`)

### Changing Configuration File Format
- Currently uses Windows INI format via Kernel32 API
- To switch to JSON/XML: replace `ConfigHandler.cs` implementation
- Keep same public interface (`ReadString`, `WriteString`) to minimize changes

## Target Framework

- .NET Framework 4.7.2
- Windows Forms application
- Requires Windows OS (uses Win32 APIs for display rotation and INI handling)
