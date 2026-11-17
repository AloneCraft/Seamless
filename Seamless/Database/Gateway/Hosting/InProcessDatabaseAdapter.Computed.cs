using System.Reflection;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;
using Seamless.Database.Gateway.Sql;
using Seamless.Execution;
using Seamless.Linq;

namespace Seamless.Database.Gateway.Hosting;

public sealed partial class InProcessDatabaseAdapter{
    private readonly Dictionary<TableBinding,ColumnCatalog[]> persistedComputed=[];
    [ThreadStatic]private static int computedDepth;
    internal void ConfigureComputed(DatabaseCatalog catalog,ISqlCatalog registry){
        var expressions=new GatewayExpressionCatalog(catalog,registry,this);foreach(var owner in catalog.Schemas){foreach(var table in owner.Tables){if(!table.Columns.Any(static column=>column.ComputedDefinition is not null)){continue;}var binding=this.tables[new TableMetadata(owner.Name,table.Name,[]).IdentifierKey];var queries=new SqlQueryEngine(expressions,0);var bound=table.Columns.Where(static column=>column.ComputedDefinition is not null).Select(column=>{var expression=GatewayComputedExpressions.Rewrite(column.ComputedDefinition!,table);return new ComputedBinding(column,expression.Sql,expression.Inputs,queries.Prepare(expression.Sql,catalog.Name,GatewayComputedExpressions.Parameters(expression.Inputs)));}).ToArray();
            typeof(InProcessDatabaseAdapter).GetMethod(nameof(RegisterComputed),BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(binding.RecordType).Invoke(null,[this,binding,bound,queries,catalog.Name]);this.persistedComputed[binding]=table.Columns.Where(static column=>column.IsComputedPersisted).ToArray();
        }}
    }
    private sealed record ComputedBinding(ColumnCatalog Column,string Sql,ColumnCatalog[] Inputs,SqlQueryPlan Plan){internal SqlQueryPlan Current{get;set;}=Plan;}
    private static void RegisterComputed<TRow>(InProcessDatabaseAdapter adapter,TableBinding binding,ComputedBinding[] expressions,SqlQueryEngine queries,string database) where TRow:struct{
        GeneratedRowExpression<TRow>.Evaluate=(row,ordinal)=>{
            if(computedDepth>=32){throw new SqlQueryException("Computed column evaluation exceeded nesting limit 32.",217);}computedDepth++;try{var expression=expressions.Single(item=>item.Column.Ordinal==ordinal);var values=new Dictionary<string,object?>();for(var index=0;index<expression.Inputs.Length;index++){var input=expression.Inputs[index];values.Add("@c"+index,NormalizeValue(binding.ColumnGetters[input.Ordinal](row)));}object? value;var options=new ExecutionOptions{ParentBudget=adapter.database.Options.ParentBudget,CancellationToken=DatabaseRegistry.CurrentSqlSession?.CancellationToken??default,StatementTimestamp=DatabaseRegistry.CurrentSqlSession?.StatementTimestamp};try{value=expression.Current.Execute(values,options).Rows.Single()[0];}catch(SqlQueryException error)when(error.Number==3961){lock(expression){expression.Current=queries.Prepare(expression.Sql,database,GatewayComputedExpressions.Parameters(expression.Inputs));}value=expression.Current.Execute(values,options).Rows.Single()[0];}if(value is null&&!expression.Column.Type.IsNullable){throw new SqlQueryException("Cannot insert NULL into a non-nullable computed column.",515);}return SqlScalarConversion.Assign(value,ComparisonHost.ToSqlType(expression.Column.Type));}finally{computedDepth--;}
        };
    }
    internal void NormalizePersistedComputed(SessionContext session){
        foreach(var (binding,columns) in this.persistedComputed){if(columns.Length==0){continue;}using var scope=new ExecutionScope(new(){ParentBudget=this.database.Options.ParentBudget,CancellationToken=session.CancellationToken});using var rows=scope.Query(binding.GetRows(this.database).Cast<object>()).Select(binding.Encode).ToStorage();foreach(var encoded in rows){session.CancellationToken.ThrowIfCancellationRequested();var row=binding.Decode(encoded);var values=columns.ToDictionary(column=>column.Name,column=>binding.ColumnGetters[column.Ordinal](row));var updated=binding.UpdateRecord(row,binding.MapValues(values));if(!encoded.AsSpan().SequenceEqual(binding.Encode(updated))){binding.SetOrUpdateRecord(this.database,row,updated);}}}
    }
}
