# Deadworks API Quick Reference

Compact cheat sheet for `DeadworksManaged.Api`. For full hook documentation, see `Boilerplate/Boilerplate.cs`. For all game events, see `Boilerplate/GameEvents.md`.

## Plugin Base Class

```csharp
using DeadworksManaged.Api;

namespace MyPlugin;

public class MyPlugin : DeadworksPluginBase
{
    public override string Name => "MyPlugin";

    public override void OnLoad(bool isReload) { }
    public override void OnUnload() { }
}
```

## Lifecycle Hooks

```csharp
// Server
public override void OnStartupServer() { }
public override void OnPrecacheResources() { }
public override void OnGameFrame(bool simulating, bool firstTick, bool lastTick) { }

// Client connection
public override void OnClientConnect(ClientConnectEvent args) { }
public override void OnClientPutInServer(ClientPutInServerEvent args) { }
public override void OnClientFullConnect(ClientFullConnectEvent args) { }
public override void OnClientDisconnect(ClientDisconnectedEvent args) { }

// Gameplay (return HookResult)
public override HookResult OnChatMessage(ChatMessage message) => HookResult.Continue;
public override HookResult OnTakeDamage(TakeDamageEvent args) => HookResult.Continue;
public override HookResult OnModifyCurrency(ModifyCurrencyEvent args) => HookResult.Continue;
public override HookResult OnAddModifier(AddModifierEvent args) => HookResult.Continue;

// Abilities
public override void OnAbilityAttempt(AbilityAttemptEvent args) { }

// Entities
public override void OnEntityCreated(EntityCreatedEvent args) { }
public override void OnEntitySpawned(EntitySpawnedEvent args) { }
public override void OnEntityDeleted(EntityDeletedEvent args) { }
public override void OnEntityStartTouch(EntityTouchEvent args) { }
public override void OnEntityEndTouch(EntityTouchEvent args) { }
```

## HookResult

```csharp
HookResult.Continue   // Pass through (default)
HookResult.Changed    // Modified args, continue processing
HookResult.Handled    // Consumed, other plugins still see it
HookResult.Stop       // Cancel the action entirely
```

## Game Events

### Declarative (auto-registered at plugin load)

```csharp
[GameEventHandler("player_death")]
public HookResult OnPlayerDeath(GameEvent e)
{
    var victim     = e.GetPlayerController("userid");
    var attacker   = e.GetPlayerController("attacker");
    var weapon     = e.GetString("weapon");
    var headshot   = e.GetBool("headshot");
    return HookResult.Continue;
}

[GameEventHandler("player_respawned")]
public HookResult OnRespawn(GameEvent e)
{
    var pawn = e.GetPlayerPawn("userid");
    return HookResult.Continue;
}
```

### Dynamic (runtime registration)

```csharp
// Register
IHandle handle = GameEvents.AddListener("midboss_respawn_pending", e =>
{
    float t = e.GetFloat("spawn_time");
    return HookResult.Continue;
});

// Unregister
handle.Cancel();  // or handle.Dispose();
```

### Fire a custom event

```csharp
using var ev = GameEvents.Create("player_chat");
ev?.SetBool("teamonly", false)
   .SetInt("userid", 0)
   .SetString("text", "hello")
   .Fire(dontBroadcast: false);
```

### GameEvent Accessors

```csharp
e.GetString("field")            // string
e.GetBool("field")              // bool
e.GetInt("field")               // int (byte/short/int/long all widen)
e.GetFloat("field")             // float
e.GetUint64("field")            // ulong (SteamID64 etc.)
e.GetPlayerController("field")  // CBasePlayerController?
e.GetPlayerPawn("field")        // CBasePlayerPawn?
e.GetEHandle("field")           // CBaseEntity?
```

## Chat Commands

```csharp
[ChatCommand("hello")]
public void OnHello(CBasePlayerController controller, CommandInfo info)
{
    Chat.PrintToChat(controller, $"Hello, {controller.PlayerName}!");
}

// Usage in game chat: !hello
```

## Timers

```csharp
// One-shot delayed execution
Timer.Once(5.Seconds(), () => {
    Console.WriteLine("5 seconds later!");
});

// Recurring timer
Timer.Every(100.Milliseconds(), () => {
    // Runs every 100ms
});

// Next server frame
Timer.NextTick(() => {
    // Runs on the next frame
});

// Sequence of timed actions
Timer.Sequence()
    .Then(1.Seconds(), () => Console.WriteLine("Step 1"))
    .Then(2.Seconds(), () => Console.WriteLine("Step 2"))
    .Start();
```

Timers are per-plugin and auto-cleanup on `OnUnload()`.

## Players

```csharp
// Get all connected players
var players = Players.GetAll();

// Iterate players
foreach (var controller in Players.GetAll())
{
    var name = controller.PlayerName;
    var pawn = controller.Pawn?.As<CCitadelPlayerPawn>();

    if (pawn != null)
    {
        pawn.Level = 36;
        pawn.SetCurrency(ECurrencyType.EGold, 99999);
        pawn.SetCurrency(ECurrencyType.EAbilityPoints, 36);
    }
}
```

## Chat

```csharp
// Message to one player
Chat.PrintToChat(controller, "Private message");

// Broadcast to all
Chat.PrintToChatAll("Server-wide message");
```

## Server

```csharp
// Execute a console command
Server.ExecuteCommand("sv_cheats 1");
Server.ExecuteCommand("mp_restartgame 1");

// Current map name
string map = Server.MapName;
```

## Precaching Resources

```csharp
public override void OnPrecacheResources()
{
    Precache.AddResource("particles/my_particle.vpcf");
    Precache.AddResource("models/my_model.vmdl");
    Precache.AddResource("sounds/my_sound.vsnd");
}
```

## Entity Casting

```csharp
// Cast a base entity to a specific type
var heroPawn = basePawn?.As<CCitadelPlayerPawn>();
var controller = entity?.As<CBasePlayerController>();
```

## Common Game Events

| Event | Key Fields |
|-------|-----------|
| `player_death` | userid, attacker, weapon, headshot, bounty |
| `player_hurt` | userid, attacker, dmg_health, weapon, hitgroup |
| `player_respawned` | userid (pawn), facing_yaw |
| `player_hero_changed` | userid (pawn) |
| `ability_cast_succeeded` | entindex_ability |
| `item_pickup` | userid (controller), item |
| `game_state_changed` | game_state_new |
| `match_clock` | match_time, paused |
| `player_level_changed` | userid (pawn), new_player_level |
| `midboss_respawn_pending` | spawn_time |
| `player_connect` | name, xuid, bot |
| `player_disconnect` | userid, reason, name |

See `Boilerplate/GameEvents.md` for the complete catalog of 60+ events with all field definitions.
