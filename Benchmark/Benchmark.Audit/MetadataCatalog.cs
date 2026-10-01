using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace Benchmark.Audit;

/// <summary>読み込んだ1アセンブリのメタデータ。</summary>
internal sealed class LoadedAssembly:IDisposable {
    private readonly PEReader peReader;
    private Dictionary<string,TypeDefinitionHandle>? typesByFullName;

    public LoadedAssembly(string path) {
        this.Path=path;
        this.peReader=new PEReader(File.OpenRead(path));
        this.Reader=this.peReader.GetMetadataReader();
        this.Name=this.Reader.GetString(this.Reader.GetAssemblyDefinition().Name);
    }

    public string Path { get; }
    public string Name { get; }
    public MetadataReader Reader { get; }

    public MethodBodyBlock? GetMethodBody(MethodDefinitionHandle handle) {
        var definition = this.Reader.GetMethodDefinition(handle);
        if(definition.RelativeVirtualAddress==0) {
            return null;
        }
        return this.peReader.GetMethodBody(definition.RelativeVirtualAddress);
    }

    public bool TryFindType(string fullName,out TypeDefinitionHandle handle) {
        if(this.typesByFullName is null) {
            this.typesByFullName=new Dictionary<string,TypeDefinitionHandle>(StringComparer.Ordinal);
            foreach(var typeHandle in this.Reader.TypeDefinitions) {
                this.typesByFullName.TryAdd(MetadataNames.TypeDefinitionFullName(this.Reader,typeHandle),typeHandle);
            }
        }
        return this.typesByFullName.TryGetValue(fullName,out handle);
    }

    public void Dispose() => this.peReader.Dispose();
}

/// <summary>型の実体(どのアセンブリのどの TypeDefinition か)。</summary>
internal readonly record struct ResolvedType(LoadedAssembly Assembly,TypeDefinitionHandle Handle) {
    public TypeDefinition Definition => this.Assembly.Reader.GetTypeDefinition(this.Handle);
    public string FullName => MetadataNames.TypeDefinitionFullName(this.Assembly.Reader,this.Handle);
}

/// <summary>監査対象と参照先(ランタイム・同じフォルダー)のアセンブリを名前で引けるようにする。</summary>
internal sealed class MetadataCatalog:IDisposable {
    private readonly List<string> probeDirectories = [];
    private readonly Dictionary<string,LoadedAssembly?> byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LoadedAssembly> loaded = [];

    public MetadataCatalog() {
        this.probeDirectories.Add(RuntimeEnvironment.GetRuntimeDirectory());
    }

    public LoadedAssembly Load(string path) {
        var fullPath = System.IO.Path.GetFullPath(path);
        var directory = System.IO.Path.GetDirectoryName(fullPath);
        if(directory is not null&&!this.probeDirectories.Contains(directory,StringComparer.OrdinalIgnoreCase)) {
            this.probeDirectories.Insert(0,directory);
        }
        var assembly = new LoadedAssembly(fullPath);
        this.loaded.Add(assembly);
        this.byName[assembly.Name]=assembly;
        return assembly;
    }

    public LoadedAssembly? Find(string assemblyName) {
        if(this.byName.TryGetValue(assemblyName,out var cached)) {
            return cached;
        }
        LoadedAssembly? found = null;
        foreach(var directory in this.probeDirectories) {
            var candidate = System.IO.Path.Combine(directory,assemblyName+".dll");
            if(File.Exists(candidate)) {
                found=new LoadedAssembly(candidate);
                this.loaded.Add(found);
                break;
            }
        }
        this.byName[assemblyName]=found;
        return found;
    }

