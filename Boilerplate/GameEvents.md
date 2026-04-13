# Deadlock Plugin Events Reference

Complete reference for writing Deadworks-managed plugins against Deadlock's
Source 2 event system. Covers plugin lifecycle hooks, the Source 2 game-event
listener API, and every event definition shipped in the retail client.

Sources:
- `game/citadel/pak01_dir/resource/game.gameevents` (Citadel-specific events)
- `game/core/pak01_dir/resource/core.gameevents` (Source 2 core events)
- `DeadworksManaged.Api/IDeadworksPlugin.cs` / `DeadworksPluginBase.cs`

---

## 1. Two event systems, one plugin

Deadworks exposes **two** distinct event mechanisms. Most plugins use both.

### 1a. Plugin lifecycle hooks
Override `virtual` methods on `DeadworksPluginBase`. These are high-level C++
callbacks bridged into managed code — they are *not* Source 2 game events, so
they always fire regardless of any `.gameevents` registration.

| Hook | Signature | Purpose |
|---|---|---|
| `OnLoad(bool isReload)` | `void` | Plugin (or hot-reload) loaded |
| `OnUnload()` | `void` | Clean up hooks, timers, listeners |
| `OnPrecacheResources()` | `void` | Call `Precache.AddResource(...)` |
| `OnStartupServer()` | `void` | New map load |
| `OnGameFrame(sim, first, last)` | `void` | Every server frame |
| `OnConfigReloaded()` | `void` | After `dw_reloadconfig` |
| `OnSignonState(ref string addons)` | `void` | Modify addons string on signon |
| `OnClientConnect(ClientConnectEvent)` | `bool` | Return `false` to reject |
| `OnClientPutInServer(ClientPutInServerEvent)` | `void` | Initial connect |
| `OnClientFullConnect(ClientFullConnectEvent)` | `void` | Fully in-game |
| `OnClientDisconnect(ClientDisconnectedEvent)` | `void` | Client dropped |
| `OnClientConCommand(ClientConCommandEvent)` | `HookResult` | Stop = block cmd |
| `OnChatMessage(ChatMessage)` | `HookResult` | Stop = swallow chat |
| `OnTakeDamage(TakeDamageEvent)` | `HookResult` | Stop = cancel damage |
| `OnModifyCurrency(ModifyCurrencyEvent)` | `HookResult` | Stop = block change |
| `OnAddModifier(AddModifierEvent)` | `HookResult` | Stop = block modifier |
| `OnAbilityAttempt(AbilityAttemptEvent)` | `void` | Set `BlockedButtons` to gate |
| `OnProcessUsercmds(ProcessUsercmdsEvent)` | `void` | Rewrite usercmds |
| `OnEntityCreated(EntityCreatedEvent)` | `void` | Before spawn |
| `OnEntitySpawned(EntitySpawnedEvent)` | `void` | Post-spawn |
| `OnEntityDeleted(EntityDeletedEvent)` | `void` | On destroy |
| `OnEntityStartTouch(EntityTouchEvent)` | `void` | Trigger entry / collision |
| `OnEntityEndTouch(EntityTouchEvent)` | `void` | Trigger exit |

`HookResult`: `Continue` (default) → `Changed` → `Handled` → `Stop`. Higher
values win across plugins; `Stop` cancels the engine action.

### 1b. Source 2 game events
Fired by the engine via `IGameEventManager2`. Subscribe two ways:

**Declarative** — auto-registered on plugin load:
```csharp
[GameEventHandler("player_death")]
public HookResult OnPlayerDeath(GameEvent e) {
    var victim   = e.GetPlayerController("userid");
    var weapon   = e.GetString("weapon");
    var headshot = e.GetBool("headshot");
    return HookResult.Continue;
}
```

**Dynamic** — register/cancel at runtime:
```csharp
IHandle handle = GameEvents.AddListener("midboss_respawn_pending", e => {
    float t = e.GetFloat("spawn_time");
    return HookResult.Continue;
});
handle.Cancel();            // or .Dispose()
```

**Fire a new event** (`GameEvents.Create` returns a `GameEventWriter`):
```csharp
using var ev = GameEvents.Create("player_chat");
ev?.SetBool("teamonly", false)
  .SetInt("userid", slot)
  .SetString("text", "hello")
  .Fire(dontBroadcast: false);
```

The `GameEvent` reader exposes `GetBool / GetInt / GetFloat / GetString /
GetUint64 / GetPlayerController / GetPlayerPawn / GetEHandle`.

