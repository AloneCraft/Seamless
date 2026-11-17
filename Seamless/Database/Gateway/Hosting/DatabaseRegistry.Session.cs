namespace Seamless.Database.Gateway.Hosting;

public sealed partial class DatabaseRegistry{
    public IDisposable? AcquireRead(IReadOnlyCollection<string> databases,CancellationToken cancellationToken=default){
        var leases=new List<IDisposable>();try{
            if(sqlSession.Value is null){var session=new SessionContext(0,this.DefaultDatabaseName){CancellationToken=cancellationToken};leases.Add(session);leases.Add(EnterSqlSession(session));}
            var adapters=databases.Where(database=>this.databases.ContainsKey(database)||!Sql.SqlServerSystemShape.IsSystemDatabase(database)).Select(this.Resolve).Select(static registration=>registration.Adapter).OfType<InProcessDatabaseAdapter>().Distinct().OrderBy(static adapter=>adapter.Database.Options.GatewayProfile?.DatabaseId??0).ToArray();
            foreach(var adapter in adapters){var lease=ReadSqlDatabase(adapter);if(lease is not null){leases.Add(lease);}}return new QueryLeases(leases);
        }catch{for(var index=leases.Count-1;index>=0;index--){leases[index].Dispose();}throw;}
    }
    private sealed class QueryLeases(List<IDisposable> leases):IDisposable{public void Dispose(){for(var index=leases.Count-1;index>=0;index--){leases[index].Dispose();}}}
    private static readonly AsyncLocal<SessionContext?> sqlSession=new();
    internal static SessionContext? CurrentSqlSession=>sqlSession.Value;
    internal static IDisposable EnterSqlSession(SessionContext session){var previous=sqlSession.Value;sqlSession.Value=session;return new SessionLease(previous);}
    private sealed class SessionLease(SessionContext? previous):IDisposable{public void Dispose(){sqlSession.Value=previous;}}
    internal static IDisposable? ReadSqlDatabase(IDatabaseAdapter adapter,SessionContext? session=null){
        if(adapter is not InProcessDatabaseAdapter inProcess){return null;}
        return inProcess.AcquireReadSession(session??sqlSession.Value);
    }
    internal IEnumerable<object?[]> EnumerateSql(SessionContext session,IEnumerable<object?[]> rows){
        using var context=EnterSqlSession(session);
        foreach(var row in rows){yield return row;}
    }
}