    /// <summary>TypeDefinition/TypeReference/TypeSpecification を実体へ解決する。解決できなければ null。</summary>
    public ResolvedType? Resolve(LoadedAssembly assembly,EntityHandle handle) {
        switch(handle.Kind) {
            case HandleKind.TypeDefinition:
                return new ResolvedType(assembly,(TypeDefinitionHandle)handle);
            case HandleKind.TypeReference:
                return this.ResolveReference(assembly,(TypeReferenceHandle)handle);
            case HandleKind.TypeSpecification: {
                var specification = assembly.Reader.GetTypeSpecification((TypeSpecificationHandle)handle);
                var shape = specification.DecodeSignature(new SignatureTypeCollector(),null);
                if(shape.Kind is SigTypeKind.Definition or SigTypeKind.Reference) {
                    return this.Resolve(assembly,shape.Handle);
                }
                return null;
            }
            default:
                return null;
        }
    }

    private ResolvedType? ResolveReference(LoadedAssembly assembly,TypeReferenceHandle handle) {
        var reader = assembly.Reader;
        var reference = reader.GetTypeReference(handle);
        var scope = reference.ResolutionScope;
        if(scope.Kind==HandleKind.TypeReference) {
            var parent = this.ResolveReference(assembly,(TypeReferenceHandle)scope);
            if(parent is not { } parentType) {
                return null;
            }
            var nestedName = reader.GetString(reference.Name);
            foreach(var nestedHandle in parentType.Definition.GetNestedTypes()) {
                var nested = parentType.Assembly.Reader.GetTypeDefinition(nestedHandle);
                if(parentType.Assembly.Reader.GetString(nested.Name)==nestedName) {
                    return new ResolvedType(parentType.Assembly,nestedHandle);
                }
            }
            return null;
        }
        var fullName = MetadataNames.TypeReferenceFullName(reader,handle);
        if(scope.Kind==HandleKind.AssemblyReference) {
            var assemblyName = reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name);
            return this.FindByFullName(assemblyName,fullName,0);
        }
        if(assembly.TryFindType(fullName,out var local)) {
            return new ResolvedType(assembly,local);
        }
        return null;
    }

    private ResolvedType? FindByFullName(string assemblyName,string fullName,int depth) {
        if(depth>8) {
            return null;
        }
        var target = this.Find(assemblyName);
        if(target is null) {
            return null;
        }
        if(target.TryFindType(fullName,out var definition)) {
            return new ResolvedType(target,definition);
        }
        foreach(var exportedHandle in target.Reader.ExportedTypes) {
            var exported = target.Reader.GetExportedType(exportedHandle);
            var exportedName = MetadataNames.Join(target.Reader.GetString(exported.Namespace),target.Reader.GetString(exported.Name));
            if(exportedName!=fullName||exported.Implementation.Kind!=HandleKind.AssemblyReference) {
                continue;
            }
            var forwardedTo = target.Reader.GetString(target.Reader.GetAssemblyReference((AssemblyReferenceHandle)exported.Implementation).Name);
            return this.FindByFullName(forwardedTo,fullName,depth+1);
        }
        return null;
    }

    public bool IsInterface(ResolvedType type) => (type.Definition.Attributes&TypeAttributes.Interface)!=0;

    public string? BaseTypeFullName(ResolvedType type) {
        var baseType = type.Definition.BaseType;
        if(baseType.IsNil) {
            return null;
        }
        var reader = type.Assembly.Reader;
        switch(baseType.Kind) {
            case HandleKind.TypeDefinition:
                return MetadataNames.TypeDefinitionFullName(reader,(TypeDefinitionHandle)baseType);
            case HandleKind.TypeReference:
                return MetadataNames.TypeReferenceFullName(reader,(TypeReferenceHandle)baseType);
            default: {
                var resolved = this.Resolve(type.Assembly,baseType);
                if(resolved is { } resolvedType) {
                    return resolvedType.FullName;
                }
                return null;
            }
        }
    }

    public bool IsValueType(ResolvedType type) {
        var baseName = this.BaseTypeFullName(type);
        return baseName is "System.ValueType" or "System.Enum";
    }

    public bool IsDelegate(ResolvedType type) => this.BaseTypeFullName(type) is "System.MulticastDelegate";

    public void Dispose() {
        foreach(var assembly in this.loaded) {
            assembly.Dispose();
        }
        this.loaded.Clear();
        this.byName.Clear();
    }
}