Typed handlers are also accepted: declare a subclass of `GameEvent` with
strongly-typed properties and take it as the handler parameter — the
PluginLoader dispatches only when `IsInstanceOfType` matches.

### Field type legend (from `.gameevents` files)

| Type | Read via | Notes |
|---|---|---|
| `string` | `GetString` | UTF-8 |
| `bool` | `GetBool` | |
| `byte` / `short` / `int` / `long` | `GetInt` | All widen to `int` |
| `uint64` | `GetUint64` | SteamID64 etc. |
| `float` | `GetFloat` | |
| `ehandle` | `GetEHandle` | Resolved to `CBaseEntity` |
| `player_controller` | `GetPlayerController` | Resolves from userid |
| `player_pawn` | `GetPlayerPawn` | Resolves from pawn handle |
| `player_controller_and_pawn` | both | Either accessor works |
| `local` | — | Not networked to clients |

---

## 2. Citadel game events (`game.gameevents`)

Deadlock-specific events. Fields listed as `name (type) - note`.

### UI / spectator
- **gameui_activated** — (no fields)
- **gameui_hidden** — (no fields)
- **gameui_free_cursor_changed** — (no fields)
- **spectate_fow_view_team_changed** — (no fields)
- **spectate_mode_changed** — not networked
- **spectate_team_changed** — not networked
- **spectate_home_team_changed** — (no fields)
- **keybind_changed** — (no fields)
- **quick_cast_mode_changed** — (no fields)
- **tools_content_changed** — (no fields)
- **item_file_reloaded** — (no fields)
- **player_info_individual_updated** — `account_id (long)`
- **persona_updated** — `SteamID (uint64)`, `force_update (bool)`
- **party_updated** — (no fields)

### Connection / session
- **client_disconnect** — `reason_code (int)`, `reason_desc (string)`
- **client_player_currency_change** — `userid (player_pawn)`
- **client_player_hero_changed** — `userid (player_pawn)`
- **bot_player_replace** — `bot (short)`, `player (short)`
- **player_bot_replace** — `player (short)`, `bot (short)`

### Combat
- **player_death** — `userid (player_controller_and_pawn)`, `entityid (long)`, `attacker (player_controller_and_pawn)`, `attackername (string)`, `attackerehandle (ehandle)`, `weapon (string)`, `headshot (bool)`, `attackerisbot (bool)`, `victimname (string)`, `victimisbot (bool)`, `abort (bool)`, `type (long)`, `victim_x/y/z (float)`, `bounty (short)`, `dropped_gold (short)`, `assister1..5controller (player_controller)`
- **player_hurt** — *not networked* — `userid`, `attacker`, `attackerentid (long)`, `health (short)`, `armor (byte)`, `weapon (string)`, `dmg_health (short)`, `dmg_armor (byte)`, `hitgroup (byte)`, `type (long)`
- **player_respawned** — `userid (player_pawn)`, `facing_yaw (float)`
- **player_rez_incoming** — `entindex_player_rezzer/rezzee (long)`, `victim_x/y/z (float)`
- **player_damage_increased** — `entindex_player (long)`, `damage_increase (long)`
- **player_maxhealth_increased** — `entindex_player (long)`, `maxhealth_increase (long)`
- **player_ammo_increased** — `entindex_player (long)`, `ammo_increase (long)`
- **player_ammo_full** — `entindex_player (long)`
- **player_healed** — `entindex_healer/healed (long)`, `heal_amount (long)`, `abilityid_healing_source (long)`
- **player_heal_prevented** — `entindex_attacker/victim (long)`, `prevented_amount (long)`, `abilityid_source (long)`
- **player_given_shield** — `entindex_provider/target`, `bullet_shield_amount/health/health_max`, `tech_shield_amount/health/health_max`, `abilityid_source` (all `long`)
- **player_given_barrier** — `entindex_provider/target (long)`, `barrier_amount (long)`, `abilityid_source (long)`
- **broke_enemy_shield** — `entindex_victim (long)`, `entindex_inflictor (long)`
- **gameover_msg** — *not networked* — `winning_team (byte)`

