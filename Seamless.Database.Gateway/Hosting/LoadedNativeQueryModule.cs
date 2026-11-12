using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Hosting;

/// <summary>クエリだけを collectible ALC に載せ、DB 型は依存先の実際の世代と共有する。</summary>
public sealed class LoadedNativeQueryModule:IDisposable{
    private NativeLoadContext? context;
    private INativeQueryModule? module;
    public WeakReference UnloadReference{get;}
    public INativeQueryModule Module=>this.module??throw new ObjectDisposedException(nameof(LoadedNativeQueryModule));
    public NativeModuleManifest AddContracts(NativeModuleManifest manifest)=>manifest with{Queries=Array.AsReadOnly(manifest.Queries.Select(query=>{
        var contracts=this.Contracts(query);return query with{ArgumentsContract=contracts.Arguments,ResultContract=contracts.Result};
    }).ToArray())};
    private (string Arguments,string Result) Contracts(NativeQueryDefinition query){
        if(this.Module is INativeRecordQueryModule records){
            // 生成契約を先に取得し、別DLLのDTOも通常の依存解決で確定させる。
            var contract=records.GetContract(query.Id);
            var arguments=this.ResolveContractType(query.ArgumentsType);var result=this.ResolveContractType(query.ResultType);
            if(contract.ArgumentsType!=arguments||contract.ResultType!=result){throw new InvalidDataException("The native record module types differ from their declaration.");}
            return(contract.ArgumentsContract,contract.ResultContract);
        }
        return(NativeWireContract.For(this.ResolveContractType(query.ArgumentsType)),NativeWireContract.For(this.ResolveContractType(query.ResultType)));
    }
    internal Type ResolveContractType(string name){
        name=name.Trim();if(name.EndsWith("[]",StringComparison.Ordinal)){return this.ResolveContractType(name[..^2]).MakeArrayType();}
        var alias=name switch{"int"=>typeof(int),"long"=>typeof(long),"short"=>typeof(short),"byte"=>typeof(byte),"bool"=>typeof(bool),"string"=>typeof(string),"decimal"=>typeof(decimal),"double"=>typeof(double),"float"=>typeof(float),_=>null};if(alias is not null){return alias;}
        var moduleAssembly=this.Module.GetType().Assembly;
        var assemblies=new[]{moduleAssembly}.Concat(AssemblyLoadContext.GetLoadContext(moduleAssembly)!.Assemblies)
            .Concat(this.context!.Databases.Values).Concat(AssemblyLoadContext.Default.Assemblies).Distinct().ToArray();
        var resolved=Type.GetType(name,assembly=>assemblies.FirstOrDefault(candidate=>AssemblyName.ReferenceMatchesDefinition(candidate.GetName(),assembly)),(assembly,type,ignoreCase)=>assembly?.GetType(type,false,ignoreCase)??assemblies.Select(candidate=>candidate.GetType(type,false,ignoreCase)).FirstOrDefault(static candidate=>candidate is not null),false);
        return resolved??throw new InvalidDataException("The native wire contract type cannot be resolved: "+name);
    }
    internal bool UsesDatabaseAssembly(string assemblyName,Assembly assembly)=>this.context is{} context&&context.Databases.TryGetValue(assemblyName,out var bound)&&ReferenceEquals(bound,assembly);
    public LoadedNativeQueryModule(NativeQueryBuildArtifact artifact,IReadOnlyDictionary<string,Assembly> databases){
        ArgumentNullException.ThrowIfNull(artifact);ArgumentNullException.ThrowIfNull(databases);
        CollectibleFormatterCache.Validate();
        using(var file=File.OpenRead(artifact.SourceProjectPath)){if(Convert.ToHexString(SHA256.HashData(file))!=artifact.SourceProjectSha256){throw new InvalidDataException("Native query source project hash mismatch.");}}
        using(var file=File.OpenRead(artifact.AssemblyPath)){if(Convert.ToHexString(SHA256.HashData(file))!=artifact.Sha256){throw new InvalidDataException("Native query assembly hash mismatch.");}}
        var assemblies=new Dictionary<string,Assembly>(StringComparer.OrdinalIgnoreCase);
        foreach(var dependency in artifact.Dependencies){
            if(!databases.TryGetValue(dependency.Database,out var assembly)||assembly.GetName().Name!=dependency.AssemblyName){throw new InvalidDataException("Native query database assembly mismatch: "+dependency.Database);}
            using var file=File.OpenRead(assembly.Location);if(Convert.ToHexString(SHA256.HashData(file))!=dependency.Sha256){throw new InvalidDataException("Native query database hash mismatch: "+dependency.Database);}
            if(NativeDatabaseIdentity.Fingerprint(assembly)!=dependency.SchemaFingerprint){throw new InvalidDataException("Native query schema fingerprint mismatch: "+dependency.Database);}
            assemblies.Add(dependency.AssemblyName,assembly);
        }
        var loading=new NativeLoadContext(assemblies);this.context=loading;this.UnloadReference=new(loading);
        try{
            Assembly assembly;
            var name=AssemblyName.GetAssemblyName(artifact.AssemblyPath).Name!;
            if(assemblies.TryGetValue(name,out var shared)){
                // DBと同じDLLのクエリはDB所有の型を使う。別ALCの同名型へ実行中のDBを渡せない。
                using var file=File.OpenRead(shared.Location);
                if(Convert.ToHexString(SHA256.HashData(file))!=artifact.Sha256){throw new InvalidDataException("The native query and database assembly builds differ.");}
                assembly=shared;
            }else{
                assembly=loading.LoadFromAssemblyPath(artifact.AssemblyPath);
            }
            this.module=Activator.CreateInstance(assembly.GetType(artifact.Manifest.EntryType,true)!) as INativeQueryModule??throw new InvalidDataException("Native query entry does not implement INativeQueryModule.");
            foreach(var query in artifact.Manifest.Queries){var contracts=this.Contracts(query);if(query.ArgumentsContract is{} arguments&&arguments!=contracts.Arguments||query.ResultContract is{} result&&result!=contracts.Result){throw new InvalidDataException("Native query wire contract hash mismatch: "+query.Id);}}
        }
        catch{this.Dispose();throw;}
    }
    public void Dispose(){this.module=null;var loading=this.context;this.context=null;if(loading is not null){CollectibleFormatterCache.Release(loading);loading.Unload();}}
    private sealed class NativeLoadContext(Dictionary<string,Assembly> databases):AssemblyLoadContext(isCollectible:true){
        internal IReadOnlyDictionary<string,Assembly> Databases=>databases;
        protected override Assembly? Load(AssemblyName name){
            if(name.Name is not null&&databases.TryGetValue(name.Name,out var database)){if(!AssemblyName.ReferenceMatchesDefinition(database.GetName(),name)){throw new FileLoadException("Native database assembly identity mismatch.");}return database;}
            if(name.Name?.StartsWith("Seamless.Generated.Generation_",StringComparison.Ordinal)==true){throw new FileLoadException("The native query references an undeclared database generation.");}
            return Default.Assemblies.FirstOrDefault(assembly=>AssemblyName.ReferenceMatchesDefinition(assembly.GetName(),name));
        }
    }
}
