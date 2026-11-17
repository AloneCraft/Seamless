using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.Json;

namespace Seamless.Execution;

/// <summary>製品DLLを統合する前の型識別子を、保存形式とハッシュ計算へ引き継ぐ。</summary>
internal static class PersistedTypeIdentity{
    private static readonly IReadOnlyDictionary<string,string> legacy=ReadLegacy();
    private static readonly IReadOnlyDictionary<string,string> assemblies=ReadNames();

    internal static string Name(Type type)=>FullName(type)+", "+AssemblyIdentity(type);
    internal static string AssemblySimpleName(Type type)=>new AssemblyName(AssemblyIdentity(type)).Name!;
    internal static string AssemblyNameFor(string name)=>assemblies.GetValueOrDefault(name,name);
    internal static Type Resolve(string name)=>Type.GetType(name,identity=>Assembly.Load(AssemblyNameFor(identity.Name!)),null,true)!;

    internal static string FullName(Type type){
        if(type.IsArray){
            var suffix="[]";
            if(!type.IsSZArray){suffix="[*]";}
            if(type.GetArrayRank()>1){suffix="["+new string(',',type.GetArrayRank()-1)+"]";}
            return FullName(type.GetElementType()!)+suffix;
        }
        if(type.IsConstructedGenericType){
            var arguments=string.Join(",",type.GenericTypeArguments.Select(argument=>"["+Name(argument)+"]"));
            return type.GetGenericTypeDefinition().FullName+"["+arguments+"]";
        }
        return type.FullName??throw new NotSupportedException("An open generic parameter has no persisted type identity.");
    }

    private static string AssemblyIdentity(Type type){
        while(type.HasElementType){type=type.GetElementType()!;}
        if(type.IsConstructedGenericType){type=type.GetGenericTypeDefinition();}
        if(type.Assembly.GetName().Name is "Runtime" or "Database" or "Client" or "Server"&&type.FullName is{} name&&legacy.TryGetValue(name,out var identity)){return identity;}
        return type.Assembly.FullName!;
    }

    private static IReadOnlyDictionary<string,string> ReadLegacy(){
        using var stream=typeof(PersistedTypeIdentity).Assembly.GetManifestResourceStream("Seamless.Execution.LegacyAssemblyTypes.json")??throw new InvalidDataException("Legacy persisted type identities are missing.");
        return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??throw new InvalidDataException("Invalid legacy persisted type identities.");
    }
    private static IReadOnlyDictionary<string,string> ReadNames(){
        using var stream=typeof(PersistedTypeIdentity).Assembly.GetManifestResourceStream("Seamless.Execution.LegacyAssemblyNames.json")??throw new InvalidDataException("Legacy assembly names are missing.");
        return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??throw new InvalidDataException("Invalid legacy assembly names.");
    }
}
