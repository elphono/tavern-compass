using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace TavernCompass.NoDance.Core.Tests;

/// <summary>
/// Reads an assembly's metadata only (System.Reflection.Metadata: no code of it is loaded or run) and writes its
/// members in the text form of <see cref="ClientMember"/>. Used on the installed game client, which must never be
/// loaded by the tests, and on witness assemblies of this repository.
/// </summary>
internal sealed class MetadataSignatures : IDisposable
{
    private readonly FileStream _stream;
    private readonly PEReader _pe;
    private readonly MetadataReader _md;
    private readonly NameProvider _names;
    private readonly Dictionary<string, TypeDefinitionHandle> _types = new();

    public MetadataSignatures(string path)
    {
        _stream = File.OpenRead(path);
        _pe = new PEReader(_stream);
        _md = _pe.GetMetadataReader();
        _names = new NameProvider();
        foreach (var handle in _md.TypeDefinitions)
        {
            _types[NameProvider.Definition(_md, handle)] = handle;
        }
    }

    public void Dispose()
    {
        _pe.Dispose();
        _stream.Dispose();
    }

    /// <summary>Every overload of the member (or the field) that the type declares, in text form; empty if none.</summary>
    public IReadOnlyList<string> Signatures(string type, string name)
    {
        var found = new List<string>();
        if (!_types.TryGetValue(type, out var handle))
        {
            return found;
        }

        var definition = _md.GetTypeDefinition(handle);
        foreach (var methodHandle in definition.GetMethods())
        {
            var method = _md.GetMethodDefinition(methodHandle);
            if (_md.GetString(method.Name) != name)
            {
                continue;
            }

            var signature = method.DecodeSignature(_names, null);
            bool isStatic = (method.Attributes & MethodAttributes.Static) != 0;
            found.Add($"{type}|{name}|{string.Join(",", signature.ParameterTypes)}|{signature.ReturnType}|{(isStatic ? "static" : "instance")}");
        }

        foreach (var fieldHandle in definition.GetFields())
        {
            var field = _md.GetFieldDefinition(fieldHandle);
            if (_md.GetString(field.Name) != name)
            {
                continue;
            }

            bool isStatic = (field.Attributes & FieldAttributes.Static) != 0;
            found.Add($"{type}|{name}|field|{field.DecodeSignature(_names, null)}|{(isStatic ? "static" : "instance")}");
        }

        return found;
    }

    public bool Contains(ClientMember member) => Signatures(member.Type, member.Name).Contains(member.Signature);

    /// <summary>The parameter names of the method with exactly this signature, or null if it is not there.</summary>
    public IReadOnlyList<string>? ParameterNames(ClientMember member)
    {
        if (!_types.TryGetValue(member.Type, out var handle))
        {
            return null;
        }

        foreach (var methodHandle in _md.GetTypeDefinition(handle).GetMethods())
        {
            var method = _md.GetMethodDefinition(methodHandle);
            if (_md.GetString(method.Name) != member.Name)
            {
                continue;
            }

            var signature = method.DecodeSignature(_names, null);
            bool isStatic = (method.Attributes & MethodAttributes.Static) != 0;
            string text = $"{member.Type}|{member.Name}|{string.Join(",", signature.ParameterTypes)}|{signature.ReturnType}|{(isStatic ? "static" : "instance")}";
            if (text != member.Signature)
            {
                continue;
            }

            var names = new string[signature.ParameterTypes.Length];
            foreach (var parameterHandle in method.GetParameters())
            {
                var parameter = _md.GetParameter(parameterHandle);
                if (parameter.SequenceNumber > 0 && parameter.SequenceNumber <= names.Length)
                {
                    names[parameter.SequenceNumber - 1] = _md.GetString(parameter.Name);
                }
            }

            return names;
        }

        return null;
    }

    /// <summary>The assemblies this one references, by name.</summary>
    public IReadOnlyList<string> AssemblyReferences() =>
        _md.AssemblyReferences.Select(h => _md.GetString(_md.GetAssemblyReference(h).Name)).ToList();

