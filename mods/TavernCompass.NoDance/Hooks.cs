using System;
using System.Collections.Generic;
using System.Reflection;
using TavernCompass.NoDance.Core;

namespace TavernCompass.NoDance;

/// <summary>
/// The Harmony patch methods, H1 to H6 (Core/PatchTargets). Each one is cheap outside the Battlegrounds shop and runs
/// under its own guard: an exception is logged once, then a required part stops the whole mod for the rest of the
/// session (the client keeps its own behaviour: half of the mod could make new dances), and the optional H6 stops alone.
/// </summary>
internal static class Hooks
{
    private static readonly HashSet<string> Failed = new();

    /// <summary>Set once every required patch is applied; cleared by a failure of a required part.</summary>
    internal static bool Active { get; set; }

    /// <summary>The optional H6, from the configuration; cleared by its own failure.</summary>
    internal static bool SkipPerCardRealTimeWrites { get; set; }

    internal static (MethodInfo? Prefix, MethodInfo? Postfix) For(PatchTarget target)
    {
        string? prefix = target.Id switch
        {
            "H2" => nameof(BeforeSendOption),
            "H4" => nameof(BeforePrediction),
            "H6" => nameof(BeforeRealTimeZonePosChange),
            _ => null,
        };
        string? postfix = target.Id switch
        {
            "H1" => nameof(AfterZoneMgrAwake),
            "H3" => nameof(AfterRealTimeTask),
            "H4" => nameof(AfterPrediction),
            "H5" => nameof(AfterPostProcessServerChangeList),
            _ => null,
        };
        if ((prefix != null) != target.HasPrefix || (postfix != null) != target.HasPostfix)
        {
            throw new InvalidOperationException($"{target.Id}: the hooks do not match the kind {target.KindName}");
        }

        return (Method(prefix), Method(postfix));
    }

    private static MethodInfo? Method(string? name) =>
        name == null ? null : typeof(Hooks).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("hook not found: " + name);

    /// <summary>Logs a failure once per part; a required part stops the mod, the optional H6 stops alone.</summary>
    internal static void Fail(string part, Exception e, bool required)
    {
        if (!Failed.Add(part))
        {
            return;
        }

        if (required)
        {
            Active = false;
            NoDancePlugin.Log?.LogError($"{part} failed: the mod stops for the rest of the session, the client keeps its own behaviour: {e}");
        }
        else
        {
            SkipPerCardRealTimeWrites = false;
            NoDancePlugin.Log?.LogError($"{part} failed: this optional patch stops: {e}");
        }
    }

    // H1: one driver per game, on the zone manager's object, destroyed with it.
    private static void AfterZoneMgrAwake(ZoneMgr __instance)
    {
        if (!Active)
        {
            return;
        }

        try
        {
            NoDanceDriver.Attach(__instance);
        }
        catch (Exception e)
        {
            Fail("H1 ZoneMgr.Awake postfix", e, required: true);
        }
    }

    // H2: the selection is cleared inside SendOption, so it is read before.
    private static void BeforeSendOption(GameState __instance)
    {
        if (!Active)
        {
            return;
        }

        try
        {
            var driver = NoDanceDriver.Current;
            if (driver == null || __instance == null)
            {
                return;
            }

            var option = __instance.GetSelectedNetworkOption();
            int position = __instance.GetSelectedOptionPosition();
            var main = option?.Main;
            if (main == null || position <= 0 || !driver.InScope())
            {
                return;
            }

            driver.StartFlight(main.ID, position);
        }
        catch (Exception e)
        {
            Fail("H2 GameState.SendOption prefix", e, required: true);
        }
    }

    // H3: every real-time power, as soon as it is received.
    private static void AfterRealTimeTask(PowerTask __instance)
    {
        if (!Active)
        {
            return;
        }

        try
        {
            var driver = NoDanceDriver.Current;
            var power = __instance?.GetPower();
            if (driver == null || power == null)
            {
                return;
            }

            switch (power.Type)
            {
                case Network.PowerType.TAG_CHANGE:
                    var change = (Network.HistTagChange)power;
                    int tag = change.Tag;
                    if (tag == (int)GAME_TAG.BACON_IN_COMBAT_PHASE)
                    {
                        driver.OnRealTimeCombatPhase(change.Entity, change.Value);
                    }
                    else if (tag == (int)GAME_TAG.ZONE || tag == (int)GAME_TAG.ZONE_POSITION || tag == (int)GAME_TAG.CONTROLLER)
                    {
                        driver.MarkDirty("packet");
                    }

                    break;
                case Network.PowerType.FULL_ENTITY:
                case Network.PowerType.SHOW_ENTITY:
                case Network.PowerType.CHANGE_ENTITY:
                case Network.PowerType.HIDE_ENTITY:
                    driver.MarkDirty("packet");
                    break;
            }
        }
        catch (Exception e)
        {
            Fail("H3 PowerTask.DoRealTimeTask postfix", e, required: true);
        }
    }

    // H4: the row renumbered 1..n in the order shown, before the client's arithmetic of the prediction.
    private static void BeforePrediction()
    {
        if (!Active)
        {
            return;
        }

        try
        {
            var driver = NoDanceDriver.Current;
            if (driver != null)
            {
                driver.RenumberBeforePrediction();
            }
        }
        catch (Exception e)
        {
            Fail("H4 ZoneMgr.AddPredictedLocalZoneChange prefix", e, required: true);
        }
    }

    private static void AfterPrediction(ZoneChangeList __result)
    {
        if (!Active)
        {
            return;
        }

        try
        {
            var driver = NoDanceDriver.Current;
            if (driver != null)
            {
                driver.AfterPrediction(__result);
            }
        }
        catch (Exception e)
        {
            Fail("H4 ZoneMgr.AddPredictedLocalZoneChange postfix", e, required: true);
        }
    }

    // H5: a server task list is about to be played; the client has already decided whether it confirms the player.
    private static void AfterPostProcessServerChangeList(ZoneChangeList serverChangeList)
    {
        if (!Active)
        {
            return;
        }

        try
        {
            var driver = NoDanceDriver.Current;
            if (driver != null)
            {
                driver.OnServerListStarts(serverChangeList);
            }
        }
        catch (Exception e)
        {
            Fail("H5 ZoneMgr.PostProcessServerChangeList postfix", e, required: true);
        }
    }

    // H6 (optional): returning false skips the client's card-by-card real-time write.
    private static bool BeforeRealTimeZonePosChange(Entity entity)
    {
        if (!Active || !SkipPerCardRealTimeWrites)
        {
            return true;
        }

        try
        {
            var driver = NoDanceDriver.Current;
            return driver == null || !driver.TakesOverRealTimeWrite(entity);
        }
        catch (Exception e)
        {
            Fail("H6 ZoneMgr.OnRealTimeZonePosChange prefix", e, required: false);
            return true;
        }
    }
}