### Abilities / weapons
- **player_used_ability** — `player (player_pawn)`, `caster (ehandle)`, `abilityname (string)`, `annotation (string)`
- **player_used_item** — `userid_caster (player_pawn)`, `abilityid_used (long)`
- **non_player_used_ability** — `caster (ehandle)`, `abilityname (string)`
- **ability_cast_succeeded** — `entindex_ability (long)`
- **ability_cast_failed** — `entindex_ability (long)`, `reason (long)`
- **ability_cooldown_end_changed** — `entindex_ability (long)`, `old_value (float)`, `new_value (float)`
- **local_player_ability_cooldown_end_changed** — `entindex_ability (long)`
- **ability_added** — `userid (player_pawn)`, `ability (ehandle)`
- **ability_removed** — `userid (player_pawn)`, `ability (ehandle)`
- **ability_level_changed** — `userid (player_pawn)`, `ability (ehandle)`, `abilitylevel (long)`
- **player_ability_upgraded** — `userid (player_pawn)`, `ability (ehandle)`
- **local_player_abilities_vdata_changed** — `ability (ehandle)`
- **player_data_abilities_changed** — `userid (player_pawn)`
- **player_data_ability_slot_changed** — `userid (player_pawn)`
- **player_ability_bonus_counter_changed** — `userid (player_pawn)`
- **player_stolen_ability_changed** — `userid (player_pawn)`
- **weapon_reload_started** — `entindex_player (long)`, `bullets_left_in_clip (int)`
- **weapon_reload_complete** — `entindex_player (long)`
- **weapon_zoom_started** — `entindex_player (long)`
- **reload_failed_no_ammo** — (no fields)
- **player_weapon_switched** — `entindex_player (long)`, `viewmodelindex (long)`
- **local_player_weapons_changed** — (no fields)
- **local_player_shot_hit** — (no fields)

### Items / shop / currency
- **item_pickup** — `userid (player_controller)`, `item (string)`
- **player_items_changed** — `userid (player_pawn)`
- **player_item_price_changed** — `userid (player_pawn)`
- **player_ability_upgrade_sell_price_changed** — `userid (player_pawn)`
- **player_quickbuy_items_changed** — `userid (player_pawn)`
- **player_opened_item_shop** — `userid (player_pawn)`
- **player_closed_item_shop** — `userid (player_pawn)`
- **player_shop_zone_changed** — (no fields)
- **player_hideout_zone_changed** — (no fields)
- **currency_missed** — `entindex_player (long)`, `type (short)`, `amount (long)`
- **currency_denied** — `userid (player_pawn)`, `type (short)`, `amount (long)`, `is_denier (bool)`, `pos_x/y/z (float)`, `entindex_orb (long)`
- **currency_claimed_display** — `entindex_player (long)`, `type (short)`, `millisecondTime (long)`, `is_denier (bool)`, `pos_x/y/z (float)`, `entindex_orb (long)`

### Hero / player state
- **player_hero_changed** — `userid (player_pawn)`
- **player_hero_reset** — (no fields)
- **player_opened_hero_select** — `entindex_player (long)`
- **player_drafting_changed** — (no fields)
- **player_guided_sandbox_started** — (no fields)
- **player_level_changed** — `userid (player_pawn)`, `new_player_level (short)`
- **player_respawn_time_changed** — `userid (player_pawn)`
- **player_stats_changed** — `userid (player_pawn)`
- **player_modifiers_changed** — `entindex_player (long)`, `modifier_index (long)`
- **player_chat_group_changed** — *not networked* — `player (player_controller)`
- **hero_assigned_lane_changed** — `player (player_controller)`
- **hero_draft_order_changed** — (no networked fields)
- **citadel_hint_changed** — `hint_feature (long)`
- **local_player_unit_states_changed** — (no fields)

### Map / match state
- **game_state_changed** — `game_state_new (long)` *(ECitadelGameState)*
- **match_clock** — `match_time (float)`, `paused (bool)`
- **midboss_respawn_pending** — `spawn_time (float)`
- **midboss_respawned** — (no fields)
- **crate_spawn** — `early (bool)`, `spawn_time (float)`, `spawn_location (short)`
- **crate_spawn_notification** — `spawn_time (float)`
- **recalculate_ziplines** — (no fields)
- **street_brawl_state_changed** — (no fields)
- **lane_test_state_updated** — `running (bool)`
- **citadel_pause_event** — `userid (player_controller)`, `value (short)`, `message (short)`
- **citadel_pregame_timer** — `value (short)`
- **sandbox_player_moved** — (no fields)
- **break_piece_spawned** — `entindex (long)`, `is_rigid (bool)`

