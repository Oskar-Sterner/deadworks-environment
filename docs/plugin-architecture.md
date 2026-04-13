# Recommended Plugin Architecture

Best practices for structuring Deadworks plugins, derived from production plugins. Follow this guide when your plugin grows beyond a single file.

## When to Use This

- **Single-file plugins** (like DevMode) are fine for simple things — just a `.cs` and `.csproj`
- **Once you have 2+ concerns** (commands, HUD, database, game logic), split into folders

## Recommended Folder Structure

```
MyPlugin/
├── MyPlugin.csproj              # Project file
├── MyPluginPlugin.cs            # Main plugin class (lifecycle, delegation)
├── Commands/
│   └── ChatCommands.cs          # All chat command handlers
├── Hud/
│   └── MyHud.cs                 # HUD rendering (billboard text entities)
├── Logic/
│   └── GameEngine.cs            # Core game logic / state machine
├── Data/
│   ├── MyPluginDb.cs            # Database connection + schema
│   └── Migrations/
│       └── 001_initial.sql      # SQL schema (idempotent)
├── Repositories/
│   └── RecordRepository.cs      # Data access (SQL hidden here)
└── Models/
    ├── MyRecord.cs              # Immutable data models (records)
    └── MyState.cs               # State enums
```

Adapt to your needs — not every plugin needs a database or HUD. The key principle is **one folder = one responsibility**.

## Core Principles

### 1. Main Plugin Class = Orchestrator Only

Your main plugin class should wire up components and delegate. It should NOT contain business logic, SQL, or rendering code.

```csharp
public class MyPluginPlugin : DeadworksPluginBase
{
    public override string Name => "MyPlugin";

    private MyPluginDb _db = null!;
    private RecordRepository _records = null!;
    private GameEngine _engine = null!;
    private ChatCommands _commands = null!;
    private MyHud _hud = null!;

    public override void OnLoad(bool isReload)
    {
        // 1. Database
        var dbPath = Path.Combine("managed", "plugins", "myplugin.db");
        _db = MyPluginDb.Open(dbPath);

        // 2. Repositories
        _records = new RecordRepository(_db.Connection);

        // 3. Core logic
        _engine = new GameEngine();

        // 4. Commands (inject dependencies)
        _commands = new ChatCommands(_engine, _records);

        // 5. HUD
        _hud = new MyHud();

        // 6. Tick timer (prefer over OnGameFrame)
        Timer.Every(100.Milliseconds(), Tick);
    }

    public override void OnUnload()
    {
        _hud.DestroyAll();
        _db.Dispose();
    }

    // Lifecycle hooks delegate to components
    [ChatCommand("mycommand")]
    public void OnMyCommand(CBasePlayerController c, CommandInfo i)
        => _commands.HandleMyCommand(c, i);

    private void Tick()
    {
        foreach (var controller in Players.GetAll())
        {
            _engine.Tick(controller);
            _hud.Update(controller);
        }
    }
}
```

### 2. Constructor-Based Dependency Injection

Every class declares what it needs in its constructor. No global statics, no service locators.

```csharp
public sealed class ChatCommands
{
    private readonly GameEngine _engine;
    private readonly RecordRepository _records;

    public ChatCommands(GameEngine engine, RecordRepository records)
    {
        _engine = engine;
        _records = records;
    }

    public void HandleMyCommand(CBasePlayerController controller, CommandInfo info)
    {
        // Use _engine and _records here
    }
}
```

**Why**: Dependencies are explicit, code is testable, data flow is traceable.

### 3. Repository Pattern for Database Access

SQL stays in repository classes. Business logic never touches SQL directly.

```csharp
public sealed class RecordRepository
{
    private readonly SqliteConnection _conn;

    public RecordRepository(SqliteConnection conn) => _conn = conn;

    public void Upsert(ulong steamId, string map, int timeMs)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO records (steam_id, map, time_ms, created_at)
            VALUES ($steamId, $map, $timeMs, unixepoch())
            ON CONFLICT (steam_id, map) DO UPDATE
            SET time_ms = $timeMs, created_at = unixepoch()
            WHERE $timeMs < records.time_ms
            """;
        cmd.Parameters.AddWithValue("$steamId", (long)steamId);
        cmd.Parameters.AddWithValue("$map", map);
        cmd.Parameters.AddWithValue("$timeMs", timeMs);
        cmd.ExecuteNonQuery();
    }

    public Record? GetBest(string map)
    {
        // ...
    }
}
```