internal static class MetadataNames {
    public static string Join(string ns,string name) {
        if(ns.Length==0) {
            return name;
        }
        return ns+"."+name;
    }

    public static string TypeDefinitionFullName(MetadataReader reader,TypeDefinitionHandle handle) {
        var definition = reader.GetTypeDefinition(handle);
        var name = reader.GetString(definition.Name);
        var declaring = definition.GetDeclaringType();
        if(!declaring.IsNil) {
            return TypeDefinitionFullName(reader,declaring)+"+"+name;
        }
        return Join(reader.GetString(definition.Namespace),name);
    }

    public static string TypeReferenceFullName(MetadataReader reader,TypeReferenceHandle handle) {
        var reference = reader.GetTypeReference(handle);
        var name = reader.GetString(reference.Name);
        if(reference.ResolutionScope.Kind==HandleKind.TypeReference) {
            return TypeReferenceFullName(reader,(TypeReferenceHandle)reference.ResolutionScope)+"+"+name;
        }
        return Join(reader.GetString(reference.Namespace),name);
    }

    /// <summary>入れ子をたどった最外側の型の名前空間。</summary>
    public static string OutermostNamespace(MetadataReader reader,TypeDefinitionHandle handle) {
        var current = reader.GetTypeDefinition(handle);
        while(!current.GetDeclaringType().IsNil) {
            current=reader.GetTypeDefinition(current.GetDeclaringType());
        }
        return reader.GetString(current.Namespace);
    }
}

internal enum SigTypeKind {
    Definition,
    Reference,
    Primitive,
    Array,
    Pointer,
    GenericParameter,
    Other,
}

/// <summary>署名を読むときに型の形だけを返す値。</summary>
internal readonly record struct SigType(SigTypeKind Kind,EntityHandle Handle);

/// <summary>署名を読み、現れた TypeDefinition をすべて集める。ジェネリック実体化は元の型を返す。</summary>
internal sealed class SignatureTypeCollector:ISignatureTypeProvider<SigType,object?> {
    public HashSet<TypeDefinitionHandle> Definitions { get; } = [];

    public SigType GetArrayType(SigType elementType,ArrayShape shape) => new(SigTypeKind.Array,default);
    public SigType GetByReferenceType(SigType elementType) => elementType;
    public SigType GetFunctionPointerType(MethodSignature<SigType> signature) => new(SigTypeKind.Pointer,default);
    public SigType GetGenericInstantiation(SigType genericType,ImmutableArray<SigType> typeArguments) => genericType;
    public SigType GetGenericMethodParameter(object? genericContext,int index) => new(SigTypeKind.GenericParameter,default);
    public SigType GetGenericTypeParameter(object? genericContext,int index) => new(SigTypeKind.GenericParameter,default);
    public SigType GetModifiedType(SigType modifier,SigType unmodifiedType,bool isRequired) => unmodifiedType;
    public SigType GetPinnedType(SigType elementType) => elementType;
    public SigType GetPointerType(SigType elementType) => new(SigTypeKind.Pointer,default);
    public SigType GetPrimitiveType(PrimitiveTypeCode typeCode) => new(SigTypeKind.Primitive,default);
    public SigType GetSZArrayType(SigType elementType) => new(SigTypeKind.Array,default);

    public SigType GetTypeFromDefinition(MetadataReader reader,TypeDefinitionHandle handle,byte rawTypeKind) {
        this.Definitions.Add(handle);
        return new SigType(SigTypeKind.Definition,handle);
    }

    public SigType GetTypeFromReference(MetadataReader reader,TypeReferenceHandle handle,byte rawTypeKind) =>
        new(SigTypeKind.Reference,handle);

    public SigType GetTypeFromSpecification(MetadataReader reader,object? genericContext,TypeSpecificationHandle handle,byte rawTypeKind) =>
        reader.GetTypeSpecification(handle).DecodeSignature(this,genericContext);
}
