# Deadworks Architecture

## Overview

Deadworks is a server-side modding framework that bridges Valve's Source 2 engine (native C++) with managed .NET plugins (C#). It replaces the stock Deadlock dedicated server executable with a custom one that embeds a .NET runtime and plugin loader.

```
┌─────────────────────────────────────────────────┐
│                  Deadlock Server                 │
│              (Source 2 Engine, C++)              │
├─────────────────────────────────────────────────┤
│              deadworks.exe (native)              │
│  ┌───────────────────────────────────────────┐  │
│  │         Native Plugin Loader (C++)        │  │
│  │  - Hooks into Source 2 event system       │  │
│  │  - Embeds .NET runtime via nethost        │  │
│  │  - Marshals callbacks to managed code     │  │
│  │  - Uses protobuf for game data            │  │
│  └─────────────────┬─────────────────────────┘  │
│                    │ Interop                     │
│  ┌─────────────────┴─────────────────────────┐  │
│  │      DeadworksManaged.Api (.NET 10)       │  │
│  │  - DeadworksPluginBase (base class)       │  │
│  │  - Entity types (CCitadelPlayerPawn, etc) │  │
│  │  - GameEvent system                       │  │
│  │  - Timer, Chat, Server, Players APIs      │  │
│  │  - Source generators for boilerplate       │  │
│  └─────────────────┬─────────────────────────┘  │
│                    │ Plugin API                  │
│  ┌─────────────────┴─────────────────────────┐  │
│  │           Your Plugin (.NET 10)           │  │
│  │  - Extends DeadworksPluginBase            │  │
│  │  - Overrides lifecycle hooks              │  │
│  │  - Uses [GameEventHandler] attributes     │  │
│  │  - Uses [ChatCommand] attributes          │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

## Components

### 1. Native Layer (`deadworks.exe`)

Written in C++, this is the core of the framework:

- **Source 2 Integration**: Hooks into the engine's game event manager (`IGameEventManager2`), entity system, and server callbacks
- **.NET Hosting**: Embeds the .NET 10 runtime using `nethost` and `hostfxr` APIs
- **Protobuf**: Uses protobuf 3.21.8 for serializing/deserializing game data structures
- **Callback Marshaling**: Translates native C++ callbacks into managed C# method calls

**Build dependencies**: Visual Studio 2026 (C++ workload), protobuf 3.21.8, .NET 10 SDK (for nethost)

### 2. Managed API (`DeadworksManaged.Api.dll`)

The C# API that plugins reference:

- **`DeadworksPluginBase`**: Abstract base class all plugins extend. Provides virtual methods for every hookable lifecycle event.
- **`IDeadworksPlugin`**: Interface defining the plugin contract
- **Entity Types**: Strongly-typed wrappers for Source 2 entities (`CCitadelPlayerPawn`, `CBasePlayerController`, etc.)
- **`GameEvent`**: Event reader with typed accessors (`GetInt`, `GetString`, `GetBool`, `GetFloat`, `GetPlayerController`, etc.)
- **`GameEvents`**: Static class for dynamic listener registration and event creation
- **`Timer`**: Per-plugin timer service with `Once()`, `Every()`, `NextTick()`, `Sequence()`
- **`Chat`**: Chat messaging API (`PrintToChat`, `PrintToChatAll`)
- **`Server`**: Server command execution and state
- **`Players`**: Player enumeration and lookup
- **`Precache`**: Resource precaching
- **Source Generators**: Compile-time code generation for reducing boilerplate

### 3. Plugin System

Plugins are .NET 10 class libraries loaded dynamically at server startup:

- **Discovery**: The plugin loader scans `managed/plugins/` for DLLs containing `DeadworksPluginBase` subclasses
- **Loading**: Each plugin is loaded into an isolated `AssemblyLoadContext` with `EnableDynamicLoading`
- **Initialization**: `OnLoad(isReload)` is called, then declarative `[GameEventHandler]` and `[ChatCommand]` methods are registered
- **Hot Reload**: `dw_reloadplugin <name>` unloads and reloads a plugin without restarting the server
- **Isolation**: Plugins are independent — they share the API but don't reference each other

## Event Flow

### Lifecycle Hooks

```
Engine Event (C++)
  → Native hook intercepts
    → Marshal to managed code
      → DeadworksPluginBase.OnXxx() called
        → Plugin returns HookResult
          → Marshal back to native
            → Engine acts on result (Continue/Stop/etc.)
```

### Source 2 Game Events

```
Engine fires game event (e.g. "player_death")
  → IGameEventManager2 dispatches
    → Deadworks native listener catches
      → Creates managed GameEvent wrapper
        → Calls [GameEventHandler("player_death")] methods
          → Plugin reads fields (GetString, GetInt, etc.)
            → Returns HookResult
```

### HookResult Precedence

When multiple plugins handle the same event, the highest HookResult wins:

```
Continue (0) < Changed (1) < Handled (2) < Stop (3)
```

- **Continue**: Pass through, no modification
- **Changed**: Args were modified, keep processing other plugins
- **Handled**: This plugin consumed it, but other plugins still see it
- **Stop**: Cancel the engine action entirely (e.g., block damage, swallow chat)

## File Layout on Disk

```
<Deadlock>/game/bin/win64/
├── deadworks.exe                          # Custom server executable
├── e_sqlite3.dll                          # Native deps go HERE (not in plugins/)
├── managed/
│   ├── DeadworksManaged.Api.dll           # Framework API
│   ├── Google.Protobuf.dll                # Protobuf runtime
│   └── plugins/                           # Plugin DLLs + managed deps
│       ├── MyPlugin.dll
│       ├── MyPlugin.pdb
│       ├── Microsoft.Data.Sqlite.dll      # Managed NuGet deps OK here
│       └── ...
```

**Critical**: Native DLLs (`.dll` files that are not .NET assemblies) must be placed in `bin/win64/` directly. The plugin loader will throw `BadImageFormatException` if it encounters native DLLs in `managed/plugins/`.

## Building the Framework from Source

For contributors who need to build deadworks itself (not just plugins):

1. **Clone**: `git clone --recurse-submodules https://github.com/Deadworks-net/deadworks.git`
2. **Configure**: Copy `local.props.example` to `local.props` and set:
   - `ProtobufIncludeDir` — protobuf 3.21.8 `src/` directory
   - `ProtobufLibDir` — protobuf `build/Release/` directory
   - `NetHostDir` — .NET SDK `nethost` native directory
   - `DeadlockDir` — Deadlock install `game\bin\win64` (enables auto-deploy)
3. **Build**: Open `deadworks.slnx` in Visual Studio 2026, build x64 Release
4. **Deploy**: Built files auto-copy to `DeadlockDir` if configured

### protobuf 3.21.8 Build

```bash
git clone --branch v3.21.8 --depth 1 https://github.com/protocolbuffers/protobuf.git protobuf-3.21.8
cd protobuf-3.21.8
cmake -B build -DCMAKE_BUILD_TYPE=Release -Dprotobuf_BUILD_TESTS=OFF -Dprotobuf_MSVC_STATIC_RUNTIME=ON
cmake --build build --config Release
```

Produces:
- `src/` — headers (for `ProtobufIncludeDir`)
- `build/Release/libprotobuf.lib` — static library (for `ProtobufLibDir`)