### 4. Immutable Data Models

Use C# `record` types for data that flows between components.

```csharp
// Immutable — prevents accidental mutation
public sealed record Zone(
    string Map,
    Vector3 Min,
    Vector3 Max,
    long UpdatedAtUnix)
{
    public bool Contains(Vector3 p, float margin = 20f) =>
        p.X >= Min.X - margin && p.X <= Max.X + margin &&
        p.Y >= Min.Y - margin && p.Y <= Max.Y + margin &&
        p.Z >= Min.Z - margin && p.Z <= Max.Z + margin;
}

// Lightweight result types
public readonly record struct FinishedRun(int Slot, int ElapsedMs);
```

### 5. State Machines for Game Logic

Model player states as explicit enums with a clean state machine.

```csharp
public enum RunState { Idle, InStart, Running, Finished }

public sealed class PlayerRun
{
    public RunState State { get; set; } = RunState.Idle;
    public long StartTickMs { get; set; }
}

public sealed class GameEngine
{
    private readonly Dictionary<int, PlayerRun> _runs = new();

    public FinishedRun? Tick(int slot, Vector3 position, long nowMs)
    {
        var run = _runs.GetOrAdd(slot);

        return run.State switch
        {
            RunState.Idle when inStartZone =>
                SetState(run, RunState.InStart),

            RunState.InStart when !inStartZone =>
                StartRun(run, nowMs),

            RunState.Running when inEndZone =>
                FinishRun(slot, run, nowMs),

            _ => null
        };
    }
}
```

### 6. Slot-Based Player State

Track per-player state using slot IDs (entity index - 1). These are deterministic and persistent per connection.

```csharp
private readonly Dictionary<int, PlayerRun> _runs = new();
private readonly Dictionary<int, ulong> _slotToSteamId = new();

public override void OnClientPutInServer(ClientPutInServerEvent args)
{
    _slotToSteamId[args.Slot] = args.Xuid;
    _runs[args.Slot] = new PlayerRun();
}

public override void OnClientDisconnect(ClientDisconnectedEvent args)
{
    _slotToSteamId.Remove(args.Slot);
    _runs.Remove(args.Slot);
}
```

### 7. Timer-Based Ticking (Not OnGameFrame)

Use `Timer.Every(100.Milliseconds(), ...)` instead of `OnGameFrame` for periodic work:

- Runs at predictable 10 Hz (not frame-rate dependent)
- Avoids thread starvation during client connect storms
- Lower overhead than per-frame callbacks
- 100ms max latency is fine for most gameplay logic

Reserve `OnGameFrame` only for things that genuinely need frame-perfect timing.

### 8. HUD Entity Lifecycle

When creating world text entities for HUD, handle entity loss gracefully:

```csharp
public sealed class MyHud
{
    private readonly Dictionary<int, CPointWorldText?> _entities = new();

    public void Update(int slot, CCitadelPlayerPawn pawn, string text)
    {
        var entity = _entities.GetValueOrDefault(slot);

        // Entity may have been destroyed (player death, etc.) — recreate
        if (entity == null || !entity.IsValid)
        {
            entity = CPointWorldText.Create(
                message: text,
                position: pawn.Position + new Vector3(0, 0, 120f),
                fontSize: 60f,
                worldUnitsPerPx: 0.12f,
                reorientMode: 1); // Billboard
            _entities[slot] = entity;
        }
        else
        {
            entity.SetMessage(text);
            entity.Position = pawn.Position + new Vector3(0, 0, 120f);
        }
    }

    public void DestroyAll()
    {
        foreach (var entity in _entities.Values)
            entity?.Destroy();
        _entities.Clear();
    }
}
```

