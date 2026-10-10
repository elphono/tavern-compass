using System;
using System.Linq;
using System.Reflection;

namespace TavernCompass.NoDance.Core;

/// <summary>
/// Writes types and members in the text form of <see cref="ClientMember"/>, from System.Reflection, and finds a
/// <see cref="ClientMember"/> in a loaded assembly. The mod uses it on the game's Assembly-CSharp when it loads; the
/// signature test writes the same form from the file's metadata, without loading it.
/// </summary>
public static class ClientSignature
{
    public const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>The C# name of the primitive types the form spells out (void, bool, int, float, string, object), or null.</summary>
    public static string? PrimitiveName(string fullName) => fullName switch
    {
        "System.Void" => "void",
        "System.Boolean" => "bool",
        "System.Int32" => "int",
        "System.Single" => "float",
        "System.String" => "string",
        "System.Object" => "object",
        _ => null,
    };

    public static string Of(Type type)
    {
        if (type.IsByRef)
        {
            return Of(type.GetElementType()!) + "&";
        }

        if (type.IsPointer)
        {
            return Of(type.GetElementType()!) + "*";
        }

        if (type.IsArray)
        {
            int rank = type.GetArrayRank();
            return Of(type.GetElementType()!) + (rank == 1 ? "[]" : "[" + new string(',', rank - 1) + "]");
        }

        if (type.IsGenericParameter)
        {
            return (type.DeclaringMethod != null ? "!!" : "!") + type.GenericParameterPosition;
        }

        if (type.IsGenericType && !type.IsGenericTypeDefinition)
        {
            return Name(type.GetGenericTypeDefinition()) + "<" + string.Join(";", type.GetGenericArguments().Select(Of)) + ">";
        }

        string name = Name(type);
        return PrimitiveName(name) ?? name;
    }

    /// <summary>The member's text form, as <see cref="ClientMember.Signature"/> writes it.</summary>
    public static string Of(MemberInfo member) => member switch
    {
        FieldInfo field => $"{Name(field.DeclaringType!)}|{field.Name}|field|{Of(field.FieldType)}|{(field.IsStatic ? "static" : "instance")}",
        MethodBase method => $"{Name(method.DeclaringType!)}|{method.Name}|"
            + string.Join(",", method.GetParameters().Select(p => Of(p.ParameterType)))
            + $"|{(method is MethodInfo info ? Of(info.ReturnType) : "void")}|{(method.IsStatic ? "static" : "instance")}",
        _ => throw new ArgumentException($"not a field or a method: {member}", nameof(member)),
    };

    /// <summary>
    /// The type named in the form (dots for both namespaces and nesting): tries the name as a namespace path, then
    /// turns the last dots into nesting one by one.
    /// </summary>
    public static Type? FindType(Assembly assembly, string name)
    {
        var parts = name.Split('.');
        for (int nested = 0; nested < parts.Length; nested++)
        {
            int outer = parts.Length - nested;
            string candidate = string.Join(".", parts.Take(outer)) + string.Concat(parts.Skip(outer).Select(p => "+" + p));
            var type = assembly.GetType(candidate, throwOnError: false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    /// <summary>
    /// The member of <paramref name="assembly"/> with exactly this signature, among the members its type declares
    /// itself; null if the type or the member is not there.
    /// </summary>
    public static MemberInfo? Find(Assembly assembly, ClientMember member)
    {
        var type = FindType(assembly, member.Type);
        if (type == null)
        {
            return null;
        }

        if (member.Kind == ClientMemberKind.Field)
        {
            var field = type.GetField(member.Name, Declared);
            return field != null && Of(field) == member.Signature ? field : null;
        }

        MethodBase[] candidates = member.Name == ".ctor"
            ? type.GetConstructors(Declared)
            : type.GetMethods(Declared).Where(m => m.Name == member.Name).ToArray();
        return candidates.FirstOrDefault(m => Of(m) == member.Signature);
    }

    private static string Name(Type type)
    {
        if (type.IsNested)
        {
            return Name(type.DeclaringType!) + "." + type.Name;
        }

        return string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace + "." + type.Name;
    }
}
