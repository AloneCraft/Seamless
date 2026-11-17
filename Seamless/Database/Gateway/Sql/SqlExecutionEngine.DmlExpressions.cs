using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Sql;

public sealed partial class SqlExecutionEngine{
    // DML の式も SELECT／DEFAULT と同じ SQL の型推論・変換・関数評価を使う。
    private Func<object,object?> CompileDmlScalar(ScalarExpression expression,string sql,DatabaseRegistration database,TableMetadata? table,bool allowColumn,SessionContext session,IReadOnlyList<SqlParameter>? parameters,IReadOnlyDictionary<string,object?>? values,Action enterExecution,SqlQueryEngine? protectedQueries)=>this.CompileDmlExpression(expression,sql,database,table,allowColumn,session,parameters,values,enterExecution,static text=>"SELECT "+text,protectedQueries);
    private Func<object,bool?> CompileDmlPredicate(BooleanExpression expression,string sql,DatabaseRegistration database,TableMetadata table,SessionContext session,IReadOnlyList<SqlParameter>? parameters,IReadOnlyDictionary<string,object?>? values,Action enterExecution,SqlQueryEngine? protectedQueries){
        var evaluate=this.CompileDmlExpression(expression,sql,database,table,true,session,parameters,values,enterExecution,static text=>"SELECT CASE WHEN "+text+" THEN 1 ELSE 0 END",protectedQueries);return row=>Equals(evaluate(row),1);
    }
    private Func<object,object?> CompileDmlExpression(TSqlFragment expression,string sql,DatabaseRegistration database,TableMetadata? table,bool allowColumn,SessionContext session,IReadOnlyList<SqlParameter>? parameters,IReadOnlyDictionary<string,object?>? values,Action enterExecution,Func<string,string> projection,SqlQueryEngine? protectedQueries){
        var references=new DmlScalarReferences();expression.Accept(references);if(!allowColumn&&references.Columns.Count!=0){throw new SqlQueryException("A VALUES expression cannot reference a target column.",207);}
        var text=sql.Substring(expression.StartOffset,expression.FragmentLength);var bindings=new List<(ColumnMetadata Column,string Parameter)>();var inputParameters=parameters?.ToList()??[];var prefix="@__seamless_dml_";
        while(inputParameters.Any(parameter=>parameter.Name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))){prefix+="_";}
        foreach(var reference in references.Columns.OrderByDescending(static reference=>reference.StartOffset)){
            var names=reference.MultiPartIdentifier.Identifiers;if(table is null||names.Count>4||names.Count>=2&&!names[^2].Value.Equals(table.TableName,StringComparison.OrdinalIgnoreCase)||names.Count>=3&&!names[^3].Value.Equals(table.SchemaName,StringComparison.OrdinalIgnoreCase)||names.Count==4&&!names[0].Value.Equals(database.Name,StringComparison.OrdinalIgnoreCase)){throw new SqlQueryException("Invalid target column reference.",4104);}
            var column=table.Columns.SingleOrDefault(column=>column.Name.Equals(names[^1].Value,StringComparison.OrdinalIgnoreCase))??throw new SqlQueryException("Invalid column '"+names[^1].Value+"'.",207);var parameter=prefix+bindings.Count;bindings.Add((column,parameter));inputParameters.Add(new(parameter,new(column.SqlTypeName,column.ClrType,column.MaximumLength,column.Precision,column.Scale,column.Collation),column.IsNullable){IsColumnValue=true});
            text=text.Remove(reference.StartOffset-expression.StartOffset,reference.FragmentLength).Insert(reference.StartOffset-expression.StartOffset,parameter);
        }
        var plan=(protectedQueries??this.queries).Prepare(projection(text),database.Name,inputParameters);
        return row=>{
            enterExecution();if(bindings.Count==0){return plan.Execute(values,this.QueryOptions(session)).Rows.Single()[0];}
            var arguments=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);if(values is not null){foreach(var value in values){arguments.Add(value.Key,value.Value);}}
            var tableName=protectedQueries is not null&&database.Adapter is InProcessDatabaseAdapter?table!.IdentifierKey:table!.FullName;
            foreach(var binding in bindings){arguments.Add(binding.Parameter,database.Adapter.GetRecordValue(tableName,binding.Column.Name,row));}
            return plan.Execute(arguments,this.QueryOptions(session)).Rows.Single()[0];
        };
    }
    private sealed class DmlScalarReferences:TSqlFragmentVisitor{
        internal List<ColumnReferenceExpression> Columns{get;}=[];
        public override void ExplicitVisit(ColumnReferenceExpression node){this.Columns.Add(node);base.ExplicitVisit(node);}
        public override void ExplicitVisit(ScalarSubquery node)=>throw new SqlQueryException("A correlated DML scalar subquery is not supported.",40515);
    }
}