### Zipline / movement / misc
- **zipline_player_attached** — `userid (player_pawn)`
- **zipline_player_detached** — `userid (player_pawn)`
- **grenade_bounce** — `userid (player_pawn)`
- **player_ball_juggle** — `entindex_last_juggler (long)`, `entindex_ball (long)`, `num_juggles (int)`, `juggle_event_type (int)`

### CitadelTV (spectator/director)
- **citadeltv_chase_hero** — `target1/2 (ehandle)`, `type (byte)`, `priority (short)`, `gametime (float)`, `highlight (bool)`, `target1/2playerid (player_controller)`, `eventtype (short)`
- **citadeltv_unit_event** — `victim (short)`, `attacker (short)`, `basepriority (short)`, `priority (short)`, `eventtype (short)` *(ECitadelSpectateEvent)*

---

## 3. Source 2 core events (`core.gameevents`)

Engine-level events inherited from Source 2. Most still fire under Deadlock.

### Server lifecycle
- **server_spawn** — `hostname`, `address (string)`, `port (short)`, `game`, `mapname`, `addonname (string)`, `maxplayers (long)`, `os`, `dedicated (bool)`, `password (bool)`
- **server_pre_shutdown** — `reason (string)`
- **server_shutdown** — `reason (string)`
- **server_message** — `text (string)`
- **server_cvar** — `cvarname (string)`, `cvarvalue (string)`
- **hostname_changed** — `hostname (string)`
- **map_shutdown / map_transition** — (no fields)
- **game_newmap** — `mapname (string)`, `transition (bool)`
- **game_message** — `target (byte)`, `text (string)` — target 0=console, 1=HUD
- **difficulty_changed** — `newDifficulty (short)`, `oldDifficulty (short)`, `strDifficulty (string)`

### Player session
- **player_activate** — `userid (player_controller)`
- **player_connect** — `name`, `userid`, `networkid`, `xuid (uint64)`, `bot (bool)`
- **player_connect_full** — `userid (player_controller)`
- **player_full_update** — `userid`, `count (short)`
- **player_disconnect** — `userid`, `reason (short)`, `name`, `networkid`, `xuid`, `PlayerID (short)`
- **player_info** — `name`, `userid`, `steamid (uint64)`, `bot (bool)`
- **player_spawn** — `userid (player_controller_and_pawn)`
- **player_team** — `userid`, `team (byte)`, `oldteam (byte)`, `disconnect (bool)`, `silent (bool)`, `name`, `isbot (bool)`
- **player_changename** — `userid`, `oldname`, `newname`
- **player_hurt** — `userid`, `attacker`, `health (byte)` *(core variant; citadel has richer one)*
- **player_chat** — `teamonly (bool)`, `userid`, `playerid (short)`, `text (string)`
- **player_footstep** — `userid (player_pawn)`
- **player_hintmessage** — `hintmessage (string)`
- **player_stats_updated** — `forceupload (bool)`
- **player_death** — `userid`, `attacker` *(core variant)*
- **local_player_team / local_player_controller_team / local_player_pawn_changed** — (no fields)

### Round / team
- **round_start** — `timelimit (long)`, `fraglimit (long)`, `objective (string)`
- **round_end** — `winner (byte)`, `reason (byte)`, `message (string)`, `time (float)`
- **round_start_pre_entity / round_start_post_nav / round_freeze_end** — (no fields)
- **teamplay_round_start** — `full_reset (bool)`
- **teamplay_broadcast_audio** — `team (byte)`, `sound (string)`
- **finale_start** — `rushes (short)`
- **team_info** — `teamid (byte)`, `teamname (string)`
- **team_score** — `teamid (byte)`, `score (short)`

### Voting
- **vote_started** — `issue (string)`, `param1`, `votedata`, `team (byte)`, `initiator (long)`
- **vote_failed** — `team (byte)`
- **vote_passed** — `details`, `param1`, `team (byte)`
- **vote_changed** — `yesVotes (byte)`, `noVotes (byte)`, `potentialVotes (byte)`
- **vote_cast_yes / vote_cast_no** — `team (byte)`, `entityid (long)`

### Achievements / inventory
- **achievement_event** — `achievement_name (string)`, `cur_val (short)`, `max_val (short)`
- **achievement_earned** — `player (player_controller)`, `achievement (short)`
- **achievement_write_failed** — (no fields)
- **bonus_updated** — `numadvanced/bronze/silver/gold (short)`
- **inventory_updated** — `itemdef (short)`, `itemid (long)`
- **cart_updated / store_pricesheet_updated / item_schema_initialized / drop_rate_modified / event_ticket_modified / gc_connected** — (no fields)

