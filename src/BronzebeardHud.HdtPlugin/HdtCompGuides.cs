using System;
using System.Collections.Generic;
using System.Linq;
using BronzebeardHud.Stats;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Guides;
using Hearthstone_Deck_Tracker.Controls.Overlay.Battlegrounds.Guides.Comps;

namespace BronzebeardHud.HdtPlugin;

/// <summary>What HDT's comp guide list holds at one moment.</summary>
internal sealed class HdtCompGuidesSnapshot
{
    public HdtCompGuidesSnapshot(string state, CompGuideSet? guides, string? error)
    {
        State = state;
        Guides = guides;
        Error = error;
    }

    /// <summary>HDT's own state: Loading, BaseFeature (free list), Tier7Feature, Empty or Error.</summary>
    public string State { get; }

    /// <summary>The guides HDT shows; null while it shows none, or when they could not be read.</summary>
    public CompGuideSet? Guides { get; }

    /// <summary>Why the guides HDT holds could not be read; null when they could.</summary>
    public string? Error { get; }
}

/// <summary>
/// Reads the Battlegrounds comp guides HDT already loaded for its own overlay, through its plugin API:
/// Hearthstone_Deck_Tracker.API.Core.OverlayWindow, then the overlay's public BattlegroundsCompsGuidesVM
/// (Windows/OverlayWindow.xaml.cs), whose public Comps (free list) and CompsByTier (Tier 7 list) hold the
/// HSReplay.Responses.BattlegroundsCompGuide objects (CompGuide property of each BattlegroundsCompGuideViewModel).
/// HDT loads them itself at the start of each Battlegrounds match (OnMatchStart): the plugin makes no request of its
/// own. HDT's CurrentState decides which list it shows, and so it does here. Measured on 2026-10-04 against HDT 1.58.6
/// and 1.55.6: docs/journal/2026-10-04-comp-guides-hdt.md.
/// </summary>
internal static class HdtCompGuides
{
    /// <summary>
    /// What HDT shows, cheaply, for every update: its state and the list object it holds. HDT replaces the list on each
    /// load, so a change of either means the guides must be read again (<see cref="Read"/>).
    /// </summary>
    public static (string State, object? List) Peek()
    {
        var vm = Core.OverlayWindow.BattlegroundsCompsGuidesVM;
        var state = vm.CurrentState;
        object? list = state switch
        {
            CompGuideListState.Tier7Feature => vm.CompsByTier,
            CompGuideListState.BaseFeature => vm.Comps,
            _ => null,
        };
        return (state.ToString(), list);
    }

    /// <summary>The guides HDT shows, read into the plugin's model: called only when <see cref="Peek"/> changed.</summary>
    public static HdtCompGuidesSnapshot Read()
    {
        var vm = Core.OverlayWindow.BattlegroundsCompsGuidesVM;
        var state = vm.CurrentState;
        switch (state)
        {
            case CompGuideListState.Tier7Feature when vm.CompsByTier is { } byTier:
                return Parse(state, () => CompGuideParser.FromTiers(
                    byTier.Select(kv => new KeyValuePair<int, IEnumerable<object>>(kv.Key, Guides(kv.Value.Comps))).ToList(),
                    CardIdOf, CompGuideSources.HdtTier7));
            case CompGuideListState.BaseFeature when vm.Comps is { } free:
                return Parse(state, () => CompGuideParser.FromObjects(Guides(free), CardIdOf, CompGuideSources.HdtFree));
            default:
                return new HdtCompGuidesSnapshot(state.ToString(), null, null);
        }
    }

    private static HdtCompGuidesSnapshot Parse(CompGuideListState state, Func<CompGuideSet> parse)
    {
        try
        {
            return new HdtCompGuidesSnapshot(state.ToString(), parse(), null);
        }
        catch (StatsFormatException e)
        {
            return new HdtCompGuidesSnapshot(state.ToString(), null, e.Message);
        }
    }

    /// <summary>The HSReplay objects behind HDT's view models, in HDT's list order.</summary>
    private static IEnumerable<object> Guides(IEnumerable<BattlegroundsCompGuideViewModel>? viewModels) =>
        (viewModels ?? Enumerable.Empty<BattlegroundsCompGuideViewModel>()).Select(c => (object)c.CompGuide).ToList();

    /// <summary>dbf id → card id, through HearthDb as HDT ships it.</summary>
    private static string? CardIdOf(int dbfId) => HearthDb.Cards.AllByDbfId.TryGetValue(dbfId, out var card) ? card.Id : null;
}
