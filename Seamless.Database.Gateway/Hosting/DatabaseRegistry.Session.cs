namespace Seamless.Database.Gateway.Hosting;

public sealed partial class DatabaseRegistry{
    private static readonly AsyncLocal<SessionContext?> sqlSession=new();
    internal static IDisposable EnterSqlSession(SessionContext session){var previous=sqlSession.Value;sqlSession.Value=session;return new SessionLease(previous);}
    private sealed class SessionLease(SessionContext? previous):IDisposable{public void Dispose(){sqlSession.Value=previous;}}
    internal static IDisposable? ReadSqlDatabase(IDatabaseAdapter adapter,SessionContext? session=null){
        if(adapter is not InProcessDatabaseAdapter inProcess){return null;}
        return inProcess.AcquireReadSession(session??sqlSession.Value);
    }
    internal IEnumerable<object?[]> EnumerateSql(SessionContext session,IEnumerable<object?[]> rows){
        using var context=EnterSqlSession(session);using var read=ReadSqlDatabase(this.Resolve(session.DatabaseName).Adapter,session);
        foreach(var row in rows){yield return row;}
    }
}