### Entities / world
- **entity_killed** — `entindex_killed/attacker/inflictor (long)`, `damagebits (long)`
- **entity_visible** — `userid`, `subject (long)`, `classname`, `entityname`
- **break_breakable / broken_breakable** — `entindex (long)`, `userid (player_pawn)`, `material (byte)`
- **break_prop** — `entindex`, `userid`, `player_held`, `player_thrown`, `player_dropped`
- **ragdoll_dissolved** — `entindex (long)`
- **door_close** — `userid (player_pawn)`, `checkpoint (bool)`
- **flare_ignite_npc** — `entindex (long)`
- **helicopter_grenade_punt_miss** — (no fields)
- **physgun_pickup** — `target (ehandle)`
- **dynamic_shadow_light_changed** — (no fields)

### Spectator / HLTV / CitadelTV
- **spec_target_updated / spec_mode_updated** — `userid (player_controller_and_pawn)`, `target (ehandle)`
- **hltv_cameraman** — `userid`
- **hltv_chase** — `target1`, `target2`, `distance (short)`, `theta/phi (short)`, `inertia (byte)`, `ineye (byte)`
- **hltv_rank_camera** — `index (byte)`, `rank (float)`, `target`
- **hltv_rank_entity** — `userid`, `rank (float)`, `target`
- **hltv_fixed** — `posx/y/z (long)`, `theta/phi (short)`, `offset (short)`, `fov (float)`, `target`
- **hltv_message** — `text`
- **hltv_status** — `clients (long)`, `slots (long)`, `proxies (short)`, `master (string)`
- **hltv_title / hltv_chat** — `text`, (+ `steamID` for chat)
- **hltv_versioninfo** — `version (long)`
- **hltv_replay** — `delay (long)`, `reason (long)` *(ReplayEventType_t)*
- **hltv_replay_status** — `reason (long)`

### Instructor / tutorial
- **gameinstructor_draw / gameinstructor_nodraw** — (no fields)
- **clientside_lesson_closed** — `lesson_name (string)`
- **set_instructor_group_enabled** — `group (string)`, `enabled (short)`
- **instructor_start_lesson** — `userid`, `hint_name`, `hint_target (long)`, `vr_movement_type/controller_type (byte)`, `vr_single_controller (bool)`
- **instructor_close_lesson** — `userid`, `hint_name`
- **instructor_server_hint_create** — `userid`, `hint_entindex (long)`, `hint_name`, `hint_replace_key`, `hint_target (long)`, `hint_activator_userid`, `hint_timeout (short)`, `hint_icon_onscreen/offscreen`, `hint_caption`, `hint_activator_caption`, `hint_color`, `hint_icon_offset (float)`, `hint_range (float)`, `hint_flags (long)`, `hint_binding`, `hint_allow_nodraw_target (bool)`, `hint_nooffscreen (bool)`, `hint_forcecaption (bool)`, `hint_local_player_only (bool)`, `hint_start_sound`, `hint_layoutfile`, `hint_vr_panel_type (short)`, `hint_vr_height_offset/offset_x/y/z (float)`
- **instructor_server_hint_stop** — `hint_name`, `hint_entindex (long)`

### Demo / bot
- **demo_start** — local-only event (embeds combat log, hero chase, pick hero lists)
- **demo_stop / map_transition** — (no fields)
- **demo_skip** — `playback_tick (long)`, `skipto_tick (long)`, local user_message_list
- **bot_takeover** — `userid (player_controller_and_pawn)`, `botid (player_controller)`, `p/y/r (float)`
- **user_data_downloaded** — (no fields)

---

## 4. Quick start checklist

1. Reference `DeadworksManaged.Api.dll` (see `Boilerplate.csproj`).
2. Subclass `DeadworksPluginBase`, override `Name`, `OnLoad`, `OnUnload`.
3. Override lifecycle hooks you care about (damage, chat, connect, …).
4. Tag Source 2 event handlers with `[GameEventHandler("event_name")]` —
   any `HookResult Foo(GameEvent)` (or typed subclass) method qualifies.
5. For runtime subscription, call `GameEvents.AddListener(name, handler)` and
   dispose the returned `IHandle` from `OnUnload`.
6. Build — the `DeployToGame` target copies the DLL into
   `…/Deadlock/game/bin/win64/managed/plugins/`.
7. In-game console: `dw_listplugins`, `dw_reloadplugin Boilerplate`,
   `dw_reloadconfig`.
