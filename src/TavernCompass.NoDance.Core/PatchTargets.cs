using System;
using System.Collections.Generic;
using System.Linq;

namespace TavernCompass.NoDance.Core;

public enum PatchKind
{
    Prefix,
    Postfix,
    PrefixAndPostfix,
}

/// <summary>A method of the client the mod patches with Harmony, and why.</summary>
public sealed class PatchTarget
{
    public PatchTarget(string id, string member, PatchKind kind, bool required, IReadOnlyList<string> boundParameters,
        string role)
    {
        Id = id;
        Member = ClientMember.Parse(member);
        Kind = kind;
        Required = required;
        BoundParameters = boundParameters;
        Role = role;
    }

    /// <summary>H1 to H6, the names of the design (docs/journal/2026-10-10-anti-danse.md).</summary>
    public string Id { get; }

    public ClientMember Member { get; }

    public PatchKind Kind { get; }

    /// <summary>A required target missing from the client disables the whole mod; an optional one only itself.</summary>
    public bool Required { get; }

    /// <summary>Parameter names the hook binds by name (Harmony injects arguments by name): checked with the signature.</summary>
    public IReadOnlyList<string> BoundParameters { get; }

    public string Role { get; }

    public bool HasPrefix => Kind is PatchKind.Prefix or PatchKind.PrefixAndPostfix;

    public bool HasPostfix => Kind is PatchKind.Postfix or PatchKind.PrefixAndPostfix;

    /// <summary>"ZoneMgr.PostProcessServerChangeList postfix", as the log writes it.</summary>
    public string Display => $"{Member.Display} {KindName}";

    public string KindName => Kind switch
    {
        PatchKind.Prefix => "prefix",
        PatchKind.Postfix => "postfix",
        _ => "prefix+postfix",
    };
}

/// <summary>
/// The Harmony targets of the mod, as data: the single source read by the mod when it loads (each target looked up by
/// signature before anything is patched) and by the signature test, which reads the installed client's metadata.
/// </summary>
public static class PatchTargets
{
    public static readonly IReadOnlyList<PatchTarget> All = new[]
    {
        new PatchTarget("H1", "ZoneMgr|Awake||void|instance", PatchKind.Postfix, required: true, Array.Empty<string>(),
            "attaches the driver to the zone manager, one per game"),
        new PatchTarget("H2", "GameState|SendOption||void|instance", PatchKind.Prefix, required: true, Array.Empty<string>(),
            "reads the selected option and its position before the client clears them: an action is in flight"),
        new PatchTarget("H3", "PowerTask|DoRealTimeTask|System.Collections.Generic.List`1<Network.PowerHistory>,int|void|instance",
            PatchKind.Postfix, required: true, Array.Empty<string>(),
            "marks the row dirty on real-time zone data, follows BACON_IN_COMBAT_PHASE in real time"),
        new PatchTarget("H4", "ZoneMgr|AddPredictedLocalZoneChange|Card,Zone,int,int|ZoneChangeList|instance",
            PatchKind.PrefixAndPostfix, required: true, Array.Empty<string>(),
            "renumbers the row 1..n without reordering it before the client's prediction; logs the prediction"),
        new PatchTarget("H5", "ZoneMgr|PostProcessServerChangeList|ZoneChangeList|void|instance", PatchKind.Postfix,
            required: true, new[] { "serverChangeList" },
            "neutralizes the positions a late server task list replays on the player's row"),
        new PatchTarget("H6", "ZoneMgr|OnRealTimeZonePosChange|Entity|void|instance", PatchKind.Prefix, required: false,
            new[] { "entity" },
            "optional: skips the client's card-by-card real-time writes on the player's row"),
    };

    public static IEnumerable<PatchTarget> Required => All.Where(t => t.Required);

    public static IEnumerable<PatchTarget> Optional => All.Where(t => !t.Required);

    public static PatchTarget ById(string id) => All.Single(t => t.Id == id);
}
