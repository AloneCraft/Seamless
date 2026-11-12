using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;
using Seamless.Security;

namespace Seamless.Database.Gateway.Http;

internal sealed class HttpAuthorizedSqlCatalog(DatabaseRegistry registry,HttpGatewayAuthorization authorization,AuthorizationLease operation,
    Func<SqlObjectName,SqlTable?>? metadata=null):ISqlCatalog{
    private readonly Dictionary<(SqlObjectName Name,SqlDefinitionKind Kind),IReadOnlyList<Guid>> definitions=[];
    private (string Database,IReadOnlyList<Guid> Resources)? delegatedRead;
    public long Generation=>registry.Generation;
    public bool CanCachePlans=>false;

    public long GetGeneration(string database){
        this.DemandDatabase(database);
        return registry.GetGeneration(database);
    }

    public string? GetDatabaseCollation(string database){
        this.DemandDatabase(database);
        return registry.GetDatabaseCollation(database);
    }

    public IDisposable? AcquireRead(IReadOnlyCollection<string> databases,CancellationToken cancellationToken=default){
        operation.ThrowIfCancellationRequested();
        return registry.AcquireRead(databases,cancellationToken);
    }

    public SqlTable FindTable(SqlObjectName name){
        this.DemandDatabase(name.Database);
        return metadata?.Invoke(name)??registry.FindAuthorizedTable(name,this.DemandTable,this.DemandView)??throw Denied();
    }

    public SqlRoutine FindRoutine(SqlObjectName name){
        this.DemandDatabase(name.Database);
        return registry.FindAuthorizedScalarFunction(name,this.DemandFunction)??throw Denied();
    }

    public IDisposable EnterDefinition(SqlObjectName name,SqlDefinitionKind kind){
        operation.ThrowIfCancellationRequested();
        if(!this.definitions.TryGetValue((name,kind),out var grants)){throw Denied();}
        return this.DelegateRead(name.Database,grants);
    }

    internal IDisposable EnterProcedure(SqlObjectName name,out DatabaseRoutineDefinition procedure){
        this.DemandDatabase(name.Database);
        HttpSqlObjectBinding? binding=null;
        procedure=registry.FindAuthorizedSqlRoutine(name,RoutineKind.StoredProcedure,(adapter,target)=>{
            binding=authorization.AccessSqlObject(operation,adapter,target,SecurityResourceKind.Procedure,SecurityPermission.Execute)??throw Denied();
        })?.Definition??throw Denied();
        return this.DelegateRead(name.Database,binding!.DelegatedReadResources);
    }

    private IDisposable DelegateRead(string database,IReadOnlyList<Guid> resources){
        var previous=this.delegatedRead;
        this.delegatedRead=(database,resources);
        return new DefinitionScope(this,previous);
    }

    private sealed class DefinitionScope(HttpAuthorizedSqlCatalog owner,(string Database,IReadOnlyList<Guid> Resources)? previous):IDisposable{
        public void Dispose()=>owner.delegatedRead=previous;
    }

    private void DemandDatabase(string database){
        if(!authorization.CanDiscover(operation,database)){throw Denied();}
    }

    private void DemandTable(string database,IDatabaseAdapter adapter,TableMetadata table){
        var delegated=this.delegatedRead is{} scope&&string.Equals(scope.Database,database,StringComparison.OrdinalIgnoreCase)?scope.Resources:null;
        if(!authorization.CanAccessTable(operation,database,adapter,table,SecurityPermission.Read,delegated)){throw Denied();}
    }

    internal void DemandMutation(string database,IDatabaseAdapter adapter,TableMetadata table,SqlCommandKind kind){
        this.DemandDatabase(database);
        if(table.SchemaName.Equals("sys",StringComparison.OrdinalIgnoreCase)||table.TableName.StartsWith('#')){throw Denied();}
        var permission=kind switch{
            SqlCommandKind.Insert=>SecurityPermission.Insert,
            SqlCommandKind.Update=>SecurityPermission.Update|SecurityPermission.Read,
            SqlCommandKind.Delete=>SecurityPermission.Delete|SecurityPermission.Read,
            _=>throw Denied()
        };
        if(!authorization.CanAccessTable(operation,database,adapter,table,permission)){throw Denied();}
    }

    private void DemandView(IDatabaseAdapter adapter,SqlObjectName name){
        var binding=authorization.AccessSqlObject(operation,adapter,name,SecurityResourceKind.View,SecurityPermission.Read)??throw Denied();
        this.definitions[(name,SqlDefinitionKind.View)]=binding.DelegatedReadResources;
    }

    private void DemandFunction(IDatabaseAdapter adapter,SqlObjectName name){
        var binding=authorization.AccessSqlObject(operation,adapter,name,SecurityResourceKind.ScalarFunction,SecurityPermission.Execute)??throw Denied();
        this.definitions[(name,SqlDefinitionKind.ScalarFunction)]=binding.DelegatedReadResources;
    }

    private static HttpSessionException Denied()=>new(403,"The identity is not permitted to use this SQL target.");
}
