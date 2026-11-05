using Seamless.Database.Client;
using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Gateway.Native;

public sealed partial class NativeQueryServer{
    private static int MaximumIncomingPayload(byte kind){
        if(kind==NativeLambdaWire.Prepare){return NativeLambdaProgram.MaximumBytes+514;}
        if(kind is NativeFrame.Request or NativeLambdaWire.Execute){return NativeFrame.MaximumBytes-1;}
        throw new InvalidDataException("Unknown native request kind.");
    }
    private readonly Dictionary<string,ILambdaDatabase> lambdaDatabases=new(StringComparer.OrdinalIgnoreCase);
    /// <summary>公開表現と認可を明示登録する。現行Native listenerはLoopbackの開発用入口。</summary>
    public void RegisterLambda<TDatabase>(string database,NativeLambdaSchema<TDatabase> schema)=>this.RegisterLambda(database,schema,256);
    public void RegisterLambda<TDatabase>(string database,NativeLambdaSchema<TDatabase> schema,int maximumPreparedQueries){
        if(this.listener is not null){throw new InvalidOperationException("Register lambda schemas before starting the server.");}
        this.databases.Resolve(database);
        var runtime=new LambdaDatabase<TDatabase>(new(schema){MaximumPreparedQueries=maximumPreparedQueries});
        if(!this.lambdaDatabases.TryAdd(database,runtime)){runtime.Dispose();throw new InvalidOperationException("The lambda database is already registered.");}
    }
    private NativePreparedLambda PrepareLambda(NativeQueryRequest request,DatabaseRegistration database){
        if(!this.lambdaDatabases.TryGetValue(database.Name,out var runtime)){throw new NativeLambdaException("Capability","The server has not exposed a lambda contract for this database.",true);}
        return runtime.Prepare(NativeLambdaProgram.Decode(request.Arguments),database.SchemaFingerprint);
    }
    private (INativeQueryModule Module,NativeQueryDefinition Definition) ResolveQuery(NativeQueryRequest request,DatabaseRegistration database){
        if(request.ModuleId=="$lambda"){
            if(!this.lambdaDatabases.TryGetValue(database.Name,out var runtime)){throw new NativeLambdaException("Capability","The lambda database is not registered.",true);}
            var result=runtime.Resolve(NativeLambdaWire.ReadHandle(Convert.FromHexString(request.QueryId)),database.Name,database.SchemaFingerprint);
            if(database.IsReadOnly&&!result.Definition.IsReadOnly){throw new NativeLambdaException("Permission","The database is read only.",true);}
            return result;
        }
        if(!this.modules.TryGetValue(request.ModuleId,out var module)){throw new NativeQueryException("The native query module is not installed.");}
        var definition=module.Manifest.Queries.SingleOrDefault(query=>query.Id==request.QueryId&&query.Database==database.Name)??throw new NativeQueryException("The native query is not registered for this database.");
        ValidateContracts(definition,request);
        if(definition.SchemaFingerprint!=database.SchemaFingerprint||request.SchemaFingerprint!=database.SchemaFingerprint){throw new NativeQueryException("The native query schema fingerprint is stale.");}
        return(module.Module,definition);
    }
    private static NativeQueryRequest ReadLambdaRequest(byte kind,byte[] bytes){
        var request=NativeLambdaWire.ReadRequest(bytes);
        if(kind==NativeLambdaWire.Prepare){if(request.Payload.Length>NativeLambdaProgram.MaximumBytes){throw new InvalidDataException("The lambda program is too large.");}return new("$lambda.prepare","",request.Database,"",request.Payload);}
        var execution=NativeLambdaWire.ReadExecution(request.Payload);
        return new("$lambda",Convert.ToHexString(NativeLambdaWire.Handle(execution.Handle)),request.Database,"",execution.Arguments,execution.Transaction);
    }
    private interface ILambdaDatabase:IDisposable{
        NativePreparedLambda Prepare(NativeLambdaProgram program,string generation);
        (INativeQueryModule Module,NativeQueryDefinition Definition) Resolve(NativePreparedLambda handle,string database,string generation);
    }
    private sealed class LambdaDatabase<TDatabase>(NativeLambdaDatabase<TDatabase> runtime):ILambdaDatabase{
        public NativePreparedLambda Prepare(NativeLambdaProgram program,string generation)=>runtime.Prepare(program,generation);
        public (INativeQueryModule Module,NativeQueryDefinition Definition) Resolve(NativePreparedLambda handle,string database,string generation)=>runtime.Resolve(handle,database,generation);
        public void Dispose()=>runtime.Dispose();
    }
}
