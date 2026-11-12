using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace Seamless.Management.Server;

internal sealed partial class NativeRuntime{
    internal FunctionDetails RegisteredFunctionDetails(DefinitionVersion query,DefinitionVersion database,FunctionTarget target){
        using var databaseBuild=this.ReadArtifact(database);using var queryBuild=this.ReadArtifact(query);
        var manifestPath=this.Full(Path.Combine(query.StoragePath,"manifest.json"));SourceWorkspace.CheckLinks(manifestPath);
        using(var manifestFile=File.OpenRead(manifestPath)){
            if(manifestFile.Length>64L<<20){throw new ManagementException("Limit","ビルド情報が上限を超えています。");}
            var manifest=JsonSerializer.Deserialize<ArtifactManifest>(manifestFile,ManagementJson.Options)??throw new ManagementException("Validation","ビルド情報がありません。");
            if(manifest.DatabaseArtifact!=database.ArtifactHash){throw new ManagementException("SchemaMismatch","登録関数の参照DB世代が異なります。");}
        }
        var databaseContext=new ManagedLoadContext(imported:databaseBuild.ImportedBuild);ManagedLoadContext? queryContext=null;
        try{
            using var databaseStream=new MemoryStream(databaseBuild.AssemblyBytes);var databaseAssembly=databaseContext.LoadFromStream(databaseStream);
            queryContext=new(databaseAssembly,queryBuild.ImportedBuild);
            using var queryStream=new MemoryStream(queryBuild.AssemblyBytes);var assembly=queryContext.LoadFromStream(queryStream);
            var type=assembly.GetType(query.Definition.EntryType,true)!;
            var methods=type.GetMethods(BindingFlags.Public|BindingFlags.Static).Where(method=>method.Name==query.Definition.EntryMember&&!method.IsGenericMethod&&method.GetParameters().All(parameter=>!parameter.ParameterType.IsByRef)).ToArray();
            if(methods.Length!=1){throw new ManagementException("Validation","公開された一意のstatic入口を確認してください。");}
            var method=methods[0];var audit=this.store.ReadAuditMethod(query.ArtifactHash,query.Definition.Source.Hash());
            if(audit is not null&&(audit.ModuleId!=method.Module.ModuleVersionId||audit.MetadataToken!=method.MetadataToken)){throw new ManagementException("ArtifactMismatch","公開入口と監査メタデータが一致しません。");}
            var parameters=method.GetParameters().Where(parameter=>parameter.ParameterType!=typeof(ManagementExecutionContext)).Select(parameter=>new CatalogField(parameter.Name??throw new ManagementException("Validation","引数名がありません。"),TypeName(parameter.ParameterType))).ToArray();
            var returns=method.ReturnType;var sequence=typeof(IEnumerable).IsAssignableFrom(returns)&&returns!=typeof(string);
            Type? row=null;
            if(sequence){row=returns.GetInterfaces().Append(returns).FirstOrDefault(item=>item.IsGenericType&&item.GetGenericTypeDefinition()==typeof(IEnumerable<>))?.GenericTypeArguments[0];}
            var columns=Array.Empty<CatalogField>();if(row is not null){columns=RuntimeValueJson.Columns(row);}
            var unsupported=typeof(Task).IsAssignableFrom(returns)||returns==typeof(ValueTask)||returns.IsGenericType&&(returns.GetGenericTypeDefinition()==typeof(ValueTask<>)||returns.GetGenericTypeDefinition()==typeof(IAsyncEnumerable<>));
            string? reason=null;if(unsupported){reason="AsyncEntryUnsupported";}
            var summary=new FunctionSummary(target,query.Definition.Id,"",query.Version,query.ArtifactHash,query.Definition.WritesData){DeclaringType=query.Definition.EntryType,EntryMember=query.Definition.EntryMember};
            string? element=null;if(row is not null){element=TypeName(row);}
            return new(summary,parameters,TypeName(returns),sequence,columns,new(query.Definition.Id,query.Version,query.ArtifactHash,null,0,0,false){UnavailableReason="SourceNotCollected"}){ElementType=element,UnavailableReason=reason};
        }finally{queryContext?.Unload();databaseContext.Unload();}
    }
    private static string TypeName(Type type)=>type.FullName??type.ToString();
}
