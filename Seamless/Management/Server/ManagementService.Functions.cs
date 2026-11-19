using System.Text;
using System.Text.Json;

namespace Seamless.Management.Server;

public sealed partial class ManagementService{
    private const long RegisteredFunctionCursor=1L<<62;
    public CatalogPage<FunctionSummary> Functions(UserAccount user,CatalogRequest request){
        lock(this.store.Gate){
            var database=this.CatalogVersion(user,request.DatabaseId)??throw new ManagementException("InvalidRequest","利用者DBを指定してください。");
            var token=this.CatalogToken(request.VersionToken);
            if(request.Cursor<0){throw new ManagementException("InvalidRequest","ページ指定が不正です。");}
            if(request.Cursor<RegisteredFunctionCursor){
                var members=this.CatalogRead(user,request,(_,items)=>items.Select(item=>item.Details.Object).Where(item=>item.Kind is "ScalarFunction" or "TableFunction")
                    .Where(item=>(request.Schema is null||item.Schema==request.Schema)&&(string.IsNullOrEmpty(request.NameFilter)||item.Name.Contains(request.NameFilter,StringComparison.OrdinalIgnoreCase)))
                    .Select(item=>new FunctionSummary(new(FunctionOrigin.DatabaseMember,request.DatabaseId,database.Definition.Id,item.Id),item.Name,item.Schema,database.Version,database.ArtifactHash,false)).ToArray());
                var page=Slice(members,request.Cursor,token);
                if(page.HasMore){return page;}
                if(request.Schema is{Length:>0}){return page;}
                var registered=this.RegisteredFunctions(user,request,database,0,token);
                if(page.Items.Length==0){return registered;}
                return page with{HasMore=registered.Items.Length>0||registered.HasMore,NextCursor=RegisteredFunctionCursor};
            }
            if(request.Schema is{Length:>0}){return new([],0,false,token);}
            return this.RegisteredFunctions(user,request,database,request.Cursor-RegisteredFunctionCursor,token);
        }
    }
    private CatalogPage<FunctionSummary> RegisteredFunctions(UserAccount user,CatalogRequest request,DefinitionVersion database,long cursor,string token){
        var page=this.store.SelectPage<DefinitionVersion,FunctionSummary>("active",cursor,100,
            value=>value.Definition.Kind==DefinitionKind.Query&&string.Equals(this.store.CurrentDatabase(value.Definition),database.Database,StringComparison.OrdinalIgnoreCase)&&
                (string.IsNullOrEmpty(request.NameFilter)||value.Definition.Id.Contains(request.NameFilter,StringComparison.OrdinalIgnoreCase)||value.Definition.EntryMember.Contains(request.NameFilter,StringComparison.OrdinalIgnoreCase)||value.Definition.EntryType.Contains(request.NameFilter,StringComparison.OrdinalIgnoreCase)),
            value=>new(new(FunctionOrigin.RegisteredEntry,request.DatabaseId,value.Definition.Id,value.Definition.Id),value.Definition.Id,"",value.Version,value.ArtifactHash,value.Definition.WritesData){DeclaringType=value.Definition.EntryType,EntryMember=value.Definition.EntryMember});
        return new(page.Items,RegisteredFunctionCursor+page.NextCursor,page.HasMore,token);
    }
    public FunctionDetails GetFunctionDetails(UserAccount user,FunctionTarget target){
        target.Validate();
        lock(this.store.Gate){
            var (database,definition)=this.ResolveFunction(user,target,false);
            if(target.Origin==FunctionOrigin.RegisteredEntry){return this.runtime.RegisteredFunctionDetails(definition,database,target);}
            var details=this.ObjectDetails(user,new(target.DatabaseId,ObjectId:target.ObjectId));
            if(details.Object.Kind is not("ScalarFunction" or "TableFunction" or "Property")){throw new ManagementException("NotFound","関数がありません。");}
            string? element=null;if(details.Object.Kind=="TableFunction"){element=details.Object.RecordType;}
            return new(new(target,details.Object.Name,details.Object.Schema,database.Version,database.ArtifactHash,false),details.Parameters,details.ReturnType??details.Object.RecordType,details.Object.Kind=="TableFunction",details.Columns,details.Source){ElementType=element};
        }
    }
    private (DefinitionVersion Database,DefinitionVersion Definition) ResolveFunction(UserAccount user,FunctionTarget target,bool execute){
        target.Validate();user=this.CurrentActor(user);
        var database=this.store.Read<DefinitionVersion>("active",target.DatabaseId[3..])??throw new ManagementException("NotFound","稼働DBがありません。");
        if(database.Definition.Kind!=DefinitionKind.Database){throw new ManagementException("InvalidRequest","DB定義ではありません。");}
        if(execute){Require(user,database.Database,ManagementPermission.Execute);}else if(!Can(user,database.Database,ManagementPermission.View)){Require(user,database.Database,ManagementPermission.Execute);}
        if(target.Origin==FunctionOrigin.DatabaseMember){if(target.DefinitionId!=database.Definition.Id){throw new ManagementException("InvalidRequest","関数のDB識別が一致しません。");}return(database,database);}
        var definition=this.store.Read<DefinitionVersion>("active",target.DefinitionId)??throw new ManagementException("NotFound","公開された登録関数がありません。");
        if(definition.Definition.Kind!=DefinitionKind.Query||!string.Equals(this.store.CurrentDatabase(definition.Definition),database.Database,StringComparison.OrdinalIgnoreCase)){throw new ManagementException("InvalidRequest","登録関数の所属DBが一致しません。");}
        return(database,definition);
    }
    public Task<ObjectExecutionResult> ExecuteFunctionAsync(UserAccount user,FunctionExecutionRequest request,CancellationToken cancellation=default){
        request=request.Freeze();var details=this.GetFunctionDetails(user,request.Target);
        if(request.ResourceHeatOptions is not null){this.AuthorizeResourceHeat(user);}
        if(request.MeasurementMode==ObjectExecutionMeasurementMode.Profiled){this.AuthorizeProfile(user,request.Target.DefinitionId);}
        if(details.UnavailableReason is not null){throw new ManagementException("Unsupported",details.UnavailableReason);}
        if(request.ExpectedVersion is{} version&&version!=details.Function.Version||request.ExpectedArtifactHash is{} hash&&hash!=details.Function.ArtifactHash){throw new ManagementException("CatalogChanged","関数の公開版または成果物が変わりました。詳細を再取得してください。");}
        if(request.Arguments.Length!=details.Parameters.Length){throw new ManagementException("InvalidRequest","関数の引数の数が一致しません。");}
        if(request.Target.Origin==FunctionOrigin.DatabaseMember){
            string name;lock(this.store.Gate){name=this.ResolveFunction(user,request.Target,true).Database.Database;}
            return this.ExecuteObjectAsync(user,new(name,request.Target.ObjectId,request.Arguments,request.Limit,details.Function.Version){MeasurementMode=request.MeasurementMode,TraceOptions=request.TraceOptions,ResourceHeatOptions=request.ResourceHeatOptions},cancellation);
        }
        return Task.FromResult(this.ExecuteRegisteredFunction(user,request,details,cancellation));
    }
    private ObjectExecutionResult ExecuteRegisteredFunction(UserAccount user,FunctionExecutionRequest request,FunctionDetails details,CancellationToken cancellation){
        DefinitionVersion definition;lock(this.store.Gate){definition=this.ResolveFunction(user,request.Target,true).Definition;}
        var values=new Dictionary<string,JsonElement>(StringComparer.Ordinal);
        for(var index=0;index<details.Parameters.Length;index++){values.Add(details.Parameters[index].Name,request.Arguments[index]);}
        var arguments=JsonSerializer.SerializeToElement(values,ManagementJson.Options);
        if(Encoding.UTF8.GetByteCount(arguments.GetRawText())>NativeRuntime.FunctionResponseByteLimit){throw new ManagementException("Limit","関数の引数が要求上限を超えています。");}
        var execution=new ExecuteRequest(definition.Definition.Id,null,arguments,request.Limit){ExpectedVersion=details.Function.Version,ExpectedArtifactHash=details.Function.ArtifactHash,ExpectedDatabaseDefinitionId=request.Target.DatabaseId[3..]};
        var rows=new List<JsonElement>();var bytes=0;var maximumBytes=NativeRuntime.FunctionResponseByteLimit;
        if(request.MeasurementMode==ObjectExecutionMeasurementMode.Profiled){maximumBytes-=ObjectExecutionProfiling.MaximumResponseBytes;}
        ObjectExecutionResult Snapshot(bool sequence)=>new(details.Function.Version,sequence,Scalar(sequence),Sequence(sequence),details.Columns,false);
        JsonElement? Scalar(bool sequence){if(sequence||rows.Count==0){return null;}return rows[0];}
        JsonElement[] Sequence(bool sequence){if(!sequence){return [];}return rows.ToArray();}
        using var capture=this.runtime.PrepareFunctionExecution(request,definition.Database,Snapshot,()=>{lock(this.store.Gate){_=this.ResolveFunction(user,request.Target,true);if(request.ResourceHeatOptions is not null){this.AuthorizeResourceHeat(user);}if(request.MeasurementMode==ObjectExecutionMeasurementMode.Profiled){this.AuthorizeProfile(user,request.Target.DefinitionId);}}},cancellation);
        try{
            foreach(var row in this.runtime.ExecuteFunction(definition.Definition,execution,this.CurrentActor(user),capture,cancellation)){
                var size=Encoding.UTF8.GetByteCount(row.Value.GetRawText())+256;
                if(size>maximumBytes-bytes){throw new ManagementException("Limit","関数の結果が応答上限を超えたため、処理を取り消しました。");}
                bytes+=size;rows.Add(row.Value.Clone());
            }
        }catch(Exception error){capture.Fail(error);throw;}
        capture.Complete();
        if(request.ResourceHeatOptions is not null){this.AuthorizeResourceHeat(user);}
        if(request.MeasurementMode==ObjectExecutionMeasurementMode.Profiled){this.AuthorizeProfile(user,request.Target.DefinitionId);}
        return Snapshot(capture.IsSequence) with{HasMore=capture.HasMore,Statistics=capture.Statistics,TraceExecutionId=capture.TraceExecutionId,ResourceHeat=capture.ResourceHeat,Profile=capture.Profile};
    }
}
