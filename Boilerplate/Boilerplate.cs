using DeadworksManaged.Api;

namespace Boilerplate;

public class Boilerplate : DeadworksPluginBase
{
    public override string Name => "Boilerplate";

    // ---------------------------------------------------------------------
    // Plugin lifecycle hooks (override from DeadworksPluginBase)
    // ---------------------------------------------------------------------

    public override void OnLoad(bool isReload)
    {
        Console.WriteLine($"[{Name}] {(isReload ? "Reloaded" : "Loaded")}!");
    }

    public override void OnUnload()
    {
        Console.WriteLine($"[{Name}] Unloaded!");
    }

    public override void OnStartupServer()
    {
        Console.WriteLine($"[{Name}] Server started (new map load).");
    }

    public override void OnPrecacheResources()
    {
        // Register particles, models, sounds here via Precache.AddResource(...)
    }

    public override void OnClientConnect(ClientConnectEvent args)
    {
        // Return false to reject the connection.
        // args: Slot, Name, Xuid, IsBot, NetworkId, RejectReason (out)
    }

    public override void OnClientPutInServer(ClientPutInServerEvent args)
    {
        Console.WriteLine($"[{Name}] {args.Name} (xuid={args.Xuid}, slot={args.Slot}) put in server.");
    }

    public override void OnClientFullConnect(ClientFullConnectEvent args)
    {
        // Fired once the client is fully in-game and can receive messages.
    }

    public override void OnClientDisconnect(ClientDisconnectedEvent args)
    {
        Console.WriteLine($"[{Name}] Slot {args.Slot} disconnected.");
    }

    public override HookResult OnChatMessage(ChatMessage message)
    {
        // Return HookResult.Stop to swallow the message, Handled to suppress
        // other plugins, or Continue to pass through.
        return HookResult.Continue;
    }

    public override HookResult OnTakeDamage(TakeDamageEvent args)
    {
        // Modify args.DamageAmount / args.DamageType, or return Stop to cancel.
        return HookResult.Continue;
    }

    public override HookResult OnModifyCurrency(ModifyCurrencyEvent args)
    {
        return HookResult.Continue;
    }

    public override HookResult OnAddModifier(AddModifierEvent args)
    {
        return HookResult.Continue;
    }

    public override void OnEntityCreated(EntityCreatedEvent args) { }
    public override void OnEntitySpawned(EntitySpawnedEvent args) { }
    public override void OnEntityDeleted(EntityDeletedEvent args) { }
    public override void OnEntityStartTouch(EntityTouchEvent args) { }
    public override void OnEntityEndTouch(EntityTouchEvent args) { }

    public override void OnAbilityAttempt(AbilityAttemptEvent args)
    {
        // Set args.BlockedButtons to suppress specific ability/item inputs.
    }

    public override void OnGameFrame(bool simulating, bool firstTick, bool lastTick) { }

    // ---------------------------------------------------------------------
    // Source 2 game event handlers
    //
    // Any method tagged with [GameEventHandler("<event_name>")] is registered
    // automatically at plugin load. The full event catalog lives in
    // GameEvents.md alongside this file.
    //
    // Two handler shapes are accepted:
    //   1. HookResult Method(GameEvent e)           - read fields via e.GetInt/GetString/...
    //   2. HookResult Method(MyTypedEvent e)        - typed subclass of GameEvent
    //
    // The examples below use the base GameEvent form to stay framework-only.
    // ---------------------------------------------------------------------

    [GameEventHandler("player_death")]
    public HookResult OnPlayerDeath(GameEvent e)
    {
        var attackerName = e.GetString("attackername");
        var weapon = e.GetString("weapon");
        var headshot = e.GetBool("headshot");
        var victim = e.GetPlayerController("userid");

        Console.WriteLine(
            $"[{Name}] {victim?.PlayerName ?? "?"} killed by {attackerName} " +
            $"with {weapon}{(headshot ? " (HS)" : "")}");
        return HookResult.Continue;
    }

    [GameEventHandler("player_hurt")]
    public HookResult OnPlayerHurt(GameEvent e)
    {
        // Fields: userid, attacker, health, dmg_health, dmg_armor, weapon, hitgroup, type
        return HookResult.Continue;
    }

    [GameEventHandler("player_respawned")]
    public HookResult OnPlayerRespawned(GameEvent e)
    {
        // Fields: userid (player_pawn), facing_yaw (float)
        return HookResult.Continue;
    }

    [GameEventHandler("player_hero_changed")]
    public HookResult OnPlayerHeroChanged(GameEvent e) => HookResult.Continue;

    [GameEventHandler("ability_cast_succeeded")]
    public HookResult OnAbilityCast(GameEvent e)
    {
        // Fields: entindex_ability (long)
        return HookResult.Continue;
    }

    [GameEventHandler("item_pickup")]
    public HookResult OnItemPickup(GameEvent e)
    {
        // Fields: userid (player_controller), item (string)
        return HookResult.Continue;
    }

    [GameEventHandler("game_state_changed")]
    public HookResult OnGameStateChanged(GameEvent e)
    {
        // Field: game_state_new (long) - matches ECitadelGameState
        return HookResult.Continue;
    }

    [GameEventHandler("match_clock")]
    public HookResult OnMatchClock(GameEvent e)
    {
        // Fields: match_time (float), paused (bool)
        return HookResult.Continue;
    }

    // ---------------------------------------------------------------------
    // Dynamic listener example - use when you need to register/remove at
    // runtime instead of at plugin load.
    // ---------------------------------------------------------------------
    private IHandle? _midbossHandle;

    private void RegisterDynamicListeners()
    {
        _midbossHandle = GameEvents.AddListener("midboss_respawn_pending", e =>
        {
            var spawnTime = e.GetFloat("spawn_time");
            Console.WriteLine($"[{Name}] Midboss respawning at {spawnTime}");
            return HookResult.Continue;
        });
    }

    // ---------------------------------------------------------------------
    // Firing a custom event
    // ---------------------------------------------------------------------
    private void FireCustomEvent()
    {
        using var writer = GameEvents.Create("player_chat");
        writer?
            .SetBool("teamonly", false)
            .SetInt("userid", 0)
            .SetString("text", "hello from Boilerplate")
            .Fire();
    }
}
