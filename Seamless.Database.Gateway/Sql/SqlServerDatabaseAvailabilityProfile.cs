using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Seamless.Database.Gateway.Sql;

internal static class SqlServerDatabaseAvailabilityProfile{
    private static readonly Lazy<TSqlScript> template=new(()=>SqlServerDatabaseQueryProfile.Read("SsmsObjectExplorerDatabase.sql"));
    private static IReadOnlyList<TSqlStatement> Statements=>template.Value.Batches[0].Statements.ToArray();
    private static int SelectIndex=>Statements.Count-5;
    internal static bool IsPrefix(TSqlScript script){
        if(SystemCatalogEmulator.ProfileHadrEnabled!=0||script.Batches.Count!=1||script.Batches[0].Statements.Count!=SelectIndex){return false;}
        return script.Batches[0].Statements.Select((s,i)=>SqlServerDatabaseQueryProfile.Same(s,Statements[i])).All(equal=>equal);
    }
    internal static bool IsCleanup(TSqlStatement statement,int ordinal)=>ordinal is >=0 and <4&&SqlServerDatabaseQueryProfile.Same(statement,Statements[SelectIndex+ordinal+1]);
    internal static bool TryBatch(TSqlScript script,string sql,out string result){
        result="";if(SystemCatalogEmulator.ProfileHadrEnabled!=0||script.Batches.Count!=1||script.Batches[0].Statements.Count!=Statements.Count){return false;}
        var actual=script.Batches[0].Statements;
        for(var i=0;i<actual.Count;i++){if(i!=SelectIndex&&!SqlServerDatabaseQueryProfile.Same(actual[i],Statements[i])){return false;}}
        if(actual[SelectIndex] is not SelectStatement select){return false;}
        return Rewrite(select,sql,true,out result);
    }
    internal static bool TrySelect(SelectStatement select,string sql,out string result)=>Rewrite(select,sql,false,out result);
    private static bool Rewrite(SelectStatement select,string sql,bool force,out string result){
        var visitor=new TempReferences();select.Accept(visitor);result="";
        if(visitor.Tables.Count==0&&!force){return false;}
        result=sql.Substring(select.StartOffset,select.FragmentLength);
        foreach(var table in visitor.Tables.OrderByDescending(t=>t.StartOffset)){
            var index=table.StartOffset-select.StartOffset;
            result=result.Remove(index,table.FragmentLength).Insert(index,"(SELECT CAST(NULL AS int) AS database_id,CAST(NULL AS tinyint) AS sync_state WHERE 1=0)");
        }
        return true;
    }
    private sealed class TempReferences:TSqlFragmentVisitor{
        internal readonly List<SchemaObjectName>Tables=[];
        public override void ExplicitVisit(NamedTableReference table){if(table.SchemaObject.Identifiers.Count==1&&table.SchemaObject.BaseIdentifier.Value.Equals("#tmp_sync_states",StringComparison.OrdinalIgnoreCase)){this.Tables.Add(table.SchemaObject);}}
    }
}
