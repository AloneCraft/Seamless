using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Gateway.Sql;

public sealed class SqlExecutionEngine{
    private readonly IDatabaseAdapter adapter;
    private readonly QueryPlanCache planCache;

    public SqlExecutionEngine(IDatabaseAdapter adapter){
        this.adapter=adapter;
        this.planCache=new QueryPlanCache(adapter);
    }

    public QueryResult Execute(string sql,SessionContext session){
        if(string.IsNullOrWhiteSpace(sql)){
            return QueryResult.Empty;
        }else{
        }

        if(SystemCatalogEmulator.TryEmulate(sql,this.adapter,out var catalogResult)){
            return catalogResult;
        }else{
        }

        var cmd=this.planCache.GetOrParse(sql);
        if(cmd.Kind==SqlCommandKind.Select){
            return this.adapter.ExecuteSelect(cmd.TableName,cmd.Columns,cmd.Predicate,session);
        }else if(cmd.Kind==SqlCommandKind.Insert){
            var affected=this.adapter.ExecuteInsert(cmd.TableName,cmd.Values??new Dictionary<string,object?>(),session);
            return QueryResult.NonQuery(affected);
        }else if(cmd.Kind==SqlCommandKind.Update){
            var affected=this.adapter.ExecuteUpdate(cmd.TableName,cmd.Values??new Dictionary<string,object?>(),cmd.Predicate,session);
            return QueryResult.NonQuery(affected);
        }else if(cmd.Kind==SqlCommandKind.Delete){
            var affected=this.adapter.ExecuteDelete(cmd.TableName,cmd.Predicate,session);
            return QueryResult.NonQuery(affected);
        }else if(cmd.Kind==SqlCommandKind.BeginTransaction){
            this.adapter.BeginTransaction(session);
            return QueryResult.NonQuery(0);
        }else if(cmd.Kind==SqlCommandKind.CommitTransaction){
            this.adapter.CommitTransaction(session);
            return QueryResult.NonQuery(0);
        }else if(cmd.Kind==SqlCommandKind.RollbackTransaction){
            this.adapter.RollbackTransaction(session);
            return QueryResult.NonQuery(0);
        }else{
            return QueryResult.NonQuery(0);
        }
    }
}