    /// <summary>The custom attributes on the types this assembly defines, by attribute type name.</summary>
    public IReadOnlyList<string> TypeAttributes() =>
        _md.TypeDefinitions
            .SelectMany(h => _md.GetTypeDefinition(h).GetCustomAttributes())
            .Select(h => AttributeTypeName(_md.GetCustomAttribute(h)))
            .ToList();

    /// <summary>
    /// The members of <paramref name="assemblyName"/> this assembly references (calls, constructs, reads), in text
    /// form: what a mod built against the game would need from it at run time.
    /// </summary>
    public IReadOnlyList<string> MemberReferencesInto(string assemblyName)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var handle in _md.MemberReferences)
        {
            var reference = _md.GetMemberReference(handle);
            if (reference.Parent.Kind != HandleKind.TypeReference)
            {
                continue;
            }

            var parent = (TypeReferenceHandle)reference.Parent;
            if (RootAssembly(parent) != assemblyName)
            {
                continue;
            }

            string type = NameProvider.Reference(_md, parent);
            string name = _md.GetString(reference.Name);
            if (reference.GetKind() == MemberReferenceKind.Field)
            {
                found.Add($"{type}|{name}|field|{reference.DecodeFieldSignature(_names, null)}|instance");
            }
            else
            {
                var signature = reference.DecodeMethodSignature(_names, null);
                found.Add($"{type}|{name}|{string.Join(",", signature.ParameterTypes)}|{signature.ReturnType}|"
                    + (signature.Header.IsInstance ? "instance" : "static"));
            }
        }

        return found.ToList();
    }

    private string RootAssembly(TypeReferenceHandle handle)
    {
        var scope = _md.GetTypeReference(handle).ResolutionScope;
        while (scope.Kind == HandleKind.TypeReference)
        {
            scope = _md.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
        }

        return scope.Kind == HandleKind.AssemblyReference
            ? _md.GetString(_md.GetAssemblyReference((AssemblyReferenceHandle)scope).Name)
            : string.Empty;
    }

    private string AttributeTypeName(CustomAttribute attribute)
    {
        if (attribute.Constructor.Kind == HandleKind.MemberReference)
        {
            var parent = _md.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
            return parent.Kind == HandleKind.TypeReference ? NameProvider.Reference(_md, (TypeReferenceHandle)parent) : string.Empty;
        }

        if (attribute.Constructor.Kind == HandleKind.MethodDefinition)
        {
            var declaring = _md.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType();
            return NameProvider.Definition(_md, declaring);
        }

        return string.Empty;
    }

    private sealed class NameProvider : ISignatureTypeProvider<string, object?>
    {
        public string GetPrimitiveType(PrimitiveTypeCode typeCode)
        {
            string fullName = "System." + typeCode;
            return ClientSignature.PrimitiveName(fullName) ?? fullName;
        }

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
            Named(Definition(reader, handle));

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
            Named(Reference(reader, handle));

        public string GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle,
            byte rawTypeKind) => reader.GetTypeSpecification(handle).DecodeSignature(this, context);

        public string GetSZArrayType(string elementType) => elementType + "[]";

        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";

        public string GetByReferenceType(string elementType) => elementType + "&";

        public string GetPointerType(string elementType) => elementType + "*";

        public string GetPinnedType(string elementType) => elementType;

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
            genericType + "<" + string.Join(";", typeArguments) + ">";

        public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;

        public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

        public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";

        /// <summary>A type written by name (System.Int32 as a value type, say) gets the same C# name as a primitive.</summary>
        private static string Named(string fullName) => ClientSignature.PrimitiveName(fullName) ?? fullName;

        public static string Definition(MetadataReader reader, TypeDefinitionHandle handle)
        {
            var definition = reader.GetTypeDefinition(handle);
            string name = reader.GetString(definition.Name);
            var declaring = definition.GetDeclaringType();
            if (!declaring.IsNil)
            {
                return Definition(reader, declaring) + "." + name;
            }

            string ns = reader.GetString(definition.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }

        public static string Reference(MetadataReader reader, TypeReferenceHandle handle)
        {
            var reference = reader.GetTypeReference(handle);
            string name = reader.GetString(reference.Name);
            if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                return Reference(reader, (TypeReferenceHandle)reference.ResolutionScope) + "." + name;
            }

            string ns = reader.GetString(reference.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }
    }
}
