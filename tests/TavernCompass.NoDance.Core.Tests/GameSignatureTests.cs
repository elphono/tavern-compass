namespace TavernCompass.NoDance.Core.Tests;

/// <summary>
/// The Harmony targets and the client members the mod relies on, checked against the installed client's metadata
/// (HEARTHSTONE_MANAGED). A game update that renames or changes one of them fails here, before the mod disables itself
/// in the game. Without the variable, these tests are skipped and say so.
/// </summary>
public class GameSignatureTests
{
    private static readonly Lazy<MetadataSignatures> Client = new(() => new MetadataSignatures(GameAssembly.Path));

    public static IEnumerable<object[]> TargetIds() => PatchTargets.All.Select(t => new object[] { t.Id });

    public static IEnumerable<object[]> Members() => ClientMembers.All.Select(m => new object[] { m.Signature });

    private static string Found(ClientMember member)
    {
        var overloads = Client.Value.Signatures(member.Type, member.Name);
        return overloads.Count == 0 ? "nothing of that name" : string.Join(" ; ", overloads);
    }

    [GameTheory]
    [MemberData(nameof(TargetIds))]
    public void Every_Harmony_target_exists_with_its_signature(string id)
    {
        var target = PatchTargets.ById(id);
        Assert.True(Client.Value.Contains(target.Member),
            $"{target.Id} {target.Display}: missing from the client ({target.Member.Signature}); found: {Found(target.Member)}");
        if (target.BoundParameters.Count > 0)
        {
            var names = Client.Value.ParameterNames(target.Member);
            Assert.NotNull(names);
            foreach (string bound in target.BoundParameters)
            {
                Assert.True(names!.Contains(bound),
                    $"{target.Id}: the hook binds parameter '{bound}', the client names them {string.Join(", ", names)}");
            }
        }
    }

    [GameTheory]
    [MemberData(nameof(Members))]
    public void Every_client_member_exists_with_its_signature(string signature)
    {
        var member = ClientMember.Parse(signature);
        Assert.True(Client.Value.Contains(member), $"missing from the client: {signature}; found: {Found(member)}");
    }

    /// <summary>The detector on a witness that must not be found: an invented method, and a real one with a wrong signature.</summary>
    [GameFact]
    public void An_invented_member_is_reported_missing()
    {
        Assert.False(Client.Value.Contains(ClientMember.Parse("GameState|NonExistentMethodForControl||void|instance")));
        Assert.False(Client.Value.Contains(ClientMember.Parse("ZoneMgr|PostProcessServerChangeList|int|void|instance")));
        Assert.False(Client.Value.Contains(ClientMember.Parse("ZoneMgr|Awake||void|static")));
        Assert.True(Client.Value.Contains(ClientMember.Parse("ZoneMgr|Awake||void|instance")));
    }

    /// <summary>
    /// Every client member the built mod references is in the list the mod checks when it loads: a call added to the
    /// mod without its line would throw in the middle of a game instead of disabling the mod at load.
    /// </summary>
    [GameAndModFact]
    public void Every_client_member_the_built_mod_references_is_checked_at_load()
    {
        using var mod = new MetadataSignatures(GameAssembly.ModDll()!);
        var references = mod.MemberReferencesInto("Assembly-CSharp");
        Assert.NotEmpty(references);
        var checkedAtLoad = new HashSet<string>(
            ClientMembers.CalledByMod.Select(m => m.Signature).Concat(PatchTargets.All.Select(t => t.Member.Signature)));
        var notChecked = references.Where(r => !checkedAtLoad.Contains(r)).ToList();
        Assert.True(notChecked.Count == 0, "referenced by the mod but not checked at load: " + string.Join(" ; ", notChecked));
        Assert.Contains("Assembly-CSharp", mod.AssemblyReferences());
        Assert.Contains("BepInEx.BepInPlugin", mod.TypeAttributes());
    }
}
