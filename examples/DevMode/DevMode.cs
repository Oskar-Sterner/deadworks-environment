using DeadworksManaged.Api;

namespace DevMode;

/// <summary>
/// Simple example plugin that enables cheats and grants max resources on respawn.
/// Great starting point for understanding Deadworks plugin basics.
/// </summary>
public class DevMode : DeadworksPluginBase
{
    public override string Name => "DevMode";

    private const int MaxLevel = 36;
    private const int MaxSouls = 99999;

    public override void OnLoad(bool isReload) { }
    public override void OnUnload() { }

    public override void OnStartupServer()
    {
        Server.ExecuteCommand("sv_cheats 1");
        Console.WriteLine($"[{Name}] sv_cheats enabled.");
    }

    [GameEventHandler("player_respawned")]
    public HookResult OnPlayerRespawned(GameEvent e)
    {
        var basePawn = e.GetPlayerPawn("userid");
        var heroPawn = basePawn?.As<CCitadelPlayerPawn>();
        if (heroPawn != null)
            GrantMaxResources(heroPawn);

        return HookResult.Continue;
    }

    private static void GrantMaxResources(CCitadelPlayerPawn pawn)
    {
        pawn.Level = MaxLevel;
        pawn.SetCurrency(ECurrencyType.EGold, MaxSouls);
        pawn.SetCurrency(ECurrencyType.EAbilityPoints, MaxLevel);
    }
}
