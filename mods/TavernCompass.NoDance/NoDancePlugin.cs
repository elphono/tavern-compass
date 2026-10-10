using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TavernCompass.NoDance.Core;
using UnityEngine;

namespace TavernCompass.NoDance;

/// <summary>
/// Loads the anti-dance mod: looks up every Harmony target and every client member it calls, by signature, before
/// anything is patched. One required target or member missing (a game update changed it): nothing is patched, one
/// log line says what is missing, and the client keeps its own behaviour.
/// </summary>
[BepInPlugin(Guid, DisplayName, BuildInfo.Version)]
[BepInProcess("Hearthstone.exe")]
public sealed class NoDancePlugin : BaseUnityPlugin
{
    public const string Guid = "com.tavern-compass.nodance";

    public const string DisplayName = "Tavern Compass — No Dance";

    private Harmony? _harmony;

    internal static ManualLogSource? Log { get; private set; }

    /// <summary>Seconds an action of the player may stay unanswered before the row is unfrozen anyway.</summary>
    internal static double FlightTimeoutSeconds { get; private set; } = 3.0;

    /// <summary>The private field that marks the player's own confirmed task list (read by reflection, checked at load).</summary>
    internal static FieldInfo? IgnoreCardZoneChangesField { get; private set; }

    private void Awake()
    {
        Log = Logger;
        var enabledEntry = Config.Bind("General", "Enabled", true,
            "Off: nothing is patched, the client keeps its own behaviour (takes effect at the next start of the game).");
        var skipEntry = Config.Bind("Fixes", "SkipPerCardRealTimeWrites", false,
            "Optional patch H6: skip the client's card-by-card real-time position writes on the player's row (the single "
            + "writer then decides alone). Only if a one-frame jump is seen with it off. Read at the start of the game.");
        var timeoutEntry = Config.Bind("Fixes", "FlightTimeoutSeconds", 3.0f, new ConfigDescription(
            "Seconds a placement or move of the player may stay unanswered before the row is unfrozen anyway (longest "
            + "answer measured: 2.3 s).", new AcceptableValueRange<float>(0.5f, 10f)));
        FlightTimeoutSeconds = timeoutEntry.Value;
        Hooks.SkipPerCardRealTimeWrites = skipEntry.Value;

        if (!enabledEntry.Value)
        {
            Logger.LogInfo($"{DisplayName} {BuildInfo.Version} disabled by configuration (General.Enabled = false)");
            return;
        }

        try
        {
            Install();
        }
        catch (Exception e)
        {
            Hooks.Active = false;
            _harmony?.UnpatchSelf();
            Logger.LogError($"{DisplayName} {BuildInfo.Version} disabled (failed while loading: {e})");
        }
    }

    private void Install()
    {
        var game = typeof(ZoneMgr).Assembly;
        var missing = new List<string>();
        var targets = new Dictionary<string, MethodBase>();
        foreach (var target in PatchTargets.All)
        {
            var found = FindSafely(game, target.Member) as MethodBase;
            string? problem = found == null ? null : BoundParameterProblem(found, target);
            if (found != null && problem == null)
            {
                targets[target.Id] = found;
            }
            else if (target.Required)
            {
                missing.Add($"{target.Id} {target.Member.Display}{(problem == null ? string.Empty : " (" + problem + ")")}");
            }
            else
            {
                Logger.LogWarning($"patch {target.Display}: target missing{(problem == null ? string.Empty : " (" + problem + ")")}, optional, skipped");
            }
        }

        foreach (var member in ClientMembers.CalledByMod)
        {
            var found = FindSafely(game, member);
            if (found == null)
            {
                missing.Add(member.Display + (member.Kind == ClientMemberKind.Field ? string.Empty : "(" + string.Join(", ", member.Parameters) + ")"));
            }
            else if (member.Name == "m_ignoreCardZoneChanges")
            {
                IgnoreCardZoneChangesField = (FieldInfo)found;
            }
        }

        if (missing.Count > 0)
        {
            Logger.LogError($"{DisplayName} {BuildInfo.Version} disabled (missing: {string.Join(", ", missing)}); "
                + "nothing patched, the client keeps its own behaviour");
            return;
        }

        _harmony = new Harmony(Guid);
        int required = 0;
        int optional = 0;
        foreach (var target in PatchTargets.All)
        {
            if (!targets.TryGetValue(target.Id, out var original))
            {
                continue;
            }

            if (!target.Required && !Hooks.SkipPerCardRealTimeWrites)
            {
                Logger.LogInfo($"patch {target.Display}: off (Fixes.SkipPerCardRealTimeWrites = false)");
                continue;
            }

            var (prefix, postfix) = Hooks.For(target);
            try
            {
                _harmony.Patch(original,
                    prefix: prefix == null ? null : new HarmonyMethod(prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(postfix));
                Logger.LogInfo($"patch {target.Display}: ok");
                if (target.Required)
                {
                    required++;
                }
                else
                {
                    optional++;
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"patch {target.Display}: failed: {e.Message}");
                if (target.Required)
                {
                    _harmony.UnpatchSelf();
                    Logger.LogError($"{DisplayName} {BuildInfo.Version} disabled (failed: {target.Id} {target.Display}); "
                        + "nothing patched, the client keeps its own behaviour");
                    return;
                }
            }
        }

        Hooks.Active = true;
        Logger.LogInfo($"{required}/{PatchTargets.Required.Count()} required patches applied, {optional}/{PatchTargets.Optional.Count()} optional "
            + $"(game {Application.version}, Unity {Application.unityVersion}, BepInEx {typeof(BaseUnityPlugin).Assembly.GetName().Version}, "
            + $"{DisplayName} {BuildInfo.Version}, flight timeout {FlightTimeoutSeconds:0.0} s)");
    }

    private MemberInfo? FindSafely(Assembly game, ClientMember member)
    {
        try
        {
            return ClientSignature.Find(game, member);
        }
        catch (Exception e)
        {
            Logger.LogWarning($"looking up {member.Signature}: {e.GetType().Name}: {e.Message}");
            return null;
        }
    }

    /// <summary>Harmony injects these arguments by name: a renamed parameter would fail the patch.</summary>
    private static string? BoundParameterProblem(MethodBase method, PatchTarget target)
    {
        var names = method.GetParameters().Select(p => p.Name).ToList();
        var absent = target.BoundParameters.Where(b => !names.Contains(b)).ToList();
        return absent.Count == 0 ? null : $"parameter {string.Join(", ", absent)} not found, the client names them {string.Join(", ", names)}";
    }

    /// <summary>
    /// The patches and the driver do not depend on this object: some games destroy BepInEx's manager object on a scene
    /// change. Said in the log, nothing else.
    /// </summary>
    private void OnDestroy() => Log?.LogInfo("plugin object destroyed; the patches stay in place");
}
