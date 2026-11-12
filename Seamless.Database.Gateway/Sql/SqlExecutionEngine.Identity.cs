using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Sql;

public sealed partial class SqlExecutionEngine{
    internal IReadOnlyList<QueryResult> ExecuteModuleBatch(string sql,SessionContext session,IReadOnlyList<SqlParameter>? parameters=null,IReadOnlyDictionary<string,object?>? values=null){
        var scope=new SessionContext.IdentityScope();
        return session.Run(()=>{
            using var module=session.EnterIdentityScope(scope);var results=this.ExecuteBatch(sql,session,parameters,values);
            return(IReadOnlyList<QueryResult>)results.Select(result=>QueryResult.Defer(()=>session.Run(()=>{using var execution=session.EnterIdentityScope(scope);_=result.Columns;return result;}))).ToArray();
        });
    }
    private decimal? IdentityCurrent(SessionContext session,string name){
        if(this.registry.GatewayTransactions is not{TypedDdlEnabled:true} manager){throw new SqlQueryException("IDENT_CURRENT requires the typed Gateway profile.",40515);}
        var parsed=new TSql160Parser(true).Parse(new StringReader("SELECT 1 FROM "+name),out var errors);
        if(errors.Count!=0||parsed is not TSqlScript{Batches.Count:1} script||script.Batches[0].Statements is not{Count:1} statements||statements[0] is not SelectStatement{QueryExpression:QuerySpecification{FromClause.TableReferences.Count:1} query}||query.FromClause.TableReferences[0] is not NamedTableReference reference||reference.SchemaObject.ServerIdentifier is not null){return null;}
        var table=reference.SchemaObject;
        if(reference.Alias is not null||reference.TableHints.Count!=0||query.WhereClause is not null||query.OrderByClause is not null||query.GroupByClause is not null||query.HavingClause is not null||table.FragmentLength!=name.Trim().Length){return null;}
        try{return manager.IdentityCurrent(session,table.DatabaseIdentifier?.Value??session.DatabaseName,table.SchemaIdentifier?.Value??"",table.BaseIdentifier.Value);}
        catch(SqlQueryException error)when(error.Number is 208 or 911){return null;}
    }
}
