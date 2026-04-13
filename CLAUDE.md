# Deadworks Plugin Development - Claude Code Guide

You are working in a **Deadworks plugin development environment** for the game Deadlock. Deadworks is a server-side .NET 10 modding framework that lets you write C# plugins for Deadlock dedicated servers.

## Directory Structure

```
deadworks-environment/
├── Boilerplate/               <-- Plugin template (copy to start new plugins)
│   ├── Boilerplate.cs         <-- Full reference of all DeadworksPluginBase overrides
│   ├── Boilerplate.csproj     <-- Template .csproj with env-var-based paths
│   └── GameEvents.md          <-- Complete Source 2 game event catalog (60+ events)
├── examples/
│   └── DevMode/               <-- Simple working example plugin
├── docs/
│   ├── architecture.md        <-- How deadworks works under the hood
│   └── api-reference.md       <-- Quick API cheat sheet
├── README.md                  <-- Human-readable getting started guide
└── CLAUDE.md                  <-- This file
```

## How Deadworks Plugins Work

Plugins are .NET 10 class libraries that:
1. Reference `DeadworksManaged.Api.dll` (provided by deadworks)
2. Subclass `DeadworksPluginBase`
3. Override lifecycle hooks and/or use `[GameEventHandler]` attributes
4. Build with `dotnet build` — auto-deployed to the server via MSBuild target

## Prerequisites

- **.NET 10 SDK** — `dotnet.microsoft.com/download/dotnet/10.0`
- **Deadlock** installed via Steam
- **Environment variable**: `DEADLOCK_GAME_DIR` pointing to the Deadlock install root (e.g. `F:\SteamLibrary\steamapps\common\Deadlock`)
- **Deadworks built and installed** — `deadworks.exe` and `DeadworksManaged.Api.dll` must exist at `<Deadlock>/game/bin/win64/`

For building the deadworks framework itself (not just plugins):
- **Visual Studio 2026** with C++ and .NET workloads
- **protobuf 3.21.8** — headers in `src/`, static lib `libprotobuf.lib` from Release build

## Creating a New Plugin

### 1. Scaffold from Boilerplate

```bash
cp -r Boilerplate/ Plugins/MyPlugin/
```

### 2. Rename files

```bash
mv Plugins/MyPlugin/Boilerplate.cs Plugins/MyPlugin/MyPlugin.cs
mv Plugins/MyPlugin/Boilerplate.csproj Plugins/MyPlugin/MyPlugin.csproj
```

### 3. Find/replace `Boilerplate` → `MyPlugin` in both files

In `MyPlugin.csproj`, update:
- `<RootNamespace>MyPlugin</RootNamespace>`
- `<AssemblyName>MyPlugin</AssemblyName>`
- `DeployFiles` Include path: `$(OutputPath)MyPlugin.dll;$(OutputPath)MyPlugin.pdb`

In `MyPlugin.cs`, update:
- `namespace MyPlugin;`
- `public class MyPlugin : DeadworksPluginBase`
- `public override string Name => "MyPlugin";`

### 4. Keep only the hooks you need, delete the rest

### 5. Build

```bash
cd Plugins/MyPlugin
dotnet build
```

## Required .csproj Structure

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
          SkipUnchangedFiles="false" Retries="0" ContinueOnError="WarnAndContinue" />
  </Target>
</Project>
```

**Critical rules:**
- `EnableDynamicLoading` must be `true` — the plugin loader requires this
- API refs use `<Private>false</Private>` — the server already has them
- Native DLLs (e.g. `e_sqlite3.dll`) go in `bin/win64/` directly, NOT `managed/plugins/` — the PluginLoader will crash with `BadImageFormatException` otherwise

## Deadworks Server

### Location

```
<Deadlock>/game/bin/win64/
├── deadworks.exe                        # Server executable
├── managed/DeadworksManaged.Api.dll     # Framework API
├── managed/Google.Protobuf.dll          # Protobuf dependency
└── managed/plugins/                     # Plugin DLLs go here
```

### Starting the Server

**Must be run from the server's own directory:**

```bash
cd "<Deadlock>/game/bin/win64"
./deadworks.exe \
  -dedicated -console -dev -insecure -allow_no_lobby_connect \
  +sv_cheats 1 +tv_enable 0 +hostport 27015 +map dl_midtown
