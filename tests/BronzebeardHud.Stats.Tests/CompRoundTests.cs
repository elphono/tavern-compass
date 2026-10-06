using static BronzebeardHud.Stats.Tests.GuideTestData;

namespace BronzebeardHud.Stats.Tests;

/// <summary>
/// One round of the compositions, as the plugin runs it (CompTargets.Round): the lobby's guides ranked against the cards
/// held, the targets chosen, ticks on guides the lobby cannot play dropped. Modelled on Ali's game of 2026-10-06 17:04,
/// lobby DEMON, MECHANICAL, PIRATE, QUILBOAR, UNDEAD (read from its Power.log): holding the neutral key card Titus made
/// a beast guide and an aberration guide targets, with frames on Bob's cards. Synthetic data.
/// </summary>
public class CompRoundTests
{
    private static readonly CompGuide Undead = Guide("Undead - Attack", 2, 0, new[] { "BUTCHER", "RISEN", "SCALER", "TITUS" }, addons: new[] { "UA" }, tribe: 11);
    private static readonly CompGuide Beasts = Guide("Beasts - Lobster", 1, 0, new[] { "LOBSTER", "TITUS", "GRYPHON" }, addons: new[] { "HYENA" }, tribe: 20);
    private static readonly CompGuide Aberrations = Guide("Aberrations - Deathrattle", 2, 1, new[] { "CONVERTER", "TITUS" }, tribe: 126);
    private static readonly CompGuide Demons = Guide("Demons - Shop Buff", 2, 2, new[] { "IMP", "FELBOAR" }, tribe: 15);
    private static readonly CompGuideSet All = Set(Undead, Beasts, Aberrations, Demons);

    private static readonly IReadOnlyDictionary<string, string[]> CardTribes = new Dictionary<string, string[]>
    {
        ["TITUS"] = Array.Empty<string>(),
        ["BUTCHER"] = new[] { "UNDEAD" },
        ["RISEN"] = new[] { "UNDEAD" },
        ["SCALER"] = new[] { "UNDEAD" },
        ["UA"] = new[] { "UNDEAD" },
        ["LOBSTER"] = new[] { "BEAST" },
        ["GRYPHON"] = new[] { "BEAST" },
        ["HYENA"] = new[] { "BEAST" },
        ["CONVERTER"] = new[] { "ABERRATION" },
        ["IMP"] = new[] { "DEMON" },
        ["FELBOAR"] = new[] { "QUILBOAR" },
    };

    private static readonly string[] Lobby = { "DEMON", "MECHANICAL", "PIRATE", "QUILBOAR", "UNDEAD" };

    private static LobbyGuides InLobby => LobbyGuides.Of(All, Lobby, id => CardTribes.TryGetValue(id, out var t) ? t : null);

    /// <summary>Two undead key cards on the board, Titus in hand: three guides of absent tribes list Titus as a key card.</summary>
    private static readonly PlayerCards Held = new(Owned("BUTCHER", "RISEN"), Owned("TITUS"));

    [Fact]
    public void ANeutralKeyCardOfGuidesOfAbsentTribes_MakesNoneOfThemATarget_NorAFrame_NorAChoiceLabel()
    {
        var round = CompTargets.Round(InLobby, Held, new CompTargetTracker(), 3);

        Assert.Equal(new[] { "Undead - Attack" }, round.Targets.Select(t => t.Guide.Name));
        Assert.DoesNotContain(round.Board.All, p => p.Guide.Id == Beasts.Id || p.Guide.Id == Aberrations.Id); // not even listed

        var bob = new[] { "LOBSTER", "CONVERTER", "SCALER" };
        var frames = TavernHighlights.For(bob, round.Targets, bridge: null);
        Assert.Equal("SCALER:core:Undead - Attack/11", TavernHighlights.Summary(bob, frames));

        var choice = ChoiceAdvisor.Advise(new[] { new OfferedOption(1, "LOBSTER", "MINION"), new OfferedOption(2, "CONVERTER", "MINION") },
            Held.All, round.Targets, InLobby.Playable, InLobby.Tribes);
        Assert.All(choice.Options, o => Assert.Equal(ChoiceReason.None, o.Reason));

        // The same round with the lobby not known: the bug as it was, the beasts and aberrations among the targets.
        var unfiltered = CompTargets.Round(LobbyGuides.Unknown(All), Held, new CompTargetTracker(), 3);
        // (Aberrations before Beasts: the same score, a larger share of its key cards held, 1 of 2 against 1 of 3.)
        Assert.Equal(new[] { "Undead - Attack", "Aberrations - Deathrattle", "Beasts - Lobster" }, unfiltered.Targets.Select(t => t.Guide.Name));
    }

    [Fact]
    public void ATickOnAGuideTheLobbyCannotPlay_IsDropped_SaidOnce_AndNeverMakesItATarget()
    {
        var tracker = new CompTargetTracker();
        tracker.Toggle(Beasts.Id); // ticked while the lobby was not known yet
        Assert.Contains(Beasts.Id, tracker.Ticked);

        var first = CompTargets.Round(InLobby, Held, tracker, 3);
        var second = CompTargets.Round(InLobby, Held, tracker, 3);

        Assert.Equal(new[] { "Undead - Attack" }, first.Targets.Select(t => t.Guide.Name));
        Assert.Equal(new[] { "Bronzebeard HUD: unticked Beasts - Lobster/20: no BEAST" }, first.Unticked);
        Assert.Empty(second.Unticked); // said once: the tick is gone
        Assert.Empty(tracker.Ticked);
        Assert.Equal(TargetKind.Probable, Assert.Single(second.Targets).Kind);
    }

    [Fact]
    public void TheLobbyNotKnownYet_FiltersNothing_TheTicksStay()
    {
        var tracker = new CompTargetTracker();
        tracker.Toggle(Beasts.Id);

        var round = CompTargets.Round(LobbyGuides.Unknown(All), Held, tracker, 3);

        Assert.Equal(new[] { "Beasts - Lobster", "Undead - Attack" }, round.Targets.Select(t => t.Guide.Name)); // ticked, then in progress
        Assert.Empty(round.Unticked);
        Assert.Equal(new[] { Beasts.Id }, tracker.Ticked);
    }
}