### 9. Database Setup Pattern

Idempotent schema application — safe for restarts and hot-reloads:

```csharp
public sealed class MyPluginDb : IDisposable
{
    public SqliteConnection Connection { get; }

    private MyPluginDb(SqliteConnection conn) => Connection = conn;

    public static MyPluginDb Open(string path)
    {
        var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode = WAL;";
        cmd.ExecuteNonQuery();

        ApplySchema(conn);
        return new MyPluginDb(conn);
    }

    private static void ApplySchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS records (
                steam_id INTEGER NOT NULL,
                map      TEXT NOT NULL,
                time_ms  INTEGER NOT NULL,
                created_at INTEGER NOT NULL,
                PRIMARY KEY (steam_id, map)
            );
            CREATE INDEX IF NOT EXISTS idx_records_leaderboard
                ON records (map, time_ms);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => Connection.Dispose();
}
```

### 10. Chat Commands as Thin Wrappers

Commands should validate input and delegate to business logic. Keep them short.

```csharp
public sealed class ChatCommands
{
    private readonly GameEngine _engine;
    private readonly RecordRepository _records;

    public ChatCommands(GameEngine engine, RecordRepository records)
    {
        _engine = engine;
        _records = records;
    }

    public void HandleReset(CBasePlayerController controller)
    {
        _engine.Reset(controller.Slot);
        Chat.PrintToChat(controller, "Timer reset.");
    }

    public void HandleTop(CBasePlayerController controller, string map)
    {
        var top = _records.GetTop(map, 10);
        foreach (var (i, r) in top.Select((r, i) => (i, r)))
            Chat.PrintToChat(controller, $"#{i + 1}: {r.PlayerName} — {r.TimeMs}ms");
    }
}
```

## NuGet + Native DLL Deployment

When your plugin uses NuGet packages with native dependencies (e.g. SQLite):

```xml
<!-- In your .csproj -->
<PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.0" />

<Target Name="DeployToGame" AfterTargets="Build">
  <ItemGroup>
    <!-- Your plugin -->
    <DeployFiles Include="$(OutputPath)MyPlugin.dll;$(OutputPath)MyPlugin.pdb" />
    <!-- Managed NuGet deps → plugins/ folder -->
    <PluginDeps Include="$(OutputPath)Microsoft.Data.Sqlite.dll" />
    <PluginDeps Include="$(OutputPath)SQLitePCLRaw.*.dll" />
    <!-- NATIVE DLLs → bin/win64/ root (NOT plugins/) -->
    <NativeDeps Include="$(OutputPath)runtimes\win-x64\native\e_sqlite3.dll" />
  </ItemGroup>

  <Copy SourceFiles="@(DeployFiles);@(PluginDeps)"
        DestinationFolder="$(DeadlockBin)\managed\plugins"
        SkipUnchangedFiles="false" Retries="0" ContinueOnError="WarnAndContinue" />

  <Copy SourceFiles="@(NativeDeps)"
        DestinationFolder="$(DeadlockBin)"
        SkipUnchangedFiles="false" Retries="0" ContinueOnError="WarnAndContinue" />
</Target>
```

**Critical**: Native DLLs in `managed/plugins/` cause `BadImageFormatException`. The plugin loader tries to load them as .NET assemblies and crashes. Always deploy native DLLs to `bin/win64/` directly.

## Summary

| Principle | Why |
|-----------|-----|
| Main class = orchestrator | Easy to understand entry point, no buried logic |
| Constructor injection | Explicit deps, testable, traceable |
| Repository pattern | SQL isolated, business logic is SQL-free |
| Immutable records | No accidental mutations, safer data flow |
| State machines | Explicit states, no boolean soup |
| Slot-based tracking | Deterministic, persistent per connection |
| Timer ticking (100ms) | Predictable, lower overhead than per-frame |
| Resilient HUD entities | Handle entity loss from deaths/reconnects |
| Idempotent schemas | Safe for restarts and hot-reloads |
| Thin chat commands | Validate and delegate, no business logic |
