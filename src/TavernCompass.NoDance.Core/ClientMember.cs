using System;
using System.Collections.Generic;
using System.Linq;

namespace TavernCompass.NoDance.Core;

public enum ClientMemberKind
{
    Method,
    Field,
}

/// <summary>
/// A member of the game client (Assembly-CSharp.dll) that the mod relies on, by name and signature only. The text form
/// is the one of the research's metadata reader: <c>Type|Name|ParamType1,ParamType2|ReturnType|instance</c> (or
/// <c>static</c>), and <c>Type|Name|field|FieldType|instance</c> for a field. Nested types are written with dots
/// (<c>Network.PowerHistory</c>), generic instances as <c>System.Collections.Generic.List`1&lt;Card&gt;</c> (arguments
/// separated by ';'), a method's generic parameter as <c>!!0</c>, and void, bool, int, float, string, object by their
/// C# names. Only names: no code of the client is in this repository.
/// </summary>
public sealed class ClientMember
{
    public ClientMember(string type, string name, IReadOnlyList<string> parameters, string returnType, bool isStatic,
        ClientMemberKind kind)
    {
        Type = type;
        Name = name;
        Parameters = parameters;
        ReturnType = returnType;
        IsStatic = isStatic;
        Kind = kind;
    }

    /// <summary>Declaring type: the member is looked up among the members this type declares itself.</summary>
    public string Type { get; }

    public string Name { get; }

    public IReadOnlyList<string> Parameters { get; }

    /// <summary>The return type of a method (void for a constructor), the type of a field.</summary>
    public string ReturnType { get; }

    public bool IsStatic { get; }

    public ClientMemberKind Kind { get; }

    /// <summary>Type.Name, as the log writes it.</summary>
    public string Display => $"{Type}.{Name}";

    public string Signature => Kind == ClientMemberKind.Field
        ? $"{Type}|{Name}|field|{ReturnType}|{(IsStatic ? "static" : "instance")}"
        : $"{Type}|{Name}|{string.Join(",", Parameters)}|{ReturnType}|{(IsStatic ? "static" : "instance")}";

    public override string ToString() => Signature;

    /// <summary>Reads the text form (see the type's summary); anything after '#' is a comment.</summary>
    public static ClientMember Parse(string line)
    {
        int hash = line.IndexOf('#');
        string text = (hash >= 0 ? line.Substring(0, hash) : line).Trim();
        var parts = text.Split('|');
        if (parts.Length != 5 || parts[0].Length == 0 || parts[1].Length == 0 || parts[3].Length == 0
            || (parts[4] != "static" && parts[4] != "instance"))
        {
            throw new FormatException($"not a client member: '{line}'");
        }

        bool isStatic = parts[4] == "static";
        if (parts[2] == "field")
        {
            return new ClientMember(parts[0], parts[1], Array.Empty<string>(), parts[3], isStatic, ClientMemberKind.Field);
        }

        var parameters = parts[2].Length == 0 ? Array.Empty<string>() : parts[2].Split(',').Select(p => p.Trim()).ToArray();
        return new ClientMember(parts[0], parts[1], parameters, parts[3], isStatic, ClientMemberKind.Method);
    }
}
