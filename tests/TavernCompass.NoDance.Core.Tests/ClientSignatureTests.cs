using System.Reflection;

namespace TavernCompass.NoDance.Core.Tests;

/// <summary>Witness types of this test assembly, shaped like the client members the mod looks up.</summary>
public class SignatureWitness
{
    public delegate void Callback(object userData);

    public int Count;

    public SignatureWitness()
    {
    }

    public static SignatureWitness Get() => new();

    public T? FindOfType<T>(Nested.Side side) where T : class => null;

    public void Process(List<SignatureWitness>? lists, CancellationToken token)
    {
    }

    public bool Register(Callback callback, object? userData) => callback != null && userData == null;

    public void Overloaded(int value)
    {
    }

    public void Overloaded(string value)
    {
    }

    public class Nested
    {
        public enum Side
        {
            Friendly,
        }

        public int Id { get; set; }
    }
}

/// <summary>
/// The text form of a member is written two ways: by the mod from System.Reflection (when it loads in the game), by the
/// signature test from the file's metadata (without loading it). Both must agree, or one of them would report a target
/// missing that is there, or the reverse. Checked here on witness types, without the game.
/// </summary>
public class ClientSignatureTests
{
    private static readonly string[] WitnessMembers =
    {
        "TavernCompass.NoDance.Core.Tests.SignatureWitness|Get||TavernCompass.NoDance.Core.Tests.SignatureWitness|static",
        "TavernCompass.NoDance.Core.Tests.SignatureWitness|.ctor||void|instance",
        "TavernCompass.NoDance.Core.Tests.SignatureWitness|FindOfType|TavernCompass.NoDance.Core.Tests.SignatureWitness.Nested.Side|!!0|instance",
        "TavernCompass.NoDance.Core.Tests.SignatureWitness|Process|System.Collections.Generic.List`1<TavernCompass.NoDance.Core.Tests.SignatureWitness>,System.Threading.CancellationToken|void|instance",
        "TavernCompass.NoDance.Core.Tests.SignatureWitness|Register|TavernCompass.NoDance.Core.Tests.SignatureWitness.Callback,object|bool|instance",
        "TavernCompass.NoDance.Core.Tests.SignatureWitness|Overloaded|string|void|instance",
        "TavernCompass.NoDance.Core.Tests.SignatureWitness|Count|field|int|instance",
        "TavernCompass.NoDance.Core.Tests.SignatureWitness.Nested|get_Id||int|instance",
        "TavernCompass.NoDance.Core.Tests.SignatureWitness.Callback|.ctor|object,System.IntPtr|void|instance",
    };

    private static readonly string[] Invented =
    {
        "TavernCompass.NoDance.Core.Tests.SignatureWitness|NonExistentMethodForControl||void|instance",
        "TavernCompass.NoDance.Core.Tests.SignatureWitness|Overloaded|bool|void|instance",
        "TavernCompass.NoDance.Core.Tests.SignatureWitness|Get||TavernCompass.NoDance.Core.Tests.SignatureWitness|instance",
        "TavernCompass.NoDance.Core.Tests.SignatureWitness|Count|field|string|instance",
        "TavernCompass.NoDance.Core.Tests.NoSuchType|Get||void|static",
    };

    private static readonly Assembly Witness = typeof(SignatureWitness).Assembly;

    [Fact]
    public void Reflection_finds_the_witness_members_and_writes_them_in_the_same_form()
    {
        foreach (string line in WitnessMembers)
        {
            var member = ClientMember.Parse(line);
            var found = ClientSignature.Find(Witness, member);
            Assert.True(found != null, "not found by reflection: " + line);
            Assert.Equal(line, ClientSignature.Of(found!));
        }
    }

    [Fact]
    public void Metadata_finds_the_same_witness_members()
    {
        using var metadata = new MetadataSignatures(Witness.Location);
        foreach (string line in WitnessMembers)
        {
            Assert.True(metadata.Contains(ClientMember.Parse(line)), "not found in the metadata: " + line);
        }
    }

    [Fact]
    public void Both_readers_report_invented_members_missing()
    {
        using var metadata = new MetadataSignatures(Witness.Location);
        foreach (string line in Invented)
        {
            var member = ClientMember.Parse(line);
            Assert.Null(ClientSignature.Find(Witness, member));
            Assert.False(metadata.Contains(member), "found in the metadata: " + line);
        }
    }

    [Fact]
    public void Metadata_gives_the_parameter_names_hooks_bind_by()
    {
        using var metadata = new MetadataSignatures(Witness.Location);
        var names = metadata.ParameterNames(ClientMember.Parse(WitnessMembers[3]));
        Assert.Equal(new[] { "lists", "token" }, names);
    }

    [Fact]
    public void Types_are_written_like_the_metadata_writes_them()
    {
        Assert.Equal("System.Collections.Generic.List`1<int>", ClientSignature.Of(typeof(List<int>)));
        Assert.Equal("System.Collections.Generic.Dictionary`2<string;object>", ClientSignature.Of(typeof(Dictionary<string, object>)));
        Assert.Equal("TavernCompass.NoDance.Core.Tests.SignatureWitness.Nested.Side", ClientSignature.Of(typeof(SignatureWitness.Nested.Side)));
        Assert.Equal("int[]", ClientSignature.Of(typeof(int[])));
        Assert.Equal("System.Int64", ClientSignature.Of(typeof(long)));
    }

    [Fact]
    public void A_nested_name_written_with_dots_is_found()
    {
        Assert.Equal(typeof(SignatureWitness.Nested.Side),
            ClientSignature.FindType(Witness, "TavernCompass.NoDance.Core.Tests.SignatureWitness.Nested.Side"));
        Assert.Null(ClientSignature.FindType(Witness, "TavernCompass.NoDance.Core.Tests.SignatureWitness.Missing"));
    }

    [Fact]
    public void Every_listed_member_reads_back_to_its_own_line_and_none_is_listed_twice()
    {
        var all = ClientMembers.All.Select(m => m.Signature).Concat(PatchTargets.All.Select(t => t.Member.Signature)).ToList();
        foreach (string line in all)
        {
            Assert.Equal(line, ClientMember.Parse(line).Signature);
        }

        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void Five_targets_are_required_and_one_is_optional()
    {
        Assert.Equal(new[] { "H1", "H2", "H3", "H4", "H5" }, PatchTargets.Required.Select(t => t.Id));
        Assert.Equal(new[] { "H6" }, PatchTargets.Optional.Select(t => t.Id));
        Assert.Equal("ZoneMgr.PostProcessServerChangeList postfix", PatchTargets.ById("H5").Display);
    }

    [Fact]
    public void A_malformed_line_is_refused()
    {
        Assert.Throws<FormatException>(() => ClientMember.Parse("ZoneMgr|Awake|void|instance"));
        Assert.Throws<FormatException>(() => ClientMember.Parse("ZoneMgr|Awake||void|sometimes"));
    }
}