```

**Important flags:**
- Use `+map` not `-map` (the `-map` flag is silently ignored)
- `-allow_no_lobby_connect` is required for direct `connect localhost:27015`
- `-insecure` disables VAC for local development

### Server Lifecycle After Plugin Changes

1. **Kill** the server: `taskkill /f /im deadworks.exe`
2. **Build** the plugin: `dotnet build`
3. **Restart** the server

The server locks plugin DLLs while running — builds will fail or deploy stale code if the server isn't stopped first.

### In-Game Console Commands

- `dw_listplugins` — list loaded plugins
- `dw_reloadplugin <name>` — hot-reload a specific plugin
- `dw_reloadconfig` — reload server configuration

## Two Event Systems

### 1. Plugin Lifecycle Hooks (override from DeadworksPluginBase)

| Hook | Return | Purpose |
|------|--------|---------|
| `OnLoad(bool isReload)` | void | Plugin loaded or hot-reloaded |
| `OnUnload()` | void | Cleanup |
| `OnStartupServer()` | void | New map load |
| `OnPrecacheResources()` | void | Register resources via `Precache.AddResource()` |
| `OnGameFrame(sim, first, last)` | void | Every server frame (prefer Timers) |
| `OnClientConnect(args)` | void | Client connecting |
| `OnClientPutInServer(args)` | void | Client initial connect |
| `OnClientFullConnect(args)` | void | Client fully in-game |
| `OnClientDisconnect(args)` | void | Client dropped |
| `OnChatMessage(message)` | HookResult | Chat intercepted |
| `OnTakeDamage(args)` | HookResult | Damage dealt |
| `OnModifyCurrency(args)` | HookResult | Currency change |
| `OnAddModifier(args)` | HookResult | Modifier applied |
| `OnAbilityAttempt(args)` | void | Set `BlockedButtons` to gate |
| `OnEntityCreated/Spawned/Deleted(args)` | void | Entity lifecycle |
| `OnEntityStartTouch/EndTouch(args)` | void | Trigger/collision |

**HookResult values:** `Continue` (default) → `Changed` (modified args) → `Handled` (consumed) → `Stop` (cancel action)

### 2. Source 2 Game Events

**Declarative** (auto-registered):
```csharp
[GameEventHandler("player_death")]
public HookResult OnPlayerDeath(GameEvent e)
{
    var victim = e.GetPlayerController("userid");
    var weapon = e.GetString("weapon");
    return HookResult.Continue;
}
```

**Dynamic** (runtime):
```csharp
var handle = GameEvents.AddListener("event_name", e => {
    return HookResult.Continue;
});
handle.Cancel(); // Unsubscribe
```

See `Boilerplate/GameEvents.md` for the full event catalog.

## Key API Patterns

```csharp
// Timers (prefer over OnGameFrame)
Timer.Every(100.Milliseconds(), () => { /* tick */ });
Timer.Once(5.Seconds(), () => { /* delayed */ });
Timer.NextTick(() => { /* next frame */ });

// Players
var players = Players.GetAll();
var pawn = controller.Pawn?.As<CCitadelPlayerPawn>();

// Chat
Chat.PrintToChat(controller, "message");
Chat.PrintToChatAll("broadcast");

// Chat commands
[ChatCommand("mycommand")]
public void OnMyCommand(CBasePlayerController controller, CommandInfo info) { }

// Server commands
Server.ExecuteCommand("sv_cheats 1");
Server.MapName; // Current map

// Fire custom events
using var ev = GameEvents.Create("player_chat");
ev?.SetBool("teamonly", false)
   .SetString("text", "hello")
   .Fire();
```

## Build & Deploy

```bash
# Standard build (uses DEADLOCK_GAME_DIR env var)
dotnet build

# Override paths at build time
dotnet build -p:DeadlockDir="F:\SteamLibrary\steamapps\common\Deadlock"

# WSL users — pass WSL-compatible paths
dotnet build -p:DeadlockDir="/mnt/f/SteamLibrary/steamapps/common/Deadlock" \
             -p:DeadlockBin="/mnt/f/SteamLibrary/steamapps/common/Deadlock/game/bin/win64"
```

## Plugin Architecture Best Practices

- **One responsibility per plugin** — keep plugins focused
- **Use Timers** for periodic work, not `OnGameFrame`
- **Clean up in OnUnload** — cancel listeners, dispose resources, timers auto-cleanup
- **Use `[GameEventHandler]`** for declarative events, `GameEvents.AddListener` for dynamic
- **Use `[ChatCommand]`** for player commands
- **Return appropriate HookResult** — `Stop` blocks the action entirely, use with care
- **Native DLLs** deploy to `bin/win64/`, not `managed/plugins/`
- **NuGet packages** — managed deps auto-copy to `managed/plugins/`, but native deps need manual deployment

## Reference Files

- `Boilerplate/Boilerplate.cs` — every overridable hook with inline comments
- `Boilerplate/GameEvents.md` — complete Source 2 event catalog with field types
- `examples/DevMode/` — minimal working plugin (enables cheats, grants resources)
- `docs/architecture.md` — how deadworks bridges native C++ and managed C#
- `docs/api-reference.md` — compact API cheat sheet
