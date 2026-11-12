using Seamless.Database.Query;
using Seamless.Database.Gateway.Sql;

namespace Seamless.Database.Gateway.Hosting;

public sealed partial class GatewayTransactionManager{
    internal void ApplyReferentialActions(SessionContext session,InProcessDatabaseAdapter adapter,TableMetadata parent,object oldRow,object? newRow){
        if(this.generationStore is null){return;}var database=this.databases[adapter.Database.Options.GatewayProfile!.DatabaseId].Name;var schema=this.TransactionSchema(session,database);var engine=new SqlExecutionEngine(this.generationRegistry!);
        foreach(var owner in schema.Definition.Schemas){foreach(var child in owner.Tables){foreach(var key in child.ForeignKeys.Where(key=>key.ReferencedSchema.Equals(parent.SchemaName,StringComparison.OrdinalIgnoreCase)&&key.ReferencedTable.Equals(parent.TableName,StringComparison.OrdinalIgnoreCase))){
            var action=newRow is null?key.DeleteAction:key.UpdateAction;if(action=="NO_ACTION"){continue;}var parameters=new List<SqlParameter>();var values=new Dictionary<string,object?>();var predicates=new List<string>();var setters=new List<string>();var changed=newRow is null;
            for(var index=0;index<key.Columns.Count;index++){
                var mapping=key.Columns[index];var column=parent.Columns.Single(column=>column.Name.Equals(mapping.ReferencedColumn,StringComparison.OrdinalIgnoreCase));var oldValue=adapter.GetRecordValue(parent.FullName,column.Name,oldRow);var oldParameter="@old"+index;parameters.Add(new(oldParameter,new(column.SqlTypeName,column.ClrType,column.MaximumLength,column.Precision,column.Scale,column.Collation)));values.Add(oldParameter,oldValue);predicates.Add(Quote(mapping.Column)+"="+oldParameter);
                if(newRow is not null){var newValue=adapter.GetRecordValue(parent.FullName,column.Name,newRow);if(!StoredValuesEqual(oldValue,newValue)){changed=true;}var newParameter="@new"+index;parameters.Add(new(newParameter,parameters[^1].Type));values.Add(newParameter,newValue);if(action=="CASCADE"){setters.Add(Quote(mapping.Column)+"="+newParameter);}}
                if(action is "SET_NULL" or "SET_DEFAULT"){setters.Add(Quote(mapping.Column)+"="+(action=="SET_NULL"?"NULL":"DEFAULT"));}
            }
            if(!changed){continue;}var name=Quote(database)+"."+Quote(owner.Name)+"."+Quote(child.Name);var sql=newRow is null&&action=="CASCADE"?"DELETE FROM "+name:"UPDATE "+name+" SET "+string.Join(',',setters);sql+=" WHERE "+string.Join(" AND ",predicates);foreach(var row in engine.Execute(sql,session,parameters,values).Rows){}
        }}}
        static string Quote(string name)=>"["+name.Replace("]","]]",StringComparison.Ordinal)+"]";
    }
    // SQL Server cascades case-only/zero-padding changes too; collation equality is insufficient here.
    private static bool StoredValuesEqual(object? first,object? second)=>
        first is byte[] left&&second is byte[] right?left.AsSpan().SequenceEqual(right):
        first is DateTimeOffset leftOffset&&second is DateTimeOffset rightOffset?leftOffset.EqualsExact(rightOffset):Equals(first,second);
}
