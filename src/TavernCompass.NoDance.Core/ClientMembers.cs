using System.Collections.Generic;
using System.Linq;

namespace TavernCompass.NoDance.Core;

/// <summary>
/// The client members the mod calls besides its Harmony targets, and those the design reasons about. Read by the mod
/// when it loads (<see cref="CalledByMod"/>: one missing disables the whole mod) and by the signature test (both lists,
/// against the installed client's metadata).
/// </summary>
public static class ClientMembers
{
    /// <summary>Called, constructed or read by the mod: a missing one would throw in the middle of a game.</summary>
    public static readonly IReadOnlyList<ClientMember> CalledByMod = Parse(
        "ZoneMgr|Get||ZoneMgr|static",
        "ZoneMgr|FindZoneOfType|Player.Side|!!0|instance",
        "ZoneMgr|IsBattlegroundShoppingPhase||bool|instance",
        "ZoneMgr|ShouldIgnorePosChange||bool|instance",
        "ZoneMgr|ProcessGeneratedLocalChangeLists|System.Collections.Generic.List`1<ZoneChangeList>,System.Threading.CancellationToken|void|instance",
        "ZoneMgr|GetCancellationToken||System.Threading.CancellationToken|instance",
        "GameState|Get||GameState|static",
        "GameState|GetSelectedNetworkOption||Network.Options.Option|instance",
        "GameState|GetSelectedOptionPosition||int|instance",
        "GameState|GetGameEntity||GameEntity|instance",
        "GameState|MustWaitForChoices||bool|instance",
        "GameState|RegisterOptionsReceivedListener|GameState.OptionsReceivedCallback,object|bool|instance",
        "GameState|UnregisterOptionsReceivedListener|GameState.OptionsReceivedCallback,object|bool|instance",
        "GameState|RegisterOptionRejectedListener|GameState.OptionRejectedCallback,object|bool|instance",
        "GameState|UnregisterOptionRejectedListener|GameState.OptionRejectedCallback,object|bool|instance",
        "GameState.OptionsReceivedCallback|.ctor|object,System.IntPtr|void|instance",
        "GameState.OptionRejectedCallback|.ctor|object,System.IntPtr|void|instance",
        "GameMgr|Get||GameMgr|static",
        "GameMgr|IsBattlegrounds||bool|instance",
        "GameMgr|IsBattlegroundDuoGame||bool|instance",
        "GameMgr|IsSpectator||bool|instance",
        "InputManager|Get||InputManager|static",
        "InputManager|GetHeldCard||Card|instance",
        "Zone|GetCards||System.Collections.Generic.List`1<Card>|instance",
        "Card|GetEntity||Entity|instance",
        "Card|GetZone||Zone|instance",
        "Card|GetZonePosition||int|instance",
        "Card|SetZonePosition|int|void|instance",
        "Entity|GetCard||Card|instance",
        "Entity|GetRealTimeZone||TAG_ZONE|instance",
        "Entity|GetRealTimeZonePosition||int|instance",
        "Entity|HasQueuedControllerTagChange||bool|instance",
        "EntityBase|GetEntityId||int|instance",
        "EntityBase|GetZonePosition||int|instance",
        "ZoneChangeList|.ctor||void|instance",
        "ZoneChangeList|AddChange|ZoneChange|void|instance",
        "ZoneChangeList|GetChanges||System.Collections.Generic.List`1<ZoneChange>|instance",
        "ZoneChangeList|GetId||int|instance",
        "ZoneChangeList|GetLocalTriggerCard||Card|instance",
        "ZoneChangeList|GetLocalTriggerChange||ZoneChange|instance",
        "ZoneChangeList|GetPredictedPosition||int|instance",
        "ZoneChangeList|GetTaskList||PowerTaskList|instance",
        "ZoneChangeList|m_ignoreCardZoneChanges|field|bool|instance",
        "PowerTaskList|GetBlockStart||Network.HistBlockStart|instance",
        "ZoneChange|.ctor||void|instance",
        "ZoneChange|SetEntity|Entity|void|instance",
        "ZoneChange|SetSourcePosition|int|void|instance",
        "ZoneChange|SetDestinationPosition|int|void|instance",
        "ZoneChange|GetEntity||Entity|instance",
        "ZoneChange|GetDestinationPosition||int|instance",
        "ZoneChange|HasDestinationPosition||bool|instance",
        "ZoneChange|HasDestinationZone||bool|instance",
        "ZoneChange|HasDestinationZoneChange||bool|instance",
        "PowerTask|GetPower||Network.PowerHistory|instance",
        "Network.PowerHistory|get_Type||Network.PowerType|instance",
        "Network.HistTagChange|get_Entity||int|instance",
        "Network.HistTagChange|get_Tag||int|instance",
        "Network.HistTagChange|get_Value||int|instance",
        "Network.Options.Option|get_Main||Network.Options.Option.SubOption|instance",
        "Network.Options.Option.SubOption|get_ID||int|instance");

    /// <summary>
    /// Read by the research and relied on by its reasoning, not called by the mod: if one of them changes, the reasons
    /// the design gives may no longer hold (the test says which). One missing does not disable the mod.
    /// </summary>
    public static readonly IReadOnlyList<ClientMember> DesignReference = Parse(
        "ZoneMgr|CreateLocalChangesFromTrigger|ZoneChangeList,ZoneChange|void|instance",
        "ZoneMgr|CreateLocalChangesFromTrigger|ZoneChangeList,Entity,Zone,TAG_ZONE,int,Zone,TAG_ZONE,int|void|instance",
        "ZoneMgr|MergeServerChangeList|ZoneChangeList|bool|instance",
        "ZoneMgr|AutoCorrectZones|System.Threading.CancellationToken,bool|void|instance",
        "ZoneMgr|IsBattlegroundBattlePhase||bool|instance",
        "GameState|RegisterOptionsSentListener|GameState.OptionsSentCallback,object|bool|instance",
        "GameState|RegisterOptionsReceivedListener|GameState.OptionsReceivedCallback|bool|instance",
        "GameState|UnregisterOptionsSentListener|GameState.OptionsSentCallback,object|bool|instance",
        "GameState|UnregisterOptionsReceivedListener|GameState.OptionsReceivedCallback|bool|instance",
        "GameState|GetFriendlyPlayerId||int|instance",
        "ZoneChangeList|SetIgnoreCardZonePurePosChanges|bool|void|instance",
        "ZoneChangeList|ShouldIgnoreCardZonePurePosChanges||bool|instance",
        "EntityBase|GetControllerId||int|instance");

    public static IEnumerable<ClientMember> All => CalledByMod.Concat(DesignReference);

    private static IReadOnlyList<ClientMember> Parse(params string[] lines) => lines.Select(ClientMember.Parse).ToList();
}
