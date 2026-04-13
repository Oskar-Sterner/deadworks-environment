# Deadworks Environment

Everything you need to get started building server-side plugins for [Deadlock](https://store.steampowered.com/app/1422450/Deadlock/) using the [Deadworks](https://github.com/Deadworks-net/deadworks) framework.

> **Deadworks is in early development** — APIs are not finalized and will change without notice. No prebuilt binaries are distributed; you must build from source.

## What is Deadworks?

Deadworks is a **server-side modding framework** for Valve's Deadlock. It lets you write plugins in **C#** that hook into the game server's lifecycle, intercept events, modify gameplay, and add custom features — all running on the server (not client-side).

Key features:
- **.NET 10** managed plugin system with hot-reload support
- **Attribute-based** event handlers and chat commands
- Full access to **Source 2 game events** (60+ events)
- **Lifecycle hooks** for damage, chat, currency, abilities, entities, and more
- **Timer API** for scheduled/periodic work
- **Player API** for accessing connected players and their pawns
- Auto-deploy builds directly to your game server

## Repository Structure

```
deadworks-environment/
├── README.md                 <-- You are here
├── CLAUDE.md                 <-- AI-assisted development guide (for Claude Code)
├── Boilerplate/              <-- Plugin template — copy this to start a new plugin
│   ├── Boilerplate.cs        <-- All overridable hooks with inline docs
│   ├── Boilerplate.csproj    <-- Template project file
│   └── GameEvents.md         <-- Complete Source 2 game event catalog
├── examples/
│   └── DevMode/              <-- Simple working example plugin
│       ├── DevMode.cs
│       └── DevMode.csproj
└── docs/
    ├── architecture.md       <-- How deadworks works under the hood
    ├── api-reference.md      <-- Quick API cheat sheet
    └── plugin-architecture.md <-- Recommended plugin structure for real projects
```

## Prerequisites

### 1. Deadlock (Steam)

Install [Deadlock](https://store.steampowered.com/app/1422450/Deadlock/) via Steam. Note your install path:

```
# Typical paths:
C:\Program Files (x86)\Steam\steamapps\common\Deadlock
F:\SteamLibrary\steamapps\common\Deadlock
```

### 2. .NET 10 SDK

Download from [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).

Verify installation:
```bash
dotnet --version
# Should output 10.x.x
```

After installing, locate the `nethost` static library path (needed for building deadworks itself):
```
C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Host.win-x64\10.0.x\runtimes\win-x64\native
```

### 3. Visual Studio 2026 (recommended)

Install [Visual Studio 2026](https://visualstudio.microsoft.com/) with these workloads:
- **Desktop development with C++**
- **.NET desktop development**

> You can build plugins with just the .NET SDK and `dotnet build`, but building the deadworks framework itself requires Visual Studio with C++.

### 4. protobuf 3.21.8 (for building deadworks framework only)

If you're only writing plugins, skip this — you just need the pre-built `deadworks.exe` and API DLLs. If you're building the framework from source:

```bash
# Clone
git clone --branch v3.21.8 --depth 1 https://github.com/protocolbuffers/protobuf.git protobuf-3.21.8

# Configure
cmake -B build -DCMAKE_BUILD_TYPE=Release -Dprotobuf_BUILD_TESTS=OFF -Dprotobuf_MSVC_STATIC_RUNTIME=ON

# Build
cmake --build build --config Release
```

This produces `libprotobuf.lib` in `build/Release/`.

### 5. Environment Variable

Set `DEADLOCK_GAME_DIR` so plugin builds can find the API DLLs and auto-deploy:

```powershell
# PowerShell (permanent)
[Environment]::SetEnvironmentVariable("DEADLOCK_GAME_DIR", "C:\Path\To\Deadlock", "User")

# Or pass at build time
dotnet build -p:DeadlockDir="C:\Path\To\Deadlock"
```

## Quick Start

### Step 1: Build the Deadworks Framework

```bash
git clone --recurse-submodules https://github.com/Deadworks-net/deadworks.git
cd deadworks

# Copy and edit local.props with your paths
cp local.props.example local.props
# Edit local.props: set ProtobufIncludeDir, ProtobufLibDir, NetHostDir, DeadlockDir
```

Open `deadworks.slnx` in Visual Studio and build (x64 Release). This produces `deadworks.exe` and `DeadworksManaged.Api.dll`.

### Step 2: Create Your First Plugin

```bash
# Copy the boilerplate
cp -r Boilerplate/ MyPlugin/

# Rename files and update namespaces
mv MyPlugin/Boilerplate.cs MyPlugin/MyPlugin.cs
mv MyPlugin/Boilerplate.csproj MyPlugin/MyPlugin.csproj
```

Edit `MyPlugin.cs` — replace `Boilerplate` with `MyPlugin` everywhere, then keep only the hooks you need.

Edit `MyPlugin.csproj` — replace `Boilerplate` with `MyPlugin` in `RootNamespace`, `AssemblyName`, and the `DeployFiles` item.

### Step 3: Build

```bash
cd MyPlugin
dotnet build
```

The `DeployToGame` MSBuild target automatically copies the DLL to `<Deadlock>/game/bin/win64/managed/plugins/`.

### Step 4: Run the Server

```bash
cd "<Deadlock>/game/bin/win64"
./deadworks.exe \
  -dedicated -console -dev -insecure -allow_no_lobby_connect \
  +sv_cheats 1 +tv_enable 0 +hostport 27015 +map dl_midtown
```

### Step 5: Connect

Open Deadlock, open the console, and type:
```
connect localhost:27015
```

## Server Management

### Starting the Server

**Always start from the server's own directory** (`game/bin/win64/`) or it will fail to load config files.

```bash
# Full command with all recommended flags
cd "<Deadlock>/game/bin/win64"
./deadworks.exe \
  -dedicated -console -dev -insecure -allow_no_lobby_connect \
  +sv_cheats 1 +tv_enable 0 +hostport 27015 +map dl_midtown
```

Flag reference:
| Flag | Purpose |
|------|---------|
| `-dedicated` | Run as dedicated server |
| `-console` | Enable server console |
| `-dev` | Development mode |
| `-insecure` | Disable VAC (local dev) |
| `-allow_no_lobby_connect` | Allow direct `connect` without lobby |
| `+sv_cheats 1` | Enable cheat commands |
| `+tv_enable 0` | Disable SourceTV |
| `+hostport 27015` | Server port |
| `+map dl_midtown` | Starting map (use `+map`, not `-map`) |

### After Plugin Changes

The server locks plugin DLLs while running. You must:

1. **Stop** the server: `taskkill /f /im deadworks.exe`
2. **Build** the plugin: `dotnet build`
3. **Restart** the server

### In-Game Console Commands

| Command | Description |
|---------|-------------|
| `dw_listplugins` | List loaded plugins |
| `dw_reloadplugin <name>` | Hot-reload a specific plugin |
| `dw_reloadconfig` | Reload server config |

## Creating a Plugin from Scratch

### Minimal `.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>MyPlugin</RootNamespace>
    <AssemblyName>MyPlugin</AssemblyName>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDynamicLoading>true</EnableDynamicLoading>

    <DeadlockDir Condition="'$(DeadlockDir)' == ''">$(DEADLOCK_GAME_DIR)</DeadlockDir>
    <DeadlockBin>$(DeadlockDir)\game\bin\win64</DeadlockBin>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="DeadworksManaged.Api">
      <HintPath>$(DeadlockBin)\managed\DeadworksManaged.Api.dll</HintPath>
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </Reference>
    <Reference Include="Google.Protobuf">
      <HintPath>$(DeadlockBin)\managed\Google.Protobuf.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <Target Name="DeployToGame" AfterTargets="Build">
    <ItemGroup>
      <DeployFiles Include="$(OutputPath)MyPlugin.dll;$(OutputPath)MyPlugin.pdb" />
    </ItemGroup>
    <Copy SourceFiles="@(DeployFiles)"
          DestinationFolder="$(DeadlockBin)\managed\plugins"
          SkipUnchangedFiles="false"
          Retries="0"
          ContinueOnError="WarnAndContinue" />
  </Target>

</Project>
```

Key points:
- `EnableDynamicLoading` **must** be `true`
- API references use `<Private>false</Private>` — the server already has these DLLs
- `DeployToGame` auto-copies the build output to the server
- If your plugin has **native DLLs** (e.g. SQLite), deploy them to `bin/win64/` directly, NOT `managed/plugins/`

### Minimal Plugin Class

```csharp
using DeadworksManaged.Api;

namespace MyPlugin;

public class MyPlugin : DeadworksPluginBase
{
    public override string Name => "MyPlugin";

    public override void OnLoad(bool isReload)
    {
        Console.WriteLine($"[{Name}] {(isReload ? "Reloaded" : "Loaded")}!");
    }

    public override void OnUnload()
    {
        Console.WriteLine($"[{Name}] Unloaded!");
    }
}
```

## Plugin Architecture Best Practices

For simple plugins, a single `.cs` file is fine (see `examples/DevMode/`). For anything more complex, see **[docs/plugin-architecture.md](docs/plugin-architecture.md)** for the recommended folder structure and patterns, including:

- Main class as orchestrator (lifecycle + delegation)
- Constructor-based dependency injection
- Repository pattern for database access
- Immutable data models with C# records
- State machines for game logic
- Slot-based player state tracking
- Timer-based ticking (100ms) instead of OnGameFrame
- NuGet + native DLL deployment rules
- Resilient HUD entity lifecycle

Quick rules:
1. **One responsibility per plugin** — don't build monoliths
2. **Use Timers instead of OnGameFrame** for periodic work
3. **Clean up in OnUnload** — cancel listeners, dispose resources
4. **Use HookResult correctly** — `Continue`, `Changed`, `Handled`, `Stop`
5. **Keep plugins independent** — don't depend on other plugins
6. **Native DLLs go in `bin/win64/`**, NOT `managed/plugins/`

## Further Reading

- [Boilerplate/Boilerplate.cs](Boilerplate/Boilerplate.cs) — Complete hook reference
- [Boilerplate/GameEvents.md](Boilerplate/GameEvents.md) — All 60+ Source 2 game events
- [docs/architecture.md](docs/architecture.md) — How deadworks works
- [docs/api-reference.md](docs/api-reference.md) — API cheat sheet
- [examples/DevMode/](examples/DevMode/) — Simple working example
- [Deadworks GitHub](https://github.com/Deadworks-net/deadworks) — Framework source

## License

This environment package is provided as-is for the Deadworks community. The Deadworks framework has its own license — see the [Deadworks repository](https://github.com/Deadworks-net/deadworks).
