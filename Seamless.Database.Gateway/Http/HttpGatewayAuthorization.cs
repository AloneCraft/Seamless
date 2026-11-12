using Seamless.Authentication;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;
using Seamless.Security;

namespace Seamless.Database.Gateway.Http;

public sealed record HttpSqlObjectBinding(Guid ResourceId,IReadOnlyList<Guid> DelegatedReadResources);

/// <summary>ホストが所有する認証器・現行認可ポリシー・DB識別子の解決。</summary>
public sealed class HttpGatewayAuthorization{
    internal OidcTokenVerifier Tokens{get;}
    private readonly Func<AuthenticatedIdentity,CancellationToken,ValueTask<AuthorizationLease?>> beginOperation;
    private readonly Func<AuthenticatedIdentity,CancellationToken,ValueTask<AuthorizationLease?>>? beginTransaction;
    private readonly Func<string,Guid?> databaseId;
    private readonly Func<IDatabaseAdapter,int,Guid?>? tableId;
    private readonly Func<IDatabaseAdapter,SecurityResourceKind,string,string,HttpSqlObjectBinding?>? sqlObject;
    internal bool SupportsTableAuthorization=>this.tableId is not null;
    internal bool SupportsSqlAuthorization=>this.tableId is not null||this.sqlObject is not null;
    internal bool SupportsTransactions=>this.beginTransaction is not null;
    public HttpGatewayAuthorization(OidcTokenVerifier tokens,AuthorizationManager policy,Func<string,Guid?> databaseId)
        :this(tokens,FromManager(policy),databaseId){}
    /// <summary>検証済みの主体に対し現行ポリシーで操作を開始する。現行版を確認できない場合はnullを返す。</summary>
    public HttpGatewayAuthorization(OidcTokenVerifier tokens,Func<AuthenticatedIdentity,CancellationToken,AuthorizationLease?> beginOperation,Func<string,Guid?> databaseId)
        :this(tokens,AsAsync(beginOperation),databaseId){}
    /// <summary>外部の認可元を待つ間に、HTTPの要求処理スレッドを占有しない。</summary>
    public HttpGatewayAuthorization(OidcTokenVerifier tokens,Func<AuthenticatedIdentity,CancellationToken,ValueTask<AuthorizationLease?>> beginOperation,Func<string,Guid?> databaseId)
        :this(tokens,beginOperation,databaseId,null){}
    /// <summary>解決済みのDBと永続テーブルIDを、ホストが確認した共通認可リソースへ対応付ける。</summary>
    public HttpGatewayAuthorization(OidcTokenVerifier tokens,Func<AuthenticatedIdentity,CancellationToken,ValueTask<AuthorizationLease?>> beginOperation,Func<string,Guid?> databaseId,Func<IDatabaseAdapter,int,Guid?>? tableId){
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(beginOperation);
        ArgumentNullException.ThrowIfNull(databaseId);
        this.Tokens=tokens;this.beginOperation=beginOperation;this.databaseId=databaseId;this.tableId=tableId;
    }
    public HttpGatewayAuthorization(OidcTokenVerifier tokens,Func<AuthenticatedIdentity,CancellationToken,ValueTask<AuthorizationLease?>> beginOperation,Func<string,Guid?> databaseId,Func<IDatabaseAdapter,int,Guid?>? tableId,Func<IDatabaseAdapter,SecurityResourceKind,string,string,Guid?> sqlObjectId)
        :this(tokens,beginOperation,databaseId,tableId,WithoutDelegation(sqlObjectId)){}
    public HttpGatewayAuthorization(OidcTokenVerifier tokens,Func<AuthenticatedIdentity,CancellationToken,ValueTask<AuthorizationLease?>> beginOperation,Func<string,Guid?> databaseId,Func<IDatabaseAdapter,int,Guid?>? tableId,Func<IDatabaseAdapter,SecurityResourceKind,string,string,HttpSqlObjectBinding?> sqlObject)
        :this(tokens,beginOperation,databaseId,tableId){
        ArgumentNullException.ThrowIfNull(sqlObject);
        this.sqlObject=sqlObject;
    }
    public HttpGatewayAuthorization(OidcTokenVerifier tokens,Func<AuthenticatedIdentity,CancellationToken,ValueTask<AuthorizationLease?>> beginOperation,Func<string,Guid?> databaseId,Func<IDatabaseAdapter,int,Guid?>? tableId,Func<IDatabaseAdapter,SecurityResourceKind,string,string,HttpSqlObjectBinding?> sqlObject,Func<AuthenticatedIdentity,ValueTask<AuthorizationLease?>> beginTransaction)
        :this(tokens,beginOperation,databaseId,tableId,sqlObject,WithCancellationCheck(beginTransaction)){}
    public HttpGatewayAuthorization(OidcTokenVerifier tokens,Func<AuthenticatedIdentity,CancellationToken,ValueTask<AuthorizationLease?>> beginOperation,Func<string,Guid?> databaseId,Func<IDatabaseAdapter,int,Guid?>? tableId,Func<IDatabaseAdapter,SecurityResourceKind,string,string,HttpSqlObjectBinding?> sqlObject,Func<AuthenticatedIdentity,CancellationToken,ValueTask<AuthorizationLease?>> beginTransaction)
        :this(tokens,beginOperation,databaseId,tableId,sqlObject){
        ArgumentNullException.ThrowIfNull(beginTransaction);this.beginTransaction=beginTransaction;
    }
    private static Func<AuthenticatedIdentity,CancellationToken,ValueTask<AuthorizationLease?>> WithCancellationCheck(Func<AuthenticatedIdentity,ValueTask<AuthorizationLease?>> beginTransaction){
        ArgumentNullException.ThrowIfNull(beginTransaction);
        return (identity,cancellation)=>{cancellation.ThrowIfCancellationRequested();return beginTransaction(identity);};
    }
    private static Func<IDatabaseAdapter,SecurityResourceKind,string,string,HttpSqlObjectBinding?> WithoutDelegation(Func<IDatabaseAdapter,SecurityResourceKind,string,string,Guid?> sqlObjectId){
        ArgumentNullException.ThrowIfNull(sqlObjectId);
        return (adapter,kind,schema,name)=>sqlObjectId(adapter,kind,schema,name) is{} id?new(id,[]):null;
    }
    private static Func<AuthenticatedIdentity,CancellationToken,AuthorizationLease?> FromManager(AuthorizationManager policy){
        ArgumentNullException.ThrowIfNull(policy);
        return (identity,token)=>{using var session=policy.CreateSession(identity);return session.BeginOperation(token);};
    }
    private static Func<AuthenticatedIdentity,CancellationToken,ValueTask<AuthorizationLease?>> AsAsync(Func<AuthenticatedIdentity,CancellationToken,AuthorizationLease?> beginOperation){
        ArgumentNullException.ThrowIfNull(beginOperation);
        return (identity,token)=>new(beginOperation(identity,token));
    }
    internal async ValueTask<AuthorizationLease?> BeginOperationAsync(AuthenticatedIdentity identity,CancellationToken token){
        var operation=await this.beginOperation(identity,token).ConfigureAwait(false);
        if(token.IsCancellationRequested){operation?.Dispose();token.ThrowIfCancellationRequested();}
        if(operation is not null&&operation.Snapshot.Subject!=identity.Subject){
            operation.Dispose();
            return null;
        }
        return operation;
    }
    internal async ValueTask<AuthorizationLease?> BeginTransactionAsync(AuthenticatedIdentity identity,CancellationToken cancellation){
        if(this.beginTransaction is null){return null;}
        cancellation.ThrowIfCancellationRequested();
        var transaction=await this.beginTransaction(identity,cancellation).ConfigureAwait(false);
        if(cancellation.IsCancellationRequested){transaction?.Dispose();cancellation.ThrowIfCancellationRequested();}
        if(transaction is not null&&(transaction.Snapshot.Subject!=identity.Subject||transaction.Audit.Deadline is null)){
            transaction.Dispose();return null;
        }
        return transaction;
    }
    internal bool CanDiscover(AuthorizationLease operation,string database){
        operation.ThrowIfCancellationRequested();
        return this.FindDatabase(operation.Snapshot,database) is{} resource
            &&operation.Snapshot.Evaluate(resource.Id,SecurityPermission.Discover).Allowed;
    }
    internal bool CanAccessTable(AuthorizationLease operation,string database,IDatabaseAdapter adapter,TableMetadata table,SecurityPermission permission,IReadOnlyList<Guid>? delegatedRead=null){
        operation.ThrowIfCancellationRequested();
        return table.StableObjectId is{} objectId&&this.tableId?.Invoke(adapter,objectId) is{} id
            &&this.CanAccessObject(operation.Snapshot,database,id,SecurityResourceKind.Table,table.SchemaName,table.TableName,permission,delegatedRead);
    }
    internal HttpSqlObjectBinding? AccessSqlObject(AuthorizationLease operation,IDatabaseAdapter adapter,SqlObjectName name,SecurityResourceKind kind,SecurityPermission permission){
        operation.ThrowIfCancellationRequested();
        if(this.sqlObject?.Invoke(adapter,kind,name.Schema,name.Name) is{} binding
            &&this.CanAccessObject(operation.Snapshot,name.Database,binding.ResourceId,kind,name.Schema,name.Name,permission)){return binding;}
        return null;
    }
    private bool CanAccessObject(AuthorizationSnapshot snapshot,string database,Guid id,SecurityResourceKind kind,string schemaName,string name,SecurityPermission permission,IReadOnlyList<Guid>? delegatedRead=null){
        if(this.FindDatabase(snapshot,database) is not{} db||snapshot.Resource(id) is not{ParentId:{} parent} resource
            ||resource.Kind!=kind||!string.Equals(resource.Name,name,StringComparison.OrdinalIgnoreCase)){return false;}
        if(schemaName.Length==0){
            if(parent!=db.Id){return false;}
        }else if(snapshot.Resource(parent) is not{Kind:SecurityResourceKind.Schema} schema
            ||schema.ParentId!=db.Id||!string.Equals(schema.Name,schemaName,StringComparison.OrdinalIgnoreCase)){return false;}
        var decision=snapshot.Evaluate(id,permission);
        return decision.Allowed||permission==SecurityPermission.Read&&kind==SecurityResourceKind.Table&&decision.Code=="NotGranted"&&delegatedRead?.Contains(id)==true;
    }
    private SecurityResource? FindDatabase(AuthorizationSnapshot snapshot,string database){
        if(this.databaseId(database) is{} id&&snapshot.Resource(id) is{Kind:SecurityResourceKind.Database} resource
            &&string.Equals(resource.Name,database,StringComparison.OrdinalIgnoreCase)){return resource;}
        return null;
    }
}
